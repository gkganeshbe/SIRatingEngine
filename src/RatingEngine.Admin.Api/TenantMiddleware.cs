using RatingEngine.Core;

namespace RatingEngine.Admin.Api;

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

        // If the JWT contains a tenant_code claim, it must match the requested tenant.
        // Comparison is case-insensitive (token may store "QARATINGENGINE", header sends "QARatingEngine").
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var tokenTenantCode = context.User.Claims.FirstOrDefault(c => c.Type == "tenant_code")?.Value;
            if (!string.IsNullOrEmpty(tokenTenantCode) &&
                !string.Equals(tokenTenantCode, tenantId, StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { message = $"Forbidden: Token does not grant access to tenant '{tenantId}'." });
                return;
            }
        }

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
