using Dapper;

namespace RatingEngine.Data.Admin;

public sealed class SqlProductStateAdminRepository : IProductStateAdminRepository
{
    private readonly DbConnectionFactory _db;
    public SqlProductStateAdminRepository(DbConnectionFactory db) => _db = db;

    public async Task<IReadOnlyList<ProductStateDetail>> ListAsync(int manifestId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, ProductManifestId, StateCode
            FROM ProductState
            WHERE ProductManifestId = @ManifestId
            ORDER BY StateCode
            """;

        using var conn = _db.Create();
        return (await conn.QueryAsync<ProductStateDetail>(new CommandDefinition(
            sql,
            new { ManifestId = manifestId },
            cancellationToken: cancellationToken))).AsList();
    }

    public async Task<int> AddAsync(int manifestId, AddProductStateRequest req, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO ProductState (ProductManifestId, StateCode)
            OUTPUT INSERTED.Id
            VALUES (@ManifestId, @StateCode)
            """;

        using var conn = _db.Create();
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            sql,
            new { ManifestId = manifestId, req.StateCode },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        using var conn = _db.Create();
        return await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM ProductState WHERE Id = @Id",
            new { Id = id },
            cancellationToken: cancellationToken)) > 0;
    }
}
