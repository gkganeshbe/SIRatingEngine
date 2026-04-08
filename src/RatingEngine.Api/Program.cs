
using Dapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RatingEngine.Api;
using RatingEngine.Core;
using RatingEngine.Data;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenIddict.Validation.AspNetCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRatingProblemDetails();

builder.Host.UseSerilog((ctx, services, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

var configDir = Path.Combine(AppContext.BaseDirectory, "..", "RatingEngine.Config");
var storageProvider = builder.Configuration["StorageProvider"] ?? "FileSystem";

// Register Dapper DateOnly handler once at startup (applies process-wide).
SqlMapper.AddTypeHandler(DateOnlyTypeHandler.Instance);

// ── Tenancy ──────────────────────────────────────────────────────────────────
// ITenantStore reads tenant→connection-string mappings from appsettings.json
// (ConnectionStrings section). Swap ConfigurationTenantStore for a DB-backed
// implementation once tenant config moves to a database.
builder.Services.AddSingleton<ITenantStore, ConfigurationTenantStore>();
// TenantContext is scoped: one instance per HTTP request.
// TenantMiddleware populates it; repositories inject ITenantContext (read-only).
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

builder.Services.AddSingleton<IPipelineFactory, JsonPipelineFactory>();

// ── Authentication / Authorization ───────────────────────────────────────────
var authMode = builder.Configuration["Auth:Mode"] ?? "IdentityServer";

if (authMode == "ApiKey")
{
    var apiKeys = builder.Configuration.GetSection("Auth:ApiKeys").Get<string[]>() ?? [];

    builder.Services
        .AddAuthentication(ApiKeyAuthenticationOptions.SchemeName)
        .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
            ApiKeyAuthenticationOptions.SchemeName,
            opts => opts.ApiKeys = apiKeys);

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("QuoteAccess", policy =>
        {
            policy.AuthenticationSchemes.Add(ApiKeyAuthenticationOptions.SchemeName);
            policy.RequireAuthenticatedUser();
            policy.RequireClaim("scope", "quote.access");
        });
    });
}
else
{
    var authority     = builder.Configuration["IdentityServer:Authority"]!;
    var quoteClientId = builder.Configuration["IdentityServer:QuoteApiClientId"]!;
    var quoteSecret   = builder.Configuration["IdentityServer:QuoteApiClientSecret"]!;

    builder.Services.AddOpenIddict()
        .AddValidation(options =>
        {
            options.SetIssuer(authority);
            options.UseIntrospection()
                   .SetClientId(quoteClientId)
                   .SetClientSecret(quoteSecret);
            options.UseSystemNetHttp();
            options.UseAspNetCore();
        });

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("QuoteAccess", policy =>
        {
            policy.AuthenticationSchemes.Add(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
            policy.RequireAuthenticatedUser();
            policy.RequireClaim("scope", "quote.access");
        });
    });
}

if (storageProvider == "Database")
{
    // ── Database storage ──────────────────────────────────────────────────────
    // All DB implementations are scoped because they depend on ITenantContext
    // (which carries the per-request tenant connection string).
    builder.Services.AddMemoryCache();
    builder.Services.AddScoped<DbConnectionFactory>();
    builder.Services.AddScoped<IProductManifestRepository, SqlProductManifestRepository>();
    builder.Services.AddScoped<ICoverageConfigRepository, SqlCoverageConfigRepository>();
    // IRateLookupFactory is scoped: each request creates lookups scoped to the resolved CoverageConfig.DbId.
    builder.Services.AddScoped<IRateLookupFactory, DbRateLookupFactory>();
}
else
{
    // ── File-system storage (default) ─────────────────────────────────────────
    builder.Services.AddSingleton<IProductManifestRepository>(sp =>
        new FileProductManifestRepository(configDir));

    builder.Services.AddSingleton<ICoverageConfigRepository>(sp =>
        new FileCoverageConfigRepository(configDir));

    // Rate tables live in per-coverage subdirs: data/rates/{productCode}.{state}.{coverageCode}.{version}/
    var ratesDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "data", "rates");
    builder.Services.AddSingleton<IRateLookupFactory>(new FileRateLookupFactory(ratesDir));
}

var app = builder.Build();

// ── Middleware ────────────────────────────────────────────────────────────────
// Must run before routing so that every endpoint is protected.
app.UseRatingProblemDetails();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TenantMiddleware>();

