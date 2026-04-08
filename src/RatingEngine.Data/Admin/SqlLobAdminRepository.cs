using Dapper;

namespace RatingEngine.Data.Admin;

public sealed class SqlLobAdminRepository : ILobAdminRepository
{
    private readonly DbConnectionFactory _db;
    public SqlLobAdminRepository(DbConnectionFactory db) => _db = db;

    public async Task<int> AddAsync(int productManifestId, AddLobRequest req, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO ProductLob (ProductManifestId, LobCode, SortOrder)
            OUTPUT INSERTED.Id
            VALUES (@ProductManifestId, @LobCode, @SortOrder)
            """;
        using var conn = _db.Create();
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            sql,
            new { ProductManifestId = productManifestId, req.LobCode, req.SortOrder },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        using var conn = _db.Create();
        return await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM ProductLob WHERE Id = @Id",
            new { Id = id },
            cancellationToken: cancellationToken)) > 0;
    }
}
