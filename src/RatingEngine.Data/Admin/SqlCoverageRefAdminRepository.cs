using Dapper;

namespace RatingEngine.Data.Admin;

public sealed class SqlCoverageRefAdminRepository : ICoverageRefAdminRepository
{
    private readonly DbConnectionFactory _db;
    public SqlCoverageRefAdminRepository(DbConnectionFactory db) => _db = db;

    public async Task<int> AddAsync(int productManifestId, AddCoverageRefRequest req, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO CoverageRef (ProductManifestId, LobId, CoverageCode, SortOrder)
            OUTPUT INSERTED.Id
            VALUES (@ProductManifestId, @LobId, @CoverageCode, @SortOrder)
            """;
        using var conn = _db.Create();
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            sql,
            new { ProductManifestId = productManifestId, req.LobId, req.CoverageCode, req.SortOrder },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        using var conn = _db.Create();
        return await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM CoverageRef WHERE Id = @Id",
            new { Id = id },
            cancellationToken: cancellationToken)) > 0;
    }
}