// ── Endpoint: single coverage rate ─────────────────────────────────────────
// Risk is a flat open dictionary – any LOB-specific attributes can be included.
app.MapPost("/quote/rate", async (
    RatingRequest req,
    IProductManifestRepository manifestRepo,
    ICoverageConfigRepository coverageRepo,
    IPipelineFactory factory, IRateLookupFactory lookupFactory,
    CancellationToken cancellationToken) =>
{
    var manifest = await manifestRepo.GetAsync(req.ProductCode, req.RateEffectiveDate, cancellationToken);
    if (manifest is null) return Results.NotFound(new { message = "Product not found or no active version for the effective date" });

    var covRef = manifest.Coverages.FirstOrDefault(c => c.CoverageCode == req.CoverageCode);
    if (covRef is null) return Results.NotFound(new { message = "Coverage not found in product manifest" });

    var coverage = await coverageRepo.GetAsync(req.ProductCode, req.RateState, req.CoverageCode, req.RateEffectiveDate, cancellationToken);
    if (coverage is null) return Results.NotFound(new { message = "Coverage config not found for the given product/state/coverage/date" });

    var lookup = lookupFactory.CreateForCoverage(coverage);
    var results = new List<object>();
    decimal coverageTotal = 0m;

    foreach (var peril in coverage.Perils)
    {
        var risk = new Dictionary<string, string>(req.Risk, StringComparer.OrdinalIgnoreCase);
        var ctx = new RateContext(req.ProductCode, coverage.Version, req.RateEffectiveDate, req.RateState, risk, peril, 0m);
        var pipeline = factory.Build(coverage.Pipeline);
        var (premium, trace) = new PipelineRunner(pipeline, lookup).Run(ctx);
        results.Add(new { peril, premium, trace });
        coverageTotal += premium;
    }

    return Results.Ok(new { coveragePremium = coverageTotal, perils = results });
}).WithName("Rate").RequireAuthorization("QuoteAccess");

