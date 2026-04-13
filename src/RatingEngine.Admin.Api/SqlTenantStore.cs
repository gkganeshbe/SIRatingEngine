using Microsoft.Extensions.Caching.Memory;
using Npgsql;
using RatingEngine.Core;

namespace RatingEngine.Admin.Api;

public class SqlTenantStore(
    IConfiguration configuration,
    IMemoryCache cache,
    ILogger<SqlTenantStore> logger) : ITenantStore
{
    private readonly string _masterConnectionString = configuration.GetConnectionString("MasterDb")
        ?? throw new ArgumentNullException("MasterDb connection string is missing in appsettings.");

    public string? GetConnectionString(string tenantId)
    {
        string cacheKey = $"TenantConnStr_{tenantId}";

        return cache.GetOrCreate(cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);

            try
            {
                using var conn = new NpgsqlConnection(_masterConnectionString);
                conn.Open();

                // Diagnose: log how many rows exist for this TenantCode regardless of IsActive
                const string diagSql = """
                    SELECT COUNT(*), MAX("IsActive"::text)
                    FROM public."Tenants"
                    WHERE LOWER("TenantCode") = LOWER(@tenantId)
                    """;
                using (var diagCmd = new NpgsqlCommand(diagSql, conn))
                {
                    diagCmd.Parameters.AddWithValue("tenantId", tenantId);
                    using var reader = diagCmd.ExecuteReader();
                    if (reader.Read())
                        logger.LogInformation("Tenant lookup for '{TenantId}': {Count} row(s) found, IsActive={IsActive}",
                            tenantId, reader[0], reader[1]);
                }

                // Use case-insensitive match; treat NULL IsActive as active (legacy rows)
                const string sql = """
                    SELECT "ConnectionString"
                    FROM public."Tenants"
                    WHERE LOWER("TenantCode") = LOWER(@tenantId)
                      AND "IsActive" IS NOT FALSE
                    """;

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("tenantId", tenantId);

                var result = cmd.ExecuteScalar();

                if (result is null)
                    logger.LogWarning("Tenant '{TenantId}' not found or inactive in MasterDB", tenantId);
                else
                    logger.LogInformation("Tenant '{TenantId}' resolved successfully", tenantId);

                return result?.ToString();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to look up tenant '{TenantId}' from MasterDB", tenantId);
                return null;
            }
        });
    }
}