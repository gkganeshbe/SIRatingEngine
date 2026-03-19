using System.Data;
using Microsoft.Data.SqlClient;
using RatingEngine.Core;

namespace RatingEngine.Data;

public sealed class DbConnectionFactory
{
    private readonly ITenantContext _tenant;
    public DbConnectionFactory(ITenantContext tenant) => _tenant = tenant;

    public IDbConnection Create()
    {
        if (string.IsNullOrWhiteSpace(_tenant.ConnectionString))
            throw new InvalidOperationException("No connection string configured for the current tenant.");
        return new SqlConnection(_tenant.ConnectionString);
    }
}