// ── Endpoint: policy-level segments ────────────────────────────────────────────
app.MapPost("/quote/rate-policy-segments", async (
    PolicySegmentRequest req,
    IProductManifestRepository manifestRepo,
    ICoverageConfigRepository coverageRepo,
    IPipelineFactory factory, IRateLookupFactory lookupFactory,
    CancellationToken cancellationToken) =>
{
    if (req.Segments is null || req.Segments.Count == 0)
        return Results.BadRequest(new { message = "At least one segment is required" });

    var totalPolicyDays = req.PolicyTo.DayNumber - req.PolicyFrom.DayNumber;
    if (totalPolicyDays <= 0)
        return Results.BadRequest(new { message = "PolicyTo must be after PolicyFrom" });

    var manifest = await manifestRepo.GetAsync(req.ProductCode, req.PolicyFrom, cancellationToken);
    if (manifest is null) return Results.NotFound(new { message = "Product not found or no active version for the effective date" });

    var coverageAccum = new Dictionary<string, (string Name, decimal Total, List<object> Segments)>();

    // Collect all coverage configs for lookup resolution in policy adjustments
    var allCovConfigs = new Dictionary<string, CoverageConfig>(StringComparer.OrdinalIgnoreCase);

    // Values published by coverage pipelines that persist across segments (e.g. a base rate
    // snapshotted in segment 1 and referenced by a dependent coverage in segment 2).
    var policyPublished = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    foreach (var segment in req.Segments)
    {
        var segmentDays     = segment.To.DayNumber - segment.From.DayNumber;
        var prorationFactor = (decimal)segmentDays / totalPolicyDays;

        // ── Step 1: load all coverage configs for this segment upfront ──────
        var segCovConfigs = new Dictionary<string, CoverageConfig>(StringComparer.OrdinalIgnoreCase);
        foreach (var covInput in segment.Coverages)
        {
            if (manifest.AllCoverages.All(c => c.CoverageCode != covInput.Id))
                return Results.NotFound(new { message = $"Coverage '{covInput.Id}' not found in product manifest" });

            var cfg = await coverageRepo.GetAsync(req.ProductCode, req.RateState, covInput.Id, segment.RateEffectiveDate, cancellationToken);
            if (cfg is null) return Results.NotFound(new { message = $"Coverage config not found for '{covInput.Id}'" });

            segCovConfigs[covInput.Id] = cfg;
            allCovConfigs[covInput.Id] = cfg;
        }

        // ── Step 2: order by dependency so each coverage can read predecessors ──
        var ordered = TopologicalSort(segment.Coverages, c => c.Id, c => segCovConfigs[c.Id].DependsOn);

        // Shared within-segment context: annual premiums + published values from this segment.
        // Seeded with any values published by earlier segments.
        var sharedCtx = new Dictionary<string, string>(policyPublished, StringComparer.OrdinalIgnoreCase);

        foreach (var covInput in ordered)
        {
            var coverage = segCovConfigs[covInput.Id];
            var lookup   = lookupFactory.CreateForCoverage(coverage);

            decimal segmentPremium = 0m;
            object  segmentDetail;

            if (covInput.RatingType == "SCHEDLEVEL" && covInput.Schedules?.Count > 0)
            {
                var schedResults = new List<object>();
                foreach (var schedule in covInput.Schedules)
                {
                    var risk = RiskBag.Merge(RiskBag.Merge(segment.Property, covInput.Params), schedule);
                    InjectSharedContext(risk, sharedCtx);
                    var schedId = schedule.TryGetValue("ScheduleId", out var sid) ? sid : (schedResults.Count + 1).ToString();
                    var (annualPremium, perilResults) = RunPerils(coverage, risk, req.ProductCode, segment.RateEffectiveDate, req.RateState, factory, lookup);
                    var proRatedPremium = Math.Round(annualPremium * prorationFactor, 2, MidpointRounding.AwayFromZero);
                    schedResults.Add(new { scheduleId = schedId, annualPremium, proRatedPremium, perils = perilResults });
                    segmentPremium += proRatedPremium;
                }
                // Inject annual-equivalent into sharedCtx for any dependent coverage pipelines
                sharedCtx[$"cov_{covInput.Id}_Premium"] =
                    prorationFactor > 0m
                        ? (segmentPremium / prorationFactor).ToString(CultureInfo.InvariantCulture)
                        : "0";
                segmentDetail = new { from = segment.From, to = segment.To, prorationFactor, segmentPremium, schedules = schedResults };
            }
            else
            {
                var risk = RiskBag.Merge(segment.Property, covInput.Params);
                InjectSharedContext(risk, sharedCtx);
                var (annualPremium, perilResults) = RunPerils(coverage, risk, req.ProductCode, segment.RateEffectiveDate, req.RateState, factory, lookup);
                var proRatedPremium = Math.Round(annualPremium * prorationFactor, 2, MidpointRounding.AwayFromZero);
                segmentPremium = proRatedPremium;

                // Inject ANNUAL premium so downstream coverage pipelines see a full-year figure
                sharedCtx[$"cov_{covInput.Id}_Premium"] = annualPremium.ToString(CultureInfo.InvariantCulture);

                // Publish selected risk-bag keys for downstream coverages
                foreach (var key in coverage.Publish)
                    if (risk.TryGetValue(key, out var v))
                    {
                        sharedCtx[key]      = v;
                        policyPublished[key] = v;   // persist to later segments
                    }

                segmentDetail = new { from = segment.From, to = segment.To, prorationFactor, segmentPremium, perils = perilResults };
            }

            if (!coverageAccum.TryGetValue(covInput.Id, out var accum))
                coverageAccum[covInput.Id] = accum = (covInput.Name, 0m, new List<object>());

            accum.Segments.Add(segmentDetail);
            coverageAccum[covInput.Id] = accum with { Total = accum.Total + segmentPremium };
        }
    }

    var coverageResults = coverageAccum.Select(kv => new
    {
        id            = kv.Key,
        name          = kv.Value.Name,
        coverageTotal = kv.Value.Total,
        segments      = kv.Value.Segments
    }).ToList();

    // ── Policy adjustments (multi-LOB credits, minimum premiums, etc.) ──────
    var covTotals  = coverageAccum.ToDictionary(kv => kv.Key, kv => kv.Value.Total, StringComparer.OrdinalIgnoreCase);
    var adjEffDate = req.Segments.Count > 0 ? req.Segments[0].RateEffectiveDate : req.PolicyFrom;
    var (adjResults, adjTotal) = RunPolicyAdjustments(
        manifest.PolicyAdjustments, covTotals,
        new Dictionary<string, decimal>(), lobCount: 1,
        req.ProductCode, adjEffDate, req.RateState,
        factory, policyPublished,
        covCode => allCovConfigs.TryGetValue(covCode, out var cfg) ? lookupFactory.CreateForCoverage(cfg) : NullRateLookup.Instance);

    var subTotal = coverageResults.Sum(c => c.coverageTotal);
    return Results.Ok(new { subTotal, policyTotal = subTotal + adjTotal, coverages = coverageResults, adjustments = adjResults });
}).WithName("RatePolicySegments").RequireAuthorization("QuoteAccess");

