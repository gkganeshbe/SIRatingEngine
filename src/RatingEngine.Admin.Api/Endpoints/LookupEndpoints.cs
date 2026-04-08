using RatingEngine.Data.Admin;

namespace RatingEngine.Admin.Api.Endpoints;

public static class LookupEndpoints
{
    public static IEndpointRouteBuilder MapLookupEndpoints(this IEndpointRouteBuilder app)
    {
        // ── Lookup Dimensions ─────────────────────────────────────────────────

        var dims = app.MapGroup("/admin/lookup-dimensions").WithTags("Lookups");

        // GET /admin/lookup-dimensions?productManifestId=123
        dims.MapGet("/", async (int? productManifestId, ILookupDimensionAdminRepository repo, CancellationToken cancellationToken) =>
            Results.Ok(await repo.ListAsync(productManifestId, cancellationToken)));

        // GET /admin/lookup-dimensions/{id}
        dims.MapGet("/{id:int}", async (int id, ILookupDimensionAdminRepository repo, CancellationToken cancellationToken) =>
        {
            var dim = await repo.GetAsync(id, cancellationToken);
            return dim is null ? Results.NotFound() : Results.Ok(dim);
        });

        // POST /admin/lookup-dimensions?productManifestId=123
        dims.MapPost("/", async (int? productManifestId, CreateLookupDimensionRequest req,
            ILookupDimensionAdminRepository repo, CancellationToken cancellationToken) =>
        {
            var id = await repo.CreateAsync(productManifestId, req, cancellationToken);
            return Results.Created($"/admin/lookup-dimensions/{id}", new { Id = id });
        });

        // PUT /admin/lookup-dimensions/{id}
        dims.MapPut("/{id:int}", async (int id, UpdateLookupDimensionRequest req,
            ILookupDimensionAdminRepository repo, CancellationToken cancellationToken) =>
        {
            var updated = await repo.UpdateAsync(id, req, cancellationToken);
            return updated ? Results.NoContent() : Results.NotFound();
        });

        // DELETE /admin/lookup-dimensions/{id}
        dims.MapDelete("/{id:int}", async (int id, ILookupDimensionAdminRepository repo, CancellationToken cancellationToken) =>
        {
            var deleted = await repo.DeleteAsync(id, cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        // POST /admin/lookup-dimensions/{id}/values
        dims.MapPost("/{id:int}/values", async (int id, CreateLookupValueRequest req,
            ILookupDimensionAdminRepository repo, CancellationToken cancellationToken) =>
        {
            var valueId = await repo.AddValueAsync(id, req, cancellationToken);
            return Results.Created($"/admin/lookup-dimensions/{id}/values/{valueId}", new { Id = valueId });
        });

        // DELETE /admin/lookup-dimension-values/{valueId}
        app.MapDelete("/admin/lookup-dimension-values/{valueId:int}",
            async (int valueId, ILookupDimensionAdminRepository repo, CancellationToken cancellationToken) =>
            {
                var deleted = await repo.DeleteValueAsync(valueId, cancellationToken);
                return deleted ? Results.NoContent() : Results.NotFound();
            }).WithTags("Lookups");

        // ── Derived Keys ──────────────────────────────────────────────────────

        var keys = app.MapGroup("/admin/derived-keys").WithTags("Lookups");

        // GET /admin/derived-keys?productManifestId=123
        keys.MapGet("/", async (int? productManifestId, IDerivedKeyAdminRepository repo, CancellationToken cancellationToken) =>
            Results.Ok(await repo.ListAsync(productManifestId, cancellationToken)));

        // GET /admin/derived-keys/{id}
        keys.MapGet("/{id:int}", async (int id, IDerivedKeyAdminRepository repo, CancellationToken cancellationToken) =>
        {
            var key = await repo.GetAsync(id, cancellationToken);
            return key is null ? Results.NotFound() : Results.Ok(key);
        });

        // POST /admin/derived-keys?productManifestId=123
        keys.MapPost("/", async (int? productManifestId, CreateDerivedKeyRequest req,
            IDerivedKeyAdminRepository repo, CancellationToken cancellationToken) =>
        {
            var id = await repo.CreateAsync(productManifestId, req, cancellationToken);
            return Results.Created($"/admin/derived-keys/{id}", new { Id = id });
        });

        // PUT /admin/derived-keys/{id}
        keys.MapPut("/{id:int}", async (int id, UpdateDerivedKeyRequest req,
            IDerivedKeyAdminRepository repo, CancellationToken cancellationToken) =>
        {
            var updated = await repo.UpdateAsync(id, req, cancellationToken);
            return updated ? Results.NoContent() : Results.NotFound();
        });

        // DELETE /admin/derived-keys/{id}
        keys.MapDelete("/{id:int}", async (int id, IDerivedKeyAdminRepository repo, CancellationToken cancellationToken) =>
        {
            var deleted = await repo.DeleteAsync(id, cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        return app;
    }
}
