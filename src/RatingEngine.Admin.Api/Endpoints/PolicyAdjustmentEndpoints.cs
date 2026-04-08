using RatingEngine.Data.Admin;

namespace RatingEngine.Admin.Api.Endpoints;

public static class PolicyAdjustmentEndpoints
{
    public static IEndpointRouteBuilder MapPolicyAdjustmentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/products/{manifestId:int}/adjustments").WithTags("PolicyAdjustments");

        // GET  /admin/products/{manifestId}/adjustments
        group.MapGet("/", async (int manifestId, IPolicyAdjustmentAdminRepository repo, CancellationToken cancellationToken) =>
            Results.Ok(await repo.ListAsync(manifestId, cancellationToken)));

        // POST /admin/products/{manifestId}/adjustments
        group.MapPost("/", async (
            int manifestId,
            CreatePolicyAdjustmentRequest req,
            HttpContext ctx,
            IPolicyAdjustmentAdminRepository repo,
            CancellationToken cancellationToken) =>
        {
            var actor = ctx.Request.Headers.TryGetValue("X-User-Id", out var uid) ? uid.ToString() : null;
            var id    = await repo.CreateAsync(manifestId, req, actor, cancellationToken);
            return Results.Created($"/admin/adjustments/{id}", new { id });
        });

        // PUT /admin/adjustments/{id}
        app.MapPut("/admin/adjustments/{id:int}", async (
            int id,
            UpdatePolicyAdjustmentRequest req,
            HttpContext ctx,
            IPolicyAdjustmentAdminRepository repo,
            CancellationToken cancellationToken) =>
        {
            var actor = ctx.Request.Headers.TryGetValue("X-User-Id", out var uid) ? uid.ToString() : null;
            return await repo.UpdateAsync(id, req, actor, cancellationToken) ? Results.Ok() : Results.NotFound();
        }).WithTags("PolicyAdjustments");

        // DELETE /admin/adjustments/{id}
        app.MapDelete("/admin/adjustments/{id:int}", async (
            int id,
            IPolicyAdjustmentAdminRepository repo,
            CancellationToken cancellationToken) =>
            await repo.DeleteAsync(id, cancellationToken) ? Results.NoContent() : Results.NotFound())
           .WithTags("PolicyAdjustments");

        return app;
    }
}