// ── Endpoint: coverage-level segments ──────────────────────────────────────────
// Each coverage has its own independent timeline. Cross-coverage dependencies are resolved
// at the coverage level: a coverage's DependsOn values are satisfied by the totals of those
// coverages as rated so far (injected as $risk.cov_{code}_Premium into each segment's risk bag).
app.MapPost("/quote/rate-coverage-segments", async (
    CoverageLevelSegmentRequest req,
    IProductManifestRepository manifestRepo,
    ICoverageConfigRepository coverageRepo,
    IPipelineFactory factory, IRateLookupFactory lookupFactory,
    CancellationToken cancellationToken) =>
{
    if (req.Coverages is null || req.Coverages.Count == 0)
        return Results.BadRequest(new { message = "At least one coverage is required" });

    var totalPolicyDays = req.PolicyTo.DayNumber - req.PolicyFrom.DayNumber;
    if (totalPolicyDays <= 0)
        return Results.BadRequest(new { message = "PolicyTo must be after PolicyFrom" });

    var manifest = await manifestRepo.GetAsync(req.ProductCode, req.PolicyFrom, cancellationToken);
    if (manifest is null) return Results.NotFound(new { message = "Product not found or no active version for the effective date" });

    // ── Step 1: load all coverage configs and sort by dependency ────────────
    var allCovConfigs = new Dictionary<string, CoverageConfig>(StringComparer.OrdinalIgnoreCase);
    foreach (var covInput in req.Coverages)
    {
        if (covInput.Segments is null || covInput.Segments.Count == 0)
            return Results.BadRequest(new { message = $"Coverage '{covInput.Id}' has no segments" });

        if (manifest.AllCoverages.All(c => c.CoverageCode != covInput.Id))
            return Results.NotFound(new { message = $"Coverage '{covInput.Id}' not found in product manifest" });

        // Use the first segment's effective date to load the config; the coverage is versioned at policy inception.
        var cfg = await coverageRepo.GetAsync(req.ProductCode, req.RateState, covInput.Id, req.PolicyFrom, cancellationToken);
        if (cfg is null) return Results.NotFound(new { message = $"Coverage config not found for '{covInput.Id}'" });

        allCovConfigs[covInput.Id] = cfg;
    }

    var orderedCoverages = TopologicalSort(req.Coverages, c => c.Id, c => allCovConfigs[c.Id].DependsOn);

    // Cross-coverage context: accumulates final (pro-rated) totals from already-rated coverages
    // and published values so dependent coverages can reference them in their pipelines.
    var policyPublished  = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    var covTotalsAccum   = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
    var coverageResults  = new List<object>();

    foreach (var covInput in orderedCoverages)
    {
        var coverage = allCovConfigs[covInput.Id];
        var lookup   = lookupFactory.CreateForCoverage(coverage);
        var segmentResults = new List<object>();
        decimal coverageTotal = 0m;

        foreach (var segment in covInput.Segments)
        {
            var segmentDays     = segment.To.DayNumber - segment.From.DayNumber;
            var prorationFactor = (decimal)segmentDays / totalPolicyDays;
            decimal segmentPremium = 0m;
            object  segmentDetail;

            if (covInput.RatingType == "SCHEDLEVEL" && covInput.Schedules?.Count > 0)
            {
                var schedResults = new List<object>();
                foreach (var schedule in covInput.Schedules)
                {
                    var risk = RiskBag.Merge(RiskBag.Merge(segment.Property, segment.Params), schedule);
                    InjectSharedContext(risk, policyPublished);
                    var schedId = schedule.TryGetValue("ScheduleId", out var sid) ? sid : (schedResults.Count + 1).ToString();
                    var (annualPremium, perilResults) = RunPerils(coverage, risk, req.ProductCode, segment.RateEffectiveDate, req.RateState, factory, lookup);
                    var proRatedPremium = Math.Round(annualPremium * prorationFactor, 2, MidpointRounding.AwayFromZero);
                    schedResults.Add(new { scheduleId = schedId, annualPremium, proRatedPremium, perils = perilResults });
                    segmentPremium += proRatedPremium;
                }
                segmentDetail = new { from = segment.From, to = segment.To, prorationFactor, segmentPremium, schedules = schedResults };
            }
            else
            {
                var risk = RiskBag.Merge(segment.Property, segment.Params);
                InjectSharedContext(risk, policyPublished);
                // Also inject already-accumulated coverage totals so dependency premiums are visible
                foreach (var kv in covTotalsAccum)
                    risk[$"cov_{kv.Key}_Premium"] = kv.Value.ToString(CultureInfo.InvariantCulture);

                var (annualPremium, perilResults) = RunPerils(coverage, risk, req.ProductCode, segment.RateEffectiveDate, req.RateState, factory, lookup);
                var proRatedPremium = Math.Round(annualPremium * prorationFactor, 2, MidpointRounding.AwayFromZero);
                segmentPremium = proRatedPremium;

                foreach (var key in coverage.Publish)
                    if (risk.TryGetValue(key, out var v))
                        policyPublished[key] = v;

                segmentDetail = new { from = segment.From, to = segment.To, prorationFactor, segmentPremium, perils = perilResults };
            }

            segmentResults.Add(segmentDetail);
            coverageTotal += segmentPremium;
        }

        covTotalsAccum[covInput.Id] = coverageTotal;
        coverageResults.Add(new { id = covInput.Id, name = covInput.Name, coverageTotal, segments = segmentResults });
    }

    // ── Policy adjustments ──────────────────────────────────────────────────
    var adjEffDate = req.Coverages.SelectMany(c => c.Segments).Select(s => s.RateEffectiveDate).DefaultIfEmpty(req.PolicyFrom).First();
    var (adjResults, adjTotal) = RunPolicyAdjustments(
        manifest.PolicyAdjustments, covTotalsAccum,
        new Dictionary<string, decimal>(), lobCount: 1,
        req.ProductCode, adjEffDate, req.RateState,
        factory, policyPublished,
        covCode => allCovConfigs.TryGetValue(covCode, out var cfg) ? lookupFactory.CreateForCoverage(cfg) : NullRateLookup.Instance);

    var subTotal = covTotalsAccum.Values.Sum();
    return Results.Ok(new { subTotal, policyTotal = subTotal + adjTotal, coverages = coverageResults, adjustments = adjResults });
}).WithName("RateCoverageSegments").RequireAuthorization("QuoteAccess");

