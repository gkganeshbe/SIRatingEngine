using RatingEngine.Data.Admin;

namespace RatingEngine.Admin.Api.Endpoints;


public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/products").WithTags("Products");

        // GET /admin/products
        group.MapGet("/", async (IProductAdminRepository repo, CancellationToken cancellationToken) =>
            Results.Ok(await repo.ListAsync(cancellationToken)));

        // GET /admin/products/{productCode}/{version}
        group.MapGet("/{productCode}/{version}", async (
            string productCode, string version,
            IProductAdminRepository repo,
            CancellationToken cancellationToken) =>
        {
            var result = await repo.GetAsync(productCode, version, cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        // POST /admin/products
        group.MapPost("/", async (
            CreateProductRequest req,
            HttpContext ctx,
            IProductAdminRepository repo,
            CancellationToken cancellationToken) =>
        {
            var actor = ctx.Request.Headers.TryGetValue("X-User-Id", out var uid) ? uid.ToString() : null;
            var id = await repo.CreateAsync(req, actor, cancellationToken);
            return Results.Created($"/admin/products/{req.ProductCode}/{req.Version}", new { id });
        });

        // PUT /admin/products/{id}
        group.MapPut("/{id:int}", async (
            int id,
            UpdateProductRequest req,
            HttpContext ctx,
            IProductAdminRepository repo,
            CancellationToken cancellationToken) =>
        {
            var actor = ctx.Request.Headers.TryGetValue("X-User-Id", out var uid) ? uid.ToString() : null;
            return await repo.UpdateAsync(id, req, actor, cancellationToken) ? Results.Ok() : Results.NotFound();
        });

        // POST /admin/products/{id}/expire
        group.MapPost("/{id:int}/expire", async (
            int id,
            ExpireRequest req,
            HttpContext ctx,
            IProductAdminRepository repo,
            CancellationToken cancellationToken) =>
        {
            var actor = ctx.Request.Headers.TryGetValue("X-User-Id", out var uid) ? uid.ToString() : null;
            return await repo.ExpireAsync(id, req.ExpireAt, actor, cancellationToken) ? Results.Ok() : Results.NotFound();
        });

        // DELETE /admin/products/{id}
        group.MapDelete("/{id:int}", async (int id, IProductAdminRepository repo, CancellationToken cancellationToken) =>
            await repo.DeleteAsync(id, cancellationToken) ? Results.NoContent() : Results.NotFound());

        // POST /admin/products/{manifestId:int}/catalog  — add coverage to product catalog
        group.MapPost("/{manifestId:int}/catalog", async (
            int manifestId,
            AddCoverageRefRequest req,
            ICoverageRefAdminRepository repo,
            CancellationToken cancellationToken) =>
        {
            var id = await repo.AddAsync(manifestId, req, cancellationToken);
            return Results.Created($"/admin/catalog/{id}", new { id });
        });

        // DELETE /admin/catalog/{id}  — remove coverage from product catalog (cascades configs)
        app.MapDelete("/admin/catalog/{id:int}", async (
            int id,
            ICoverageRefAdminRepository repo,
            CancellationToken cancellationToken) =>
            await repo.DeleteAsync(id, cancellationToken) ? Results.NoContent() : Results.NotFound())
           .WithTags("Products");

        // POST /admin/products/{manifestId:int}/lobs  — add a LOB to a commercial product
        group.MapPost("/{manifestId:int}/lobs", async (
            int manifestId,
            AddLobRequest req,
            ILobAdminRepository repo,
            CancellationToken cancellationToken) =>
        {
            var id = await repo.AddAsync(manifestId, req, cancellationToken);
            return Results.Created($"/admin/lobs/{id}", new { id });
        });

        // DELETE /admin/lobs/{id}  — remove a LOB (cascades to coverages and all state configs)
        app.MapDelete("/admin/lobs/{id:int}", async (
            int id,
            ILobAdminRepository repo,
            CancellationToken cancellationToken) =>
            await repo.DeleteAsync(id, cancellationToken) ? Results.NoContent() : Results.NotFound())
           .WithTags("Products");

        // GET  /admin/products/{manifestId}/states  — list states this product is filed in
        group.MapGet("/{manifestId:int}/states", async (
            int manifestId,
            IProductStateAdminRepository repo,
            CancellationToken cancellationToken) =>
            Results.Ok(await repo.ListAsync(manifestId, cancellationToken)));

        // POST /admin/products/{manifestId}/states  — add a state declaration
        group.MapPost("/{manifestId:int}/states", async (
            int manifestId,
            AddProductStateRequest req,
            IProductStateAdminRepository repo,
            CancellationToken cancellationToken) =>
        {
            var id = await repo.AddAsync(manifestId, req, cancellationToken);
            return Results.Created($"/admin/states/{id}", new { id });
        });

        // DELETE /admin/states/{id}  — remove a state declaration
        app.MapDelete("/admin/states/{id:int}", async (
            int id,
            IProductStateAdminRepository repo,
            CancellationToken cancellationToken) =>
            await repo.DeleteAsync(id, cancellationToken) ? Results.NoContent() : Results.NotFound())
           .WithTags("Products");

        // GET  /admin/lobs/{lobId}/scopes  — list permitted aggregation scopes for a LOB
        app.MapGet("/admin/lobs/{lobId:int}/scopes", async (
            int lobId,
            ILobScopeAdminRepository repo,
            CancellationToken cancellationToken) =>
            Results.Ok(await repo.ListAsync(lobId, cancellationToken)))
           .WithTags("Products");

        // POST /admin/lobs/{lobId}/scopes  — add a permitted scope
        app.MapPost("/admin/lobs/{lobId:int}/scopes", async (
            int lobId,
            AddLobScopeRequest req,
            ILobScopeAdminRepository repo,
            CancellationToken cancellationToken) =>
        {
            var id = await repo.AddAsync(lobId, req, cancellationToken);
            return Results.Created($"/admin/scopes/{id}", new { id });
        }).WithTags("Products");

        // DELETE /admin/scopes/{id}  — remove a permitted scope
        app.MapDelete("/admin/scopes/{id:int}", async (
            int id,
            ILobScopeAdminRepository repo,
            CancellationToken cancellationToken) =>
            await repo.DeleteAsync(id, cancellationToken) ? Results.NoContent() : Results.NotFound())
           .WithTags("Products");

        // PATCH /admin/catalog/{coverageRefId}/aggregation  — set AggregationRule + PerilRollup
        app.MapPatch("/admin/catalog/{coverageRefId:int}/aggregation", async (
            int coverageRefId,
            UpdateCoverageRefRequest req,
            ILobScopeAdminRepository repo,
            CancellationToken cancellationToken) =>
            await repo.UpdateCoverageRefAsync(coverageRefId, req, cancellationToken) ? Results.Ok() : Results.NotFound())
           .WithTags("Products");

        return app;
    }
}
