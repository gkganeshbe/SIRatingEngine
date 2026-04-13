using RatingEngine.Admin.Api.Services;
using RatingEngine.Core;

namespace RatingEngine.Admin.Api.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/users").WithTags("Users");

        // GET /admin/users
        // Lists all users that belong to the current tenant.
        group.MapGet("/", async (
            ITenantContext tenant,
            UserManagementService svc,
            CancellationToken ct) =>
        {
            var users = await svc.ListAsync(tenant.TenantId, ct);
            return Results.Ok(users);
        });

        // POST /admin/users
        // Creates a new user in the identity server, scoped to the current tenant.
        group.MapPost("/", async (
            CreateUserRequest req,
            ITenantContext tenant,
            UserManagementService svc,
            CancellationToken ct) =>
        {
            var created = await svc.CreateAsync(req, tenant.TenantId, ct);
            return Results.Created($"/admin/users/{created.Id}", created);
        });

        // DELETE /admin/users/{userId}
        // Removes a user from the identity server. Scoped to the current tenant
        // so that a tenant admin cannot delete users from other tenants.
        group.MapDelete("/{userId}", async (
            string userId,
            ITenantContext tenant,
            UserManagementService svc,
            CancellationToken ct) =>
        {
            await svc.DeleteAsync(userId, tenant.TenantId, ct);
            return Results.NoContent();
        });

        // POST /admin/users/{userId}/reset-password
        // Lets a tenant admin set a new password for one of their users.
        group.MapPost("/{userId}/reset-password", async (
            string userId,
            ResetPasswordRequest req,
            ITenantContext tenant,
            UserManagementService svc,
            CancellationToken ct) =>
        {
            await svc.ResetPasswordAsync(userId, req, tenant.TenantId, ct);
            return Results.Ok();
        });

        return app;
    }
}
