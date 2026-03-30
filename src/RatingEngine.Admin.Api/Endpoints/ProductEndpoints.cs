using RatingEngine.Data.Admin;

namespace RatingEngine.Admin.Api.Endpoints;

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/products").WithTags("Products");

        // GET /admin/products
        group.MapGet("/", async (IProductAdminRepository repo) =>
            Results.Ok(await repo.ListAsync()));

        // GET /admin/products/{productCode}/{version}
        group.MapGet("/{productCode}/{version}", async (
            string productCode, string version,
            IProductAdminRepository repo) =>
        {
            var result = await repo.GetAsync(productCode, version);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        // POST /admin/products
        group.MapPost("/", async (
            CreateProductRequest req,
            HttpContext ctx,
            IProductAdminRepository repo) =>
        {
            var actor = ctx.Request.Headers.TryGetValue("X-User-Id", out var uid) ? uid.ToString() : null;
            var id = await repo.CreateAsync(req, actor);
            return Results.Created($"/admin/products/{req.ProductCode}/{req.Version}", new { id });
        });

        // PUT /admin/products/{id}
        group.MapPut("/{id:int}", async (
            int id,
            UpdateProductRequest req,
            HttpContext ctx,
            IProductAdminRepository repo) =>
        {
            var actor = ctx.Request.Headers.TryGetValue("X-User-Id", out var uid) ? uid.ToString() : null;
            return await repo.UpdateAsync(id, req, actor) ? Results.Ok() : Results.NotFound();
        });

        // POST /admin/products/{id}/expire
        group.MapPost("/{id:int}/expire", async (
            int id,
            ExpireRequest req,
            HttpContext ctx,
            IProductAdminRepository repo) =>
        {
            var actor = ctx.Request.Headers.TryGetValue("X-User-Id", out var uid) ? uid.ToString() : null;
            return await repo.ExpireAsync(id, req.ExpireAt, actor) ? Results.Ok() : Results.NotFound();
        });

        // DELETE /admin/products/{id}
        group.MapDelete("/{id:int}", async (int id, IProductAdminRepository repo) =>
            await repo.DeleteAsync(id) ? Results.NoContent() : Results.NotFound());

        return app;
    }
}
