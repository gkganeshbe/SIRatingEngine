
using Dapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RatingEngine.Api;
using RatingEngine.Core;
using RatingEngine.Data;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

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

if (storageProvider == "Database")
{
    // ── Database storage ──────────────────────────────────────────────────────
    // All DB implementations are scoped because they depend on ITenantContext
    // (which carries the per-request tenant connection string).
    builder.Services.AddMemoryCache();
    builder.Services.AddScoped<DbConnectionFactory>();
    builder.Services.AddScoped<IProductManifestRepository, SqlProductManifestRepository>();
    builder.Services.AddScoped<ICoverageConfigRepository, SqlCoverageConfigRepository>();
    builder.Services.AddScoped<IRateLookup, DbRateLookup>();
}
else
{
    // ── File-system storage (default) ─────────────────────────────────────────
    builder.Services.AddSingleton<IProductManifestRepository>(sp =>
        new FileProductManifestRepository(configDir));

    builder.Services.AddSingleton<ICoverageConfigRepository>(sp =>
        new FileCoverageConfigRepository(configDir));

    builder.Services.AddSingleton<IRateLookup>(sp =>
        InMemoryRateLookup.FromDirectory(Path.Combine(AppContext.BaseDirectory, "..", "..", "data", "rates")));
}

var app = builder.Build();

// ── Middleware ────────────────────────────────────────────────────────────────
// Must run before routing so that every endpoint is protected.
app.UseMiddleware<TenantMiddleware>();

// ── Endpoint: single coverage rate ─────────────────────────────────────────
// Risk is a flat open dictionary – any LOB-specific attributes can be included.
app.MapPost("/quote/{productVersion}/rate", async (
    string productVersion, RatingRequest req,
    IProductManifestRepository manifestRepo,
    ICoverageConfigRepository coverageRepo,
    IPipelineFactory factory, IRateLookup lookup) =>
{
    var manifest = await manifestRepo.GetAsync(req.ProductCode, productVersion);
    if (manifest is null) return Results.NotFound(new { message = "Product or version not found" });

    var covRef = manifest.Coverages.FirstOrDefault(c => c.CoverageCode == req.CoverageCode);
    if (covRef is null) return Results.NotFound(new { message = "Coverage not found in product manifest" });

    var coverage = await coverageRepo.GetAsync(req.ProductCode, req.CoverageCode, covRef.Version);
    if (coverage is null) return Results.NotFound(new { message = "Coverage config not found" });

    var jurisdiction = req.Risk.TryGetValue("State", out var state) ? state : string.Empty;
    var results = new List<object>();
    decimal coverageTotal = 0m;

    foreach (var peril in coverage.Perils)
    {
        var risk = new Dictionary<string, string>(req.Risk, StringComparer.OrdinalIgnoreCase);
        var ctx = new RateContext(req.ProductCode, coverage.Version, req.EffectiveDate, jurisdiction, risk, peril, 0m);
        var pipeline = factory.Build(coverage.Pipeline);
        var (premium, trace) = new PipelineRunner(pipeline, lookup).Run(ctx);
        results.Add(new { peril, premium, trace });
        coverageTotal += premium;
    }

    return Results.Ok(new { coveragePremium = coverageTotal, perils = results });
}).WithName("Rate");

