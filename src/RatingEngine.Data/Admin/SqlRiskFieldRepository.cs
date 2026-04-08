using Dapper;

namespace RatingEngine.Data.Admin;

public sealed class SqlRiskFieldRepository : IRiskFieldRepository
{
    private readonly DbConnectionFactory _db;
    public SqlRiskFieldRepository(DbConnectionFactory db) => _db = db;

    public async Task<IReadOnlyList<RiskField>> ListAsync(string? productCode = null, CancellationToken cancellationToken = default)
    {
        // Returns global fields (ProductCode IS NULL) plus any fields specific to the given product.
        const string sql = """
            SELECT Id, DisplayName, Path, Description, Category, SortOrder, ProductCode
            FROM RiskField
            WHERE ProductCode IS NULL
               OR ProductCode = @productCode
            ORDER BY SortOrder, DisplayName
            """;
        using var conn = _db.Create();
        return (await conn.QueryAsync<RiskField>(new CommandDefinition(
            sql,
            new { productCode },
            cancellationToken: cancellationToken))).AsList();
    }

    public async Task<int> CreateAsync(CreateRiskFieldRequest req, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO RiskField (DisplayName, Path, Description, Category, SortOrder, ProductCode)
            OUTPUT INSERTED.Id
            VALUES (@DisplayName, @Path, @Description, @Category, @SortOrder, @ProductCode)
            """;
        using var conn = _db.Create();
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            sql,
            req,
            cancellationToken: cancellationToken));
    }

    public async Task<bool> UpdateAsync(int id, UpdateRiskFieldRequest req, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE RiskField
            SET DisplayName = @DisplayName,
                Path        = @Path,
                Description = @Description,
                Category    = @Category,
                SortOrder   = @SortOrder,
                ProductCode = @ProductCode
            WHERE Id = @Id
            """;
        using var conn = _db.Create();
        return await conn.ExecuteAsync(new CommandDefinition(
            sql,
            new { Id = id, req.DisplayName, req.Path, req.Description, req.Category, req.SortOrder, req.ProductCode },
            cancellationToken: cancellationToken)) > 0;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        using var conn = _db.Create();
        return await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM RiskField WHERE Id = @Id",
            new { Id = id },
            cancellationToken: cancellationToken)) > 0;
    }
}
