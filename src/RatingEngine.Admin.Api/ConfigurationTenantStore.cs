using RatingEngine.Core;

namespace RatingEngine.Admin.Api;

public class ConfigurationTenantStore(IConfiguration configuration) : ITenantStore
{
    public string? GetConnectionString(string tenantId) =>
        configuration.GetConnectionString(tenantId);
}
