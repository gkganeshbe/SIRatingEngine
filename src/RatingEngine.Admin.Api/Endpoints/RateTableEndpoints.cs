using RatingEngine.Data.Admin;

namespace RatingEngine.Admin.Api.Endpoints;

public static class RateTableEndpoints
{
    public static IEndpointRouteBuilder MapRateTableEndpoints(this IEndpointRouteBuilder app)
    {
        // All rate table endpoints are scoped under a coverage config.
        // Rate table names only need to be unique within a coverage config.
        var group = app.MapGroup("/admin/coverages/{coverageId:int}/rate-tables").WithTags("Rate Tables");

        // GET /admin/coverages/{coverageId}/rate-tables
        group.MapGet("/", async (
            int coverageId,
            IRateTableAdminRepository repo,
            CancellationToken cancellationToken) =>
            Results.Ok(await repo.ListAsync(coverageId, cancellationToken)));

        // GET /admin/coverages/{coverageId}/rate-tables/{name}
        group.MapGet("/{name}", async (int coverageId, string name, IRateTableAdminRepository repo, CancellationToken cancellationToken) =>
        {
            var result = await repo.GetAsync(coverageId, name, cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        // POST /admin/coverages/{coverageId}/rate-tables
        // CoverageConfigId in the body must match the URL coverageId.
        group.MapPost("/", async (
            int coverageId,
            CreateRateTableRequest req,
            HttpContext ctx,
            IRateTableAdminRepository repo,
            CancellationToken cancellationToken) =>
        {
            if (req.CoverageConfigId != coverageId)
                return Results.BadRequest(new { message = "CoverageConfigId in body must match URL coverageId" });

            var actor = ctx.Request.Headers.TryGetValue("X-User-Id", out var uid) ? uid.ToString() : null;
            var id = await repo.CreateAsync(req, actor, cancellationToken);
            return Results.Created($"/admin/coverages/{coverageId}/rate-tables/{req.Name}", new { id });
        });

        // PUT /admin/coverages/{coverageId}/rate-tables/{id}
        group.MapPut("/{id:int}", async (
            int coverageId,
            int id,
            UpdateRateTableRequest req,
            HttpContext ctx,
            IRateTableAdminRepository repo,
            CancellationToken cancellationToken) =>
        {
            var actor = ctx.Request.Headers.TryGetValue("X-User-Id", out var uid) ? uid.ToString() : null;
            return await repo.UpdateAsync(id, req, actor, cancellationToken) ? Results.Ok() : Results.NotFound();
        });

        // DELETE /admin/coverages/{coverageId}/rate-tables/{id}
        group.MapDelete("/{id:int}", async (int coverageId, int id, IRateTableAdminRepository repo, CancellationToken cancellationToken) =>
            await repo.DeleteAsync(id, cancellationToken) ? Results.NoContent() : Results.NotFound());

        // ── Rows ──────────────────────────────────────────────────────────────

        // GET /admin/coverages/{coverageId}/rate-tables/{name}/rows?effectiveDate=2026-01-01
        group.MapGet("/{name}/rows", async (
            int coverageId,
            string name,
            DateOnly? effectiveDate,
            IRateTableAdminRepository repo,
            CancellationToken cancellationToken) =>
            Results.Ok(await repo.GetRowsAsync(coverageId, name, effectiveDate, cancellationToken)));

        // POST /admin/coverages/{coverageId}/rate-tables/{name}/rows
        group.MapPost("/{name}/rows", async (
            int coverageId,
            string name,
            CreateRateTableRowRequest req,
            IRateTableAdminRepository repo,
            CancellationToken cancellationToken) =>
        {
            var id = await repo.AddRowAsync(coverageId, name, req, cancellationToken);
            return Results.Created($"/admin/coverages/{coverageId}/rate-tables/{name}/rows/{id}", new { id });
        });

        // POST /admin/coverages/{coverageId}/rate-tables/{name}/rows/bulk
        group.MapPost("/{name}/rows/bulk", async (
            int coverageId,
            string name,
            BulkInsertRowsRequest req,
            IRateTableAdminRepository repo,
            CancellationToken cancellationToken) =>
        {
            var count = await repo.BulkInsertRowsAsync(coverageId, name, req.Rows, cancellationToken);
            return Results.Ok(new { inserted = count });
        });

        // PUT /admin/coverages/{coverageId}/rate-tables/{name}/rows/{rowId}
        group.MapPut("/{name}/rows/{rowId:long}", async (
            int coverageId, string name, long rowId,
            CreateRateTableRowRequest req,
            IRateTableAdminRepository repo,
            CancellationToken cancellationToken) =>
            await repo.UpdateRowAsync(coverageId, name, rowId, req, cancellationToken) ? Results.Ok() : Results.NotFound());

        // POST /admin/coverages/{coverageId}/rate-tables/{name}/rows/{rowId}/expire
        group.MapPost("/{name}/rows/{rowId:long}/expire", async (
            int coverageId, string name, long rowId,
            ExpireRequest req,
            IRateTableAdminRepository repo,
            CancellationToken cancellationToken) =>
            await repo.ExpireRowAsync(coverageId, name, rowId, req.ExpireAt, cancellationToken) ? Results.Ok() : Results.NotFound());

        // DELETE /admin/coverages/{coverageId}/rate-tables/{name}/rows/{rowId}
        group.MapDelete("/{name}/rows/{rowId:long}", async (
            int coverageId, string name, long rowId,
            IRateTableAdminRepository repo,
            CancellationToken cancellationToken) =>
            await repo.DeleteRowAsync(coverageId, name, rowId, cancellationToken) ? Results.NoContent() : Results.NotFound());

        return app;
    }
}
