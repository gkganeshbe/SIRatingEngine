using RatingEngine.Data.Admin;

namespace RatingEngine.Admin.Api.Endpoints;

public static class RiskFieldEndpoints
{
    public static IEndpointRouteBuilder MapRiskFieldEndpoints(this IEndpointRouteBuilder app)
    {
        // Product-scoped list + create
        // GET  /admin/products/{productCode}/risk-fields  → global + product fields
        // POST /admin/products/{productCode}/risk-fields  → create field scoped to product
        var productGroup = app.MapGroup("/admin/products/{productCode}/risk-fields").WithTags("RiskFields");

        productGroup.MapGet("/", async (string productCode, IRiskFieldRepository repo, CancellationToken cancellationToken) =>
            Results.Ok(await repo.ListAsync(productCode, cancellationToken)));

        productGroup.MapPost("/", async (
            string productCode,
            CreateRiskFieldRequest req,
            IRiskFieldRepository repo,
            CancellationToken cancellationToken) =>
        {
            // Bind the productCode from the route into the request so the stored row is scoped correctly.
            var scoped = req with { ProductCode = productCode };
            var id = await repo.CreateAsync(scoped, cancellationToken);
            return Results.Created($"/admin/products/{productCode}/risk-fields/{id}", new { id });
        });

        // Global list (no product filter — returns only system/global fields where ProductCode IS NULL)
        // Useful for admin super-user views.
        app.MapGet("/admin/risk-fields", async (IRiskFieldRepository repo, CancellationToken cancellationToken) =>
            Results.Ok(await repo.ListAsync(null, cancellationToken))).WithTags("RiskFields");

        // Individual field update / delete — not product-scoped in the URL because the record
        // already carries its own ProductCode; the caller identifies by Id.
        var fieldGroup = app.MapGroup("/admin/risk-fields").WithTags("RiskFields");

        fieldGroup.MapPut("/{id:int}", async (
            int id,
            UpdateRiskFieldRequest req,
            IRiskFieldRepository repo,
            CancellationToken cancellationToken) =>
            await repo.UpdateAsync(id, req, cancellationToken) ? Results.Ok() : Results.NotFound());

        fieldGroup.MapDelete("/{id:int}", async (int id, IRiskFieldRepository repo, CancellationToken cancellationToken) =>
            await repo.DeleteAsync(id, cancellationToken) ? Results.NoContent() : Results.NotFound());

        return app;
    }
}
