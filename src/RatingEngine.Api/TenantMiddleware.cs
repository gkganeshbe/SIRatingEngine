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
