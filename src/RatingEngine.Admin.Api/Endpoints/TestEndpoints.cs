using RatingEngine.Core;
using RatingEngine.Data;
using RatingEngine.Data.Admin;

namespace RatingEngine.Admin.Api.Endpoints;

/// <summary>
/// Admin-only test endpoint that runs the rating pipeline for a specific coverage
/// and returns a full step trace. Used by the Testing Sandbox in the Admin UI.
/// </summary>
public static class TestEndpoints
{
    public static IEndpointRouteBuilder MapTestEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/admin/test/rate", async (
            AdminTestRateRequest req,
            ICoverageConfigRepository coverageRepo,
            IPipelineFactory factory,
            IRateLookupFactory lookupFactory,
            CancellationToken cancellationToken) =>
        {
            var effDate = req.RateEffectiveDate ?? DateOnly.FromDateTime(DateTime.Today);

            var coverage = await coverageRepo.GetAsync(
                req.ProductCode, req.State, req.CoverageCode, effDate, cancellationToken);

            if (coverage is null)
                return Results.NotFound(new
                {
                    message = $"No active coverage config found for {req.ProductCode}/{req.State}/{req.CoverageCode} on {effDate}"
                });

            var lookup   = lookupFactory.CreateForCoverage(coverage);
            var pipeline = factory.Build(coverage.Pipeline);
            var results  = new List<object>();
            decimal coverageTotal = 0m;

            var perils = req.Peril is not null ? new[] { req.Peril } : coverage.Perils.ToArray();

            foreach (var peril in perils)
            {
                var risk = new Dictionary<string, string>(
                    req.Risk ?? new Dictionary<string, string>(),
                    StringComparer.OrdinalIgnoreCase);

                var ctx = new RateContext(
                    req.ProductCode, coverage.Version, effDate,
                    req.State, risk, peril, req.StartingPremium ?? 0m);

                var (premium, trace) = new PipelineRunner(pipeline, lookup).Run(ctx);
                results.Add(new { peril, premium, trace });
                coverageTotal += premium;
            }

            return Results.Ok(new
            {
                productCode  = req.ProductCode,
                state        = req.State,
                coverageCode = req.CoverageCode,
                version      = coverage.Version,
                effDate,
                coveragePremium = coverageTotal,
                perils          = results
            });
        }).WithTags("Testing");

        return app;
    }
}

public record AdminTestRateRequest(
    string ProductCode,
    string State,
    string CoverageCode,
    DateOnly? RateEffectiveDate,
    /// <summary>If null, all perils in the coverage config are rated.</summary>
    string? Peril,
    decimal? StartingPremium,
    IReadOnlyDictionary<string, string>? Risk);