// ── Endpoint: commercial multi-LOB policy ──────────────────────────────────────
// Supports multi-LOB submissions where each LOB contains one or more risks
// (POLICY / LOCATION / BUILDING), each with their own applicable coverages.
// Within any coverage, RatingType = "SCHEDLEVEL" is still honoured so that
// coverages like Inland Marine can rate once per scheduled item.
//
// Merge chain: PolicyRisk → LobRisk → Risk.Attributes → CoverageParams → ScheduleFields
//
// Cross-coverage dependency (DependsOn/Publish) is resolved PER RISK: after rating each
// coverage for a building/location/risk, its premium and any published values are written
// back into that risk's baseRisk dict so the next coverage in the dependency chain can read
// them via $risk.cov_{code}_Premium or $risk.{publishedKey}.
//
// Policy adjustments (multi-LOB credits, minimum premiums) run after all LOBs/risks/coverages
// are rated, using policy-wide coverage totals and LOB totals.
//
// Response rolls up: schedule items → coverage → risk → LOB → policy.
app.MapPost("/quote/rate-commercial", async (
    CommercialPolicyRequest req,
    IProductManifestRepository manifestRepo,
    ICoverageConfigRepository coverageRepo,
    IPipelineFactory factory, IRateLookupFactory lookupFactory,
    CancellationToken cancellationToken) =>
{
    var manifest = await manifestRepo.GetAsync(req.ProductCode, req.RateEffectiveDate, cancellationToken);
    if (manifest is null)
        return Results.NotFound(new { message = "Product not found or no active version for the effective date" });

    var allManifestCoverages = manifest.AllCoverages;
    var lobResults           = new List<object>();

    // Policy-level accumulators for policy adjustments
    var policyCovTotals = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
    var policyLobTotals = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
    var policyPublished = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    // All coverage configs loaded during this request (for policy adjustment lookup resolution)
    var allCovConfigs   = new Dictionary<string, CoverageConfig>(StringComparer.OrdinalIgnoreCase);

    foreach (var lob in req.Lobs)
    {
        var riskResults = new List<object>();
        decimal lobTotal = 0m;
        var lobBaseRisk  = RiskBag.Merge(req.PolicyRisk, lob.LobRisk);

        // ── Pre-load all coverage configs for the LOB ───────────────────────
        // Required upfront so we can identify aggregate coverages before
        // starting the per-risk loop.
        var lobCovConfigs = new Dictionary<string, CoverageConfig>(StringComparer.OrdinalIgnoreCase);
        foreach (var risk in lob.Risks)
            foreach (var covInput in risk.Coverages)
            {
                if (lobCovConfigs.ContainsKey(covInput.Id)) continue;
                if (allManifestCoverages.All(c => c.CoverageCode != covInput.Id))
                    return Results.NotFound(new { message = $"Coverage '{covInput.Id}' not found in product manifest" });
                var cfg = await coverageRepo.GetAsync(req.ProductCode, req.RateState, covInput.Id, req.RateEffectiveDate, cancellationToken);
                if (cfg is null)
                    return Results.NotFound(new { message = $"Coverage config not found for '{covInput.Id}'" });
                lobCovConfigs[covInput.Id] = cfg;
                allCovConfigs[covInput.Id] = cfg;
            }

        // Determine which coverages switch to aggregate mode for this LOB.
        // The When condition is evaluated against the LOB-level merged risk bag.
        var lobCtx = new RateContext(req.ProductCode, "", req.RateEffectiveDate, req.RateState,
            new Dictionary<string, string>(lobBaseRisk, StringComparer.OrdinalIgnoreCase), "", 0m);
        var aggregateCovIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (covId, cfg) in lobCovConfigs)
            if (cfg.Aggregate is not null && JsonPipelineFactory.EvalWhen(cfg.Aggregate.When, lobCtx))
                aggregateCovIds.Add(covId);

        // ── Phase A: rate per-risk (non-aggregate coverages only) ───────────
        foreach (var risk in lob.Risks)
        {
            var covResults = new List<object>();
            decimal riskTotal = 0m;

            // PolicyRisk → LobRisk → Risk.Attributes
            var baseRisk = RiskBag.Merge(lobBaseRisk, risk.Attributes);

            var perRiskCovInputs = risk.Coverages
                .Where(c => !aggregateCovIds.Contains(c.Id)).ToList();
            var riskCovConfigs = perRiskCovInputs
                .ToDictionary(c => c.Id, c => lobCovConfigs[c.Id]);
            var orderedCovInputs = TopologicalSort(perRiskCovInputs, c => c.Id, c => riskCovConfigs[c.Id].DependsOn);

            foreach (var covInput in orderedCovInputs)
            {
                var coverage = riskCovConfigs[covInput.Id];
                var lookup   = lookupFactory.CreateForCoverage(coverage);
                decimal covPremium;
                object  covDetail;

                if (covInput.RatingType == "SCHEDLEVEL" && covInput.Schedules?.Count > 0)
                {
                    var schedResults = new List<object>();
                    decimal schedTotal = 0m;
                    foreach (var schedule in covInput.Schedules)
                    {
                        var schedRisk = RiskBag.Merge(RiskBag.Merge(baseRisk, covInput.Params), schedule);
                        var schedId   = schedule.TryGetValue("ScheduleId", out var sid) ? sid : (schedResults.Count + 1).ToString();
                        var (itemPremium, perilResults) = RunPerils(coverage, schedRisk, req.ProductCode, req.RateEffectiveDate, req.RateState, factory, lookup);
                        schedResults.Add(new { scheduleId = schedId, premium = itemPremium, perils = perilResults });
                        schedTotal += itemPremium;
                    }
                    covPremium = schedTotal;
                    covDetail  = new { id = covInput.Id, name = covInput.Name, premium = covPremium, schedules = schedResults };
                }
                else
                {
                    var covRisk = RiskBag.Merge(baseRisk, covInput.Params);
                    InjectSharedContext(covRisk, policyPublished);
                    var (premium, perilResults) = RunPerils(coverage, covRisk, req.ProductCode, req.RateEffectiveDate, req.RateState, factory, lookup);
                    covPremium = premium;
                    covDetail  = new { id = covInput.Id, name = covInput.Name, premium = covPremium, perils = perilResults };

                    baseRisk[$"cov_{covInput.Id}_Premium"] = covPremium.ToString(CultureInfo.InvariantCulture);
                    foreach (var key in coverage.Publish)
                        if (covRisk.TryGetValue(key, out var v))
                        { baseRisk[key] = v; policyPublished[key] = v; }
                }

                covResults.Add(covDetail);
                riskTotal += covPremium;
                policyCovTotals[covInput.Id] = policyCovTotals.GetValueOrDefault(covInput.Id) + covPremium;
            }

            riskResults.Add(new { riskId = risk.RiskId, riskLevel = risk.RiskLevel, locationId = risk.LocationId, riskTotal, coverages = covResults });
            lobTotal += riskTotal;
        }

        // ── Phase B: rate aggregate coverages at LOB level ──────────────────
        // SCHEDLEVEL risks within an aggregate coverage are still rated
        // individually; standard risks are collapsed into one merged context.
        var aggCovResults = new List<object>();
        foreach (var covId in aggregateCovIds)
        {
            var cfg = lobCovConfigs[covId];
            var lookup = lookupFactory.CreateForCoverage(cfg);
            decimal covPremium = 0m;
            object covDetail;

            // Collect all risks in the LOB that carry this coverage
            var risksWithCov = lob.Risks
                .Where(r => r.Coverages.Any(c => c.Id == covId)).ToList();
            var covInputExample = risksWithCov
                .SelectMany(r => r.Coverages).First(c => c.Id == covId);

            // SCHEDLEVEL items within this aggregate coverage: rate individually
            if (covInputExample.RatingType == "SCHEDLEVEL" && covInputExample.Schedules?.Count > 0)
            {
                var schedResults = new List<object>();
                decimal schedTotal = 0m;
                foreach (var schedule in covInputExample.Schedules)
                {
                    var schedRisk = RiskBag.Merge(RiskBag.Merge(lobBaseRisk, covInputExample.Params), schedule);
                    InjectSharedContext(schedRisk, policyPublished);
                    var schedId = schedule.TryGetValue("ScheduleId", out var sid) ? sid : (schedResults.Count + 1).ToString();
                    var (itemPremium, perilResults) = RunPerils(cfg, schedRisk, req.ProductCode, req.RateEffectiveDate, req.RateState, factory, lookup);
                    schedResults.Add(new { scheduleId = schedId, premium = itemPremium, perils = perilResults });
                    schedTotal += itemPremium;
                }
                covPremium = schedTotal;
                covDetail  = new { id = covId, name = covInputExample.Name, premium = covPremium, schedules = schedResults };
            }
            else
            {
                // Standard risks: aggregate into one context and run pipeline once
                var standardRisks = risksWithCov
                    .Where(r => r.Coverages.First(c => c.Id == covId).RatingType != "SCHEDLEVEL")
                    .ToList();
                var aggregateRisk = ComputeAggregateRisk(lobBaseRisk, standardRisks, cfg.Aggregate!);
                InjectSharedContext(aggregateRisk, policyPublished);

                var (premium, perilResults) = RunPerils(cfg, aggregateRisk, req.ProductCode, req.RateEffectiveDate, req.RateState, factory, lookup);
                covPremium = premium;
                covDetail  = new { id = covId, name = covInputExample.Name, premium = covPremium, perils = perilResults, aggregatedRisks = standardRisks.Count };

                foreach (var key in cfg.Publish)
                    if (aggregateRisk.TryGetValue(key, out var v)) policyPublished[key] = v;
            }

            aggCovResults.Add(covDetail);
            lobTotal += covPremium;
            policyCovTotals[covId] = policyCovTotals.GetValueOrDefault(covId) + covPremium;
        }

        policyLobTotals[lob.LobCode] = lobTotal;
        lobResults.Add(new { lobCode = lob.LobCode, lobTotal, risks = riskResults, aggregateCoverages = aggCovResults });
    }

    // ── Policy adjustments ──────────────────────────────────────────────────
    var (adjResults, adjTotal) = RunPolicyAdjustments(
        manifest.PolicyAdjustments, policyCovTotals, policyLobTotals,
        lobCount: req.Lobs.Count,
        req.ProductCode, req.RateEffectiveDate, req.RateState,
        factory, policyPublished,
        covCode => allCovConfigs.TryGetValue(covCode, out var cfg) ? lookupFactory.CreateForCoverage(cfg) : NullRateLookup.Instance);

    var subTotal = policyLobTotals.Values.Sum();
    return Results.Ok(new { subTotal, policyTotal = subTotal + adjTotal, lobs = lobResults, adjustments = adjResults });
}).WithName("RateCommercial").RequireAuthorization("QuoteAccess");

