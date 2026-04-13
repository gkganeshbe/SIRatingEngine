using Dapper;

namespace RatingEngine.Data.Admin;

public sealed class PgSqlLobAdminRepository : ILobAdminRepository
{
    private readonly DbConnectionFactory _db;
    public PgSqlLobAdminRepository(DbConnectionFactory db) => _db = db;

    public async Task<int> AddAsync(int productManifestId, AddLobRequest req, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO "ProductLob" ("ProductId", "LobCode", "SortOrder")
            VALUES (@ProductManifestId, @LobCode, @SortOrder)
            RETURNING "Id"
            """;
        using var conn = _db.Create();
        return await conn.QuerySingleAsync<int>(new CommandDefinition(
            sql,
            new { ProductManifestId = productManifestId, req.LobCode, req.SortOrder },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        using var conn = _db.Create();
        return await conn.ExecuteAsync(new CommandDefinition(
            """DELETE FROM "ProductLob" WHERE "Id" = @Id""",
            new { Id = id },
            cancellationToken: cancellationToken)) > 0;
    }
}
