using RatingEngine.Core;

namespace RatingEngine.Api;

/// <summary>
/// Enforces the X-Tenant-Id header on every request.
/// Resolves the tenant's connection string and stores it in the scoped TenantContext
/// so downstream code can access it via ITenantContext.
/// </summary>
public class TenantMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Tenant-Id";

    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext, ITenantStore tenantStore)
    {
        if (!context.Request.Headers.TryGetValue(HeaderName, out var headerValue)
            || string.IsNullOrWhiteSpace(headerValue))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { message = $"Missing required header: {HeaderName}" });
            return;
        }

        var tenantId = headerValue.ToString();
        var connectionString = tenantStore.GetConnectionString(tenantId);
        var user = context.User;

        // Security Hardening: Ensure the authenticated user has claims for this tenant.
        if (user.Identity?.IsAuthenticated == true)
        {
            var allowedTenants = user.FindAll("tenant").Select(c => c.Value);
            if (!allowedTenants.Contains(tenantId) && !user.HasClaim("role", "system_admin"))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { message = $"Forbidden: Principal lacks access to tenant '{tenantId}'" });
                return;
            }
        }

        if (connectionString is null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { message = $"Unknown tenant: '{tenantId}'" });
            return;
        }

        tenantContext.TenantId = tenantId;
        tenantContext.ConnectionString = connectionString;

        await next(context);
    }
}
