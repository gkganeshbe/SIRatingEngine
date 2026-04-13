using Dapper;

namespace RatingEngine.Data.Admin;

public sealed class PgSqlCoverageRefAdminRepository : ICoverageRefAdminRepository
{
    private readonly DbConnectionFactory _db;
    public PgSqlCoverageRefAdminRepository(DbConnectionFactory db) => _db = db;

    public async Task<int> AddAsync(int productManifestId, AddCoverageRefRequest req, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO "CoverageRef" ("ProductId", "LobId", "CoverageCode", "SortOrder")
            VALUES (@ProductManifestId, @LobId, @CoverageCode, @SortOrder)
            RETURNING "Id"
            """;
        using var conn = _db.Create();
        return await conn.QuerySingleAsync<int>(new CommandDefinition(
            sql,
            new { ProductManifestId = productManifestId, req.LobId, req.CoverageCode, req.SortOrder },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        using var conn = _db.Create();
        return await conn.ExecuteAsync(new CommandDefinition(
            """DELETE FROM "CoverageRef" WHERE "Id" = @Id""",
            new { Id = id },
            cancellationToken: cancellationToken)) > 0;
    }
}