// ── Endpoint: policy-level segments ────────────────────────────────────────────
app.MapPost("/quote/{productVersion}/rate-policy-segments", async (
    string productVersion, PolicySegmentRequest req,
    IProductManifestRepository manifestRepo,
    ICoverageConfigRepository coverageRepo,
    IPipelineFactory factory, IRateLookup lookup) =>
{
    if (req.Segments is null || req.Segments.Count == 0)
        return Results.BadRequest(new { message = "At least one segment is required" });

    var totalPolicyDays = req.PolicyTo.DayNumber - req.PolicyFrom.DayNumber;
    if (totalPolicyDays <= 0)
        return Results.BadRequest(new { message = "PolicyTo must be after PolicyFrom" });

    var manifest = await manifestRepo.GetAsync(req.ProductCode, productVersion);
    if (manifest is null) return Results.NotFound(new { message = "Product or version not found" });

    var coverageAccum = new Dictionary<string, (string Name, decimal Total, List<object> Segments)>();

    foreach (var segment in req.Segments)
    {
        var segmentDays     = segment.To.DayNumber - segment.From.DayNumber;
        var prorationFactor = (decimal)segmentDays / totalPolicyDays;
        var jurisdiction    = segment.Property.TryGetValue("State", out var s) ? s : string.Empty;

        foreach (var covInput in segment.Coverages)
        {
            var covRef = manifest.Coverages.FirstOrDefault(c => c.CoverageCode == covInput.Id);
            if (covRef is null) return Results.NotFound(new { message = $"Coverage '{covInput.Id}' not found in product manifest" });

            var coverage = await coverageRepo.GetAsync(req.ProductCode, covInput.Id, covRef.Version);
            if (coverage is null) return Results.NotFound(new { message = $"Coverage config not found for '{covInput.Id}'" });

            decimal segmentPremium = 0m;
            object segmentDetail;

            if (covInput.RatingType == "SCHEDLEVEL" && covInput.Schedules?.Count > 0)
            {
                // Rate once per schedule entry; sum to coverage premium
                var schedResults = new List<object>();
                foreach (var schedule in covInput.Schedules)
                {
                    var risk = RiskBag.Merge(RiskBag.Merge(segment.Property, covInput.Params), schedule);
                    var schedId = schedule.TryGetValue("ScheduleId", out var sid) ? sid : (schedResults.Count + 1).ToString();
                    var (annualPremium, perilResults) = RunPerils(coverage, risk, req.ProductCode, segment.EffectiveDate, jurisdiction, factory, lookup);
                    var proRatedPremium = Math.Round(annualPremium * prorationFactor, 2, MidpointRounding.AwayFromZero);
                    schedResults.Add(new { scheduleId = schedId, annualPremium, proRatedPremium, perils = perilResults });
                    segmentPremium += proRatedPremium;
                }
                segmentDetail = new { from = segment.From, to = segment.To, prorationFactor, segmentPremium, schedules = schedResults };
            }
            else
            {
                var risk = RiskBag.Merge(segment.Property, covInput.Params);
                var (annualPremium, perilResults) = RunPerils(coverage, risk, req.ProductCode, segment.EffectiveDate, jurisdiction, factory, lookup);
                var proRatedPremium = Math.Round(annualPremium * prorationFactor, 2, MidpointRounding.AwayFromZero);
                segmentPremium = proRatedPremium;
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

    return Results.Ok(new { policyTotal = coverageResults.Sum(c => c.coverageTotal), coverages = coverageResults });
}).WithName("RatePolicySegments");

// ── Endpoint: coverage-level segments ──────────────────────────────────────────
app.MapPost("/quote/{productVersion}/rate-coverage-segments", async (
    string productVersion, CoverageLevelSegmentRequest req,
    IProductManifestRepository manifestRepo,
    ICoverageConfigRepository coverageRepo,
    IPipelineFactory factory, IRateLookup lookup) =>
{
    if (req.Coverages is null || req.Coverages.Count == 0)
        return Results.BadRequest(new { message = "At least one coverage is required" });

    var totalPolicyDays = req.PolicyTo.DayNumber - req.PolicyFrom.DayNumber;
    if (totalPolicyDays <= 0)
        return Results.BadRequest(new { message = "PolicyTo must be after PolicyFrom" });

    var manifest = await manifestRepo.GetAsync(req.ProductCode, productVersion);
    if (manifest is null) return Results.NotFound(new { message = "Product or version not found" });

    var coverageResults = new List<object>();

    foreach (var covInput in req.Coverages)
    {
        if (covInput.Segments is null || covInput.Segments.Count == 0)
            return Results.BadRequest(new { message = $"Coverage '{covInput.Id}' has no segments" });

        var covRef = manifest.Coverages.FirstOrDefault(c => c.CoverageCode == covInput.Id);
        if (covRef is null) return Results.NotFound(new { message = $"Coverage '{covInput.Id}' not found in product manifest" });

        var coverage = await coverageRepo.GetAsync(req.ProductCode, covInput.Id, covRef.Version);
        if (coverage is null) return Results.NotFound(new { message = $"Coverage config not found for '{covInput.Id}'" });

        var segmentResults = new List<object>();
        decimal coverageTotal = 0m;

        foreach (var segment in covInput.Segments)
        {
            var segmentDays     = segment.To.DayNumber - segment.From.DayNumber;
            var prorationFactor = (decimal)segmentDays / totalPolicyDays;
            var jurisdiction    = segment.Property.TryGetValue("State", out var s) ? s : string.Empty;
            decimal segmentPremium = 0m;
            object segmentDetail;

            if (covInput.RatingType == "SCHEDLEVEL" && covInput.Schedules?.Count > 0)
            {
                var schedResults = new List<object>();
                foreach (var schedule in covInput.Schedules)
                {
                    var risk = RiskBag.Merge(RiskBag.Merge(segment.Property, segment.Params), schedule);
                    var schedId = schedule.TryGetValue("ScheduleId", out var sid) ? sid : (schedResults.Count + 1).ToString();
                    var (annualPremium, perilResults) = RunPerils(coverage, risk, req.ProductCode, segment.EffectiveDate, jurisdiction, factory, lookup);
                    var proRatedPremium = Math.Round(annualPremium * prorationFactor, 2, MidpointRounding.AwayFromZero);
                    schedResults.Add(new { scheduleId = schedId, annualPremium, proRatedPremium, perils = perilResults });
                    segmentPremium += proRatedPremium;
                }
                segmentDetail = new { from = segment.From, to = segment.To, prorationFactor, segmentPremium, schedules = schedResults };
            }
            else
            {
                var risk = RiskBag.Merge(segment.Property, segment.Params);
                var (annualPremium, perilResults) = RunPerils(coverage, risk, req.ProductCode, segment.EffectiveDate, jurisdiction, factory, lookup);
                var proRatedPremium = Math.Round(annualPremium * prorationFactor, 2, MidpointRounding.AwayFromZero);
                segmentPremium = proRatedPremium;
                segmentDetail = new { from = segment.From, to = segment.To, prorationFactor, segmentPremium, perils = perilResults };
            }

            segmentResults.Add(segmentDetail);
            coverageTotal += segmentPremium;
        }

        coverageResults.Add(new { id = covInput.Id, name = covInput.Name, coverageTotal, segments = segmentResults });
    }

    return Results.Ok(new { policyTotal = coverageResults.Cast<dynamic>().Sum(c => (decimal)c.coverageTotal), coverages = coverageResults });
}).WithName("RateCoverageSegments");

app.Run();

// ── Shared helpers ───────────────────────────────────────────────────────────

// Runs the full pipeline once per peril for a single merged risk dict.
// Returns the sum of all peril premiums and the per-peril breakdown.
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

// ── Request models ───────────────────────────────────────────────────────────
// Risk is a free-form dictionary — any LOB-specific attributes are valid.

public record RatingRequest(
    string ProductCode,
    string CoverageCode,
    DateOnly EffectiveDate,
    IReadOnlyDictionary<string, string> Risk
);

public record PolicySegmentRequest(
    string ProductCode,
    DateOnly PolicyFrom,
    DateOnly PolicyTo,
    IReadOnlyList<PolicySegment> Segments
);

public record CoverageLevelSegmentRequest(
    string ProductCode,
    DateOnly PolicyFrom,
    DateOnly PolicyTo,
    IReadOnlyList<CoverageLevelInput> Coverages
);
