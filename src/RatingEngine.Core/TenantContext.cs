namespace RatingEngine.Core;

/// <summary>
/// Read-only view of the resolved tenant for the current request.
/// Inject this into repositories and services that need the tenant-specific DB connection.
/// </summary>
public interface ITenantContext
{
    string TenantId { get; }
    string ConnectionString { get; }
}

/// <summary>
/// Resolves a tenant ID to its database connection string.
/// Swap this implementation when tenant config moves from appsettings to a database.
/// </summary>
public interface ITenantStore
{
    /// <returns>Connection string, or <c>null</c> if the tenant is not registered.</returns>
    string? GetConnectionString(string tenantId);
}
