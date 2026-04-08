using RatingEngine.Data.Admin;

namespace RatingEngine.Admin.Api.Endpoints;

public static class ColumnDefEndpoints
{
    public static IEndpointRouteBuilder MapColumnDefEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/rate-tables/{tableName}/column-defs").WithTags("Rate Table Column Defs");

        // GET /admin/rate-tables/{tableName}/column-defs
        group.MapGet("/", async (string tableName, IColumnDefAdminRepository repo, CancellationToken cancellationToken) =>
            Results.Ok(await repo.ListAsync(tableName, cancellationToken)));

        // PUT /admin/rate-tables/{tableName}/column-defs  (full replace)
        group.MapPut("/", async (
            string tableName,
            IReadOnlyList<ColumnDefRequest> req,
            IColumnDefAdminRepository repo,
            CancellationToken cancellationToken) =>
        {
            await repo.ReplaceAsync(tableName, req, cancellationToken);
            return Results.Ok();
        });

        // PUT /admin/rate-tables/{tableName}/column-defs/{id}  (single update)
        group.MapPut("/{id:int}", async (
            string tableName, int id,
            ColumnDefRequest req,
            IColumnDefAdminRepository repo,
            CancellationToken cancellationToken) =>
            await repo.UpdateAsync(id, req, cancellationToken) ? Results.Ok() : Results.NotFound());

        // DELETE /admin/rate-tables/{tableName}/column-defs/{id}
        group.MapDelete("/{id:int}", async (
            string tableName, int id,
            IColumnDefAdminRepository repo,
            CancellationToken cancellationToken) =>
            await repo.DeleteAsync(id, cancellationToken) ? Results.NoContent() : Results.NotFound());

        return app;
    }
}