app.Run();

// ── Shared helpers ───────────────────────────────────────────────────────────

// Runs the full pipeline once per peril for a single merged risk dict.
// Returns the sum of all peril premiums and the per-peril breakdown.
// The risk dict is mutated by ComputeStep; callers may read Publish keys from it afterward.
static (decimal total, List<object> perilResults) RunPerils(
    CoverageConfig coverage,
    Dictionary<string, string> risk,
    string productCode, DateOnly effectiveDate, string jurisdiction,
    IPipelineFactory factory, IRateLookup lookup)
{
    var perilResults = new List<object>();
    decimal total = 0m;
    foreach (var peril in coverage.Perils)
    {
        var ctx = new RateContext(productCode, coverage.Version, effectiveDate, jurisdiction, risk, peril, 0m);
        var (premium, trace) = new PipelineRunner(factory.Build(coverage.Pipeline), lookup).Run(ctx);
        perilResults.Add(new { peril, premium, trace });
        total += premium;
    }
    return (total, perilResults);
}

// Copies all entries from sharedCtx into the target risk bag (in-place).
// Builds an aggregate risk bag from a LOB base risk and a list of standard risks.
// For each field in the AggregateConfig, computes the specified function (SUM/AVG/MAX/MIN/COUNT)
// across all risks and injects the result as $risk.{ResultKey}.
// SCHEDLEVEL risks should be excluded from the risks list before calling this.
static Dictionary<string, string> ComputeAggregateRisk(
    Dictionary<string, string> lobBaseRisk,
    List<CommercialRiskInput> risks,
    AggregateConfig cfg)
{
    var agg = new Dictionary<string, string>(lobBaseRisk, StringComparer.OrdinalIgnoreCase);
    foreach (var fieldCfg in cfg.Fields)
    {
        decimal result;
        if (fieldCfg.Function.Equals("COUNT", StringComparison.OrdinalIgnoreCase))
        {
            result = risks.Count;
        }
        else
        {
            var values = risks.Select(r =>
            {
                r.Attributes.TryGetValue(fieldCfg.SourceField, out var raw);
                return decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0m;
            }).ToList();

            result = fieldCfg.Function.ToUpperInvariant() switch
            {
                "SUM" => values.Sum(),
                "AVG" => values.Count > 0 ? values.Sum() / values.Count : 0m,
                "MAX" => values.Count > 0 ? values.Max() : 0m,
                "MIN" => values.Count > 0 ? values.Min() : 0m,
                _     => values.Sum()
            };
        }
        agg[fieldCfg.ResultKey] = result.ToString(CultureInfo.InvariantCulture);
    }
    return agg;
}

