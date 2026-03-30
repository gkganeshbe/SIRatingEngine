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
            IRateTableAdminRepository repo) =>
            Results.Ok(await repo.ListAsync(coverageId)));

        // GET /admin/coverages/{coverageId}/rate-tables/{name}
        group.MapGet("/{name}", async (int coverageId, string name, IRateTableAdminRepository repo) =>
        {
            var result = await repo.GetAsync(coverageId, name);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        // POST /admin/coverages/{coverageId}/rate-tables
        // CoverageConfigId in the body must match the URL coverageId.
        group.MapPost("/", async (
            int coverageId,
            CreateRateTableRequest req,
            HttpContext ctx,
            IRateTableAdminRepository repo) =>
        {
            if (req.CoverageConfigId != coverageId)
                return Results.BadRequest(new { message = "CoverageConfigId in body must match URL coverageId" });

            var actor = ctx.Request.Headers.TryGetValue("X-User-Id", out var uid) ? uid.ToString() : null;
            var id = await repo.CreateAsync(req, actor);
            return Results.Created($"/admin/coverages/{coverageId}/rate-tables/{req.Name}", new { id });
        });

        // PUT /admin/coverages/{coverageId}/rate-tables/{id}
        group.MapPut("/{id:int}", async (
            int coverageId,
            int id,
            UpdateRateTableRequest req,
            HttpContext ctx,
            IRateTableAdminRepository repo) =>
        {
            var actor = ctx.Request.Headers.TryGetValue("X-User-Id", out var uid) ? uid.ToString() : null;
            return await repo.UpdateAsync(id, req, actor) ? Results.Ok() : Results.NotFound();
        });

        // DELETE /admin/coverages/{coverageId}/rate-tables/{id}
        group.MapDelete("/{id:int}", async (int coverageId, int id, IRateTableAdminRepository repo) =>
            await repo.DeleteAsync(id) ? Results.NoContent() : Results.NotFound());

        // ── Rows ──────────────────────────────────────────────────────────────

        // GET /admin/coverages/{coverageId}/rate-tables/{name}/rows?effectiveDate=2026-01-01
        group.MapGet("/{name}/rows", async (
            int coverageId,
            string name,
            DateOnly? effectiveDate,
            IRateTableAdminRepository repo) =>
            Results.Ok(await repo.GetRowsAsync(coverageId, name, effectiveDate)));

        // POST /admin/coverages/{coverageId}/rate-tables/{name}/rows
        group.MapPost("/{name}/rows", async (
            int coverageId,
            string name,
            CreateRateTableRowRequest req,
            IRateTableAdminRepository repo) =>
        {
            var id = await repo.AddRowAsync(coverageId, name, req);
            return Results.Created($"/admin/coverages/{coverageId}/rate-tables/{name}/rows/{id}", new { id });
        });

        // POST /admin/coverages/{coverageId}/rate-tables/{name}/rows/bulk
        group.MapPost("/{name}/rows/bulk", async (
            int coverageId,
            string name,
            BulkInsertRowsRequest req,
            IRateTableAdminRepository repo) =>
        {
            var count = await repo.BulkInsertRowsAsync(coverageId, name, req.Rows);
            return Results.Ok(new { inserted = count });
        });

        // PUT /admin/coverages/{coverageId}/rate-tables/{name}/rows/{rowId}
        group.MapPut("/{name}/rows/{rowId:long}", async (
            int coverageId, string name, long rowId,
            CreateRateTableRowRequest req,
            IRateTableAdminRepository repo) =>
            await repo.UpdateRowAsync(rowId, req) ? Results.Ok() : Results.NotFound());

        // POST /admin/coverages/{coverageId}/rate-tables/{name}/rows/{rowId}/expire
        group.MapPost("/{name}/rows/{rowId:long}/expire", async (
            int coverageId, string name, long rowId,
            ExpireRequest req,
            IRateTableAdminRepository repo) =>
            await repo.ExpireRowAsync(rowId, req.ExpireAt) ? Results.Ok() : Results.NotFound());

        // DELETE /admin/coverages/{coverageId}/rate-tables/{name}/rows/{rowId}
        group.MapDelete("/{name}/rows/{rowId:long}", async (
            int coverageId, string name, long rowId,
            IRateTableAdminRepository repo) =>
            await repo.DeleteRowAsync(rowId) ? Results.NoContent() : Results.NotFound());

        return app;
    }
}
