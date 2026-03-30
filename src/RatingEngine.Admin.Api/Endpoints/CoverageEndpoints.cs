using RatingEngine.Data.Admin;

namespace RatingEngine.Admin.Api.Endpoints;

public static class CoverageEndpoints
{
    public static IEndpointRouteBuilder MapCoverageEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/coverages").WithTags("Coverages");

        // GET /admin/coverages?productCode=
        group.MapGet("/", async (
            string? productCode,
            ICoverageAdminRepository repo) =>
            Results.Ok(await repo.ListAsync(productCode)));

        // GET /admin/coverages/{productCode}/{coverageCode}/{version}
        group.MapGet("/{productCode}/{coverageCode}/{version}", async (
            string productCode, string coverageCode, string version,
            ICoverageAdminRepository repo) =>
        {
            var result = await repo.GetAsync(productCode, coverageCode, version);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        // POST /admin/coverages
        group.MapPost("/", async (
            CreateCoverageRequest req,
            HttpContext ctx,
            ICoverageAdminRepository repo) =>
        {
            var actor = ctx.Request.Headers.TryGetValue("X-User-Id", out var uid) ? uid.ToString() : null;
            var id = await repo.CreateAsync(req, actor);
            return Results.Created(
                $"/admin/coverages/{req.ProductCode}/{req.CoverageCode}/{req.Version}",
                new { id });
        });

        // PUT /admin/coverages/{id}
        group.MapPut("/{id:int}", async (
            int id,
            UpdateCoverageRequest req,
            HttpContext ctx,
            ICoverageAdminRepository repo) =>
        {
            var actor = ctx.Request.Headers.TryGetValue("X-User-Id", out var uid) ? uid.ToString() : null;
            return await repo.UpdateAsync(id, req, actor) ? Results.Ok() : Results.NotFound();
        });

        // POST /admin/coverages/{id}/expire
        group.MapPost("/{id:int}/expire", async (
            int id,
            ExpireRequest req,
            HttpContext ctx,
            ICoverageAdminRepository repo) =>
        {
            var actor = ctx.Request.Headers.TryGetValue("X-User-Id", out var uid) ? uid.ToString() : null;
            return await repo.ExpireAsync(id, req.ExpireAt, actor) ? Results.Ok() : Results.NotFound();
        });

        // DELETE /admin/coverages/{id}
        group.MapDelete("/{id:int}", async (int id, ICoverageAdminRepository repo) =>
            await repo.DeleteAsync(id) ? Results.NoContent() : Results.NotFound());

        return app;
    }
}
