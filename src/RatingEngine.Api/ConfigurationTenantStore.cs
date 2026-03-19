using RatingEngine.Core;

namespace RatingEngine.Api;

/// <summary>
/// Resolves tenant connection strings from the "ConnectionStrings" section of appsettings.json.
/// Each tenant ID maps to a named connection string entry.
/// Replace with a DB-backed implementation once tenant config moves to a database.
/// </summary>
public class ConfigurationTenantStore(IConfiguration configuration) : ITenantStore
{
    public string? GetConnectionString(string tenantId) =>
        configuration.GetConnectionString(tenantId);
}