// Called before running a coverage pipeline so that dependency premiums and
// published values from prior coverages are visible via $risk.* paths.
static void InjectSharedContext(Dictionary<string, string> target, Dictionary<string, string> sharedCtx)
{
    foreach (var kv in sharedCtx)
        target[kv.Key] = kv.Value;
}

// Topological sort for coverage inputs using Kahn's depth-first algorithm.
// Items with no dependencies (or whose dependencies are not in the same list) come first.
// Throws InvalidOperationException on circular dependencies.
static IReadOnlyList<T> TopologicalSort<T>(
    IEnumerable<T> items,
    Func<T, string> getCode,
    Func<T, IEnumerable<string>> getDependencies)
{
    var list   = items.ToList();
    var byCode = list.ToDictionary(getCode, StringComparer.OrdinalIgnoreCase);
    var result   = new List<T>(list.Count);
    var visited  = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    void Visit(T item)
    {
        var code = getCode(item);
        if (visited.Contains(code)) return;
        if (!visiting.Add(code))
            throw new InvalidOperationException($"Circular coverage dependency detected at '{code}'");
        foreach (var dep in getDependencies(item))
            if (byCode.TryGetValue(dep, out var depItem))
                Visit(depItem);
        visiting.Remove(code);
        visited.Add(code);
        result.Add(item);
    }

    foreach (var item in list) Visit(item);
    return result;
}

