using RatingEngine.Data.Admin;

namespace RatingEngine.Admin.Api.Endpoints;

public static class CoverageEndpoints
{
    public static IEndpointRouteBuilder MapCoverageEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/coverages").WithTags("Coverages");

        // GET /admin/coverages?coverageRefId={id}   — list state configs for one catalog entry
        // GET /admin/coverages?productManifestId={id} — all state configs for a product
        group.MapGet("/", async (
            int? coverageRefId,
            int? productManifestId,
            ICoverageAdminRepository repo,
            CancellationToken cancellationToken) =>
            Results.Ok(await repo.ListAsync(coverageRefId, productManifestId, cancellationToken)));

        // GET /admin/coverages/{id}  — get single state config (pipeline + rate tables)
        group.MapGet("/{id:int}", async (
            int id,
            ICoverageAdminRepository repo,
            CancellationToken cancellationToken) =>
        {
            var result = await repo.GetAsync(id, cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        // POST /admin/coverages  — create state config for a catalog entry
        group.MapPost("/", async (
            CreateCoverageRequest req,
            HttpContext ctx,
            ICoverageAdminRepository repo,
            CancellationToken cancellationToken) =>
        {
            var actor = ctx.Request.Headers.TryGetValue("X-User-Id", out var uid) ? uid.ToString() : null;
            var id    = await repo.CreateAsync(req, actor, cancellationToken);
            return Results.Created($"/admin/coverages/{id}", new { id });
        });

        // PUT /admin/coverages/{id}
        group.MapPut("/{id:int}", async (
            int id,
            UpdateCoverageRequest req,
            HttpContext ctx,
            ICoverageAdminRepository repo,
            CancellationToken cancellationToken) =>
        {
            var actor = ctx.Request.Headers.TryGetValue("X-User-Id", out var uid) ? uid.ToString() : null;
            return await repo.UpdateAsync(id, req, actor, cancellationToken) ? Results.Ok() : Results.NotFound();
        });

        // POST /admin/coverages/{id}/expire
        group.MapPost("/{id:int}/expire", async (
            int id,
            ExpireRequest req,
            HttpContext ctx,
            ICoverageAdminRepository repo,
            CancellationToken cancellationToken) =>
        {
            var actor = ctx.Request.Headers.TryGetValue("X-User-Id", out var uid) ? uid.ToString() : null;
            return await repo.ExpireAsync(id, req.ExpireAt, actor, cancellationToken) ? Results.Ok() : Results.NotFound();
        });

        // DELETE /admin/coverages/{id}
        group.MapDelete("/{id:int}", async (int id, ICoverageAdminRepository repo, CancellationToken cancellationToken) =>
            await repo.DeleteAsync(id, cancellationToken) ? Results.NoContent() : Results.NotFound());

        return app;
    }
}
