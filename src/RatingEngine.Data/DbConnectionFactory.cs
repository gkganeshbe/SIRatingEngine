using System.Data;
using Microsoft.Data.SqlClient;
using Npgsql;
using RatingEngine.Core;

namespace RatingEngine.Data;

/// <summary>
/// Creates an open-able <see cref="IDbConnection"/> for the current tenant and exposes
/// the detected <see cref="DatabaseProvider"/> so repository factory delegates can
/// choose the correct dialect implementation at runtime.
/// </summary>
public sealed class DbConnectionFactory
{
    private readonly ITenantContext _tenant;
    public DbConnectionFactory(ITenantContext tenant) => _tenant = tenant;

    /// <summary>
    /// The dialect detected from the tenant connection string.
    /// Detection rule: contains "Host=" (case-insensitive) → PostgreSQL; otherwise SQL Server.
    /// </summary>
    public DatabaseProvider Provider => Detect(_tenant.ConnectionString);

    /// <summary>Returns a new, unopened connection for the current tenant.</summary>
    public IDbConnection Create()
    {
        var cs = _tenant.ConnectionString;
        if (string.IsNullOrWhiteSpace(cs))
            throw new InvalidOperationException("No connection string configured for the current tenant.");

        return Detect(cs) == DatabaseProvider.PostgreSql
            ? new NpgsqlConnection(cs)
            : new SqlConnection(cs);
    }

    private static DatabaseProvider Detect(string? cs) =>
        cs is not null && cs.Contains("Host=", StringComparison.OrdinalIgnoreCase)
            ? DatabaseProvider.PostgreSql
            : DatabaseProvider.SqlServer;
}