// Runs all policy-level adjustments after all coverages have been rated.
// Each adjustment pipeline starts with $premium = ScopedTotal and can produce any adjusted value.
// Returns the adjustment details list and the total net adjustment amount.
static (List<object> Results, decimal TotalAdjustment) RunPolicyAdjustments(
    IReadOnlyList<PolicyAdjustmentConfig> adjustments,
    Dictionary<string, decimal> covTotals,
    Dictionary<string, decimal> lobTotals,
    int lobCount,
    string productCode, DateOnly effectiveDate, string jurisdiction,
    IPipelineFactory factory,
    Dictionary<string, string> publishedValues,
    Func<string, IRateLookup> lookupResolver)
{
    var results         = new List<object>();
    decimal totalAdj    = 0m;
    var policyTotal     = covTotals.Values.Sum();

    foreach (var adj in adjustments)
    {
        // Build the risk bag for this adjustment pipeline
        var ctxBag = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        ctxBag["PolicyTotal"] = policyTotal.ToString(CultureInfo.InvariantCulture);
        ctxBag["LobCount"]    = lobCount.ToString(CultureInfo.InvariantCulture);

        foreach (var kv in covTotals)
            ctxBag[$"cov_{kv.Key}_Premium"] = kv.Value.ToString(CultureInfo.InvariantCulture);
        foreach (var kv in lobTotals)
            ctxBag[$"lob_{kv.Key}_Premium"] = kv.Value.ToString(CultureInfo.InvariantCulture);
        foreach (var kv in publishedValues)
            ctxBag[kv.Key] = kv.Value;

        var scopedTotal = adj.AppliesTo.Count > 0
            ? adj.AppliesTo.Sum(c => covTotals.GetValueOrDefault(c))
            : policyTotal;
        ctxBag["ScopedTotal"] = scopedTotal.ToString(CultureInfo.InvariantCulture);

        var lookup = adj.RateLookupCoverage is not null
            ? lookupResolver(adj.RateLookupCoverage)
            : NullRateLookup.Instance;

        // Pipeline starts with $premium = ScopedTotal
        var adjCtx = new RateContext(productCode, "adj", effectiveDate, jurisdiction, ctxBag, "ADJ", scopedTotal);
        var (adjustedTotal, trace) = new PipelineRunner(factory.Build(adj.Pipeline), lookup).Run(adjCtx);
        var adjustmentAmount = adjustedTotal - scopedTotal;

        results.Add(new
        {
            id               = adj.Id,
            name             = adj.Name,
            scopedTotal,
            adjustedTotal,
            adjustmentAmount,
            appliesTo        = adj.AppliesTo,
            trace
        });

        totalAdj += adjustmentAmount;
    }

    return (results, totalAdj);
}

// ── Request models ───────────────────────────────────────────────────────────
// Risk is a free-form dictionary — any LOB-specific attributes are valid.
// RateState and RateEffectiveDate are first-class routing parameters used by
// the engine to select the correct versioned pipeline; they are not risk factors.

public record RatingRequest(
    string ProductCode,
    string RateState,
    string CoverageCode,
    DateOnly RateEffectiveDate,
    IReadOnlyDictionary<string, string> Risk
);

public record PolicySegmentRequest(
    string ProductCode,
    string RateState,
    DateOnly PolicyFrom,
    DateOnly PolicyTo,
    IReadOnlyList<PolicySegment> Segments
);

public record CoverageLevelSegmentRequest(
    string ProductCode,
    string RateState,
    DateOnly PolicyFrom,
    DateOnly PolicyTo,
    IReadOnlyList<CoverageLevelInput> Coverages
);

/// <summary>
/// Top-level request for commercial multi-LOB rating.
/// PolicyRisk holds attributes shared by every LOB (e.g. state, effective date context).
/// Each LOB carries its own LobRisk and a list of risks (buildings / locations / policy).
/// </summary>
public record CommercialPolicyRequest(
    string ProductCode,
    string RateState,
    DateOnly RateEffectiveDate,
    IReadOnlyDictionary<string, string> PolicyRisk,
    IReadOnlyList<LobInput> Lobs
);
