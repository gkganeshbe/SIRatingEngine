using Dapper;

namespace RatingEngine.Data.Admin;

public sealed class SqlDerivedKeyAdminRepository : IDerivedKeyAdminRepository
{
    private readonly DbConnectionFactory _db;
    public SqlDerivedKeyAdminRepository(DbConnectionFactory db) => _db = db;

    public async Task<IReadOnlyList<DerivedKeyDetail>> ListAsync(int? productManifestId = null, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, ProductManifestId, Name, ReadableName, AggFunction, SourceField, Description
            FROM DerivedKey
            WHERE ProductManifestId IS NULL OR ProductManifestId = @ManifestId
            ORDER BY Name
            """;
        using var conn = _db.Create();
        return (await conn.QueryAsync<DerivedKeyDetail>(new CommandDefinition(
            sql,
            new { ManifestId = productManifestId },
            cancellationToken: cancellationToken))).AsList();
    }

    public async Task<DerivedKeyDetail?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, ProductManifestId, Name, ReadableName, AggFunction, SourceField, Description
            FROM DerivedKey WHERE Id = @Id
            """;
        using var conn = _db.Create();
        return await conn.QueryFirstOrDefaultAsync<DerivedKeyDetail>(new CommandDefinition(
            sql,
            new { Id = id },
            cancellationToken: cancellationToken));
    }

    public async Task<int> CreateAsync(int? productManifestId, CreateDerivedKeyRequest req, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO DerivedKey (ProductManifestId, Name, ReadableName, AggFunction, SourceField, Description)
            OUTPUT INSERTED.Id
            VALUES (@ManifestId, @Name, @ReadableName, @AggFunction, @SourceField, @Description)
            """;
        using var conn = _db.Create();
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            sql,
            new { ManifestId = productManifestId, req.Name, req.ReadableName, req.AggFunction, req.SourceField, req.Description },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> UpdateAsync(int id, UpdateDerivedKeyRequest req, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE DerivedKey
            SET ReadableName = @ReadableName, AggFunction = @AggFunction,
                SourceField = @SourceField, Description = @Description
            WHERE Id = @Id
            """;
        using var conn = _db.Create();
        return await conn.ExecuteAsync(new CommandDefinition(
            sql,
            new { Id = id, req.ReadableName, req.AggFunction, req.SourceField, req.Description },
            cancellationToken: cancellationToken)) > 0;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        using var conn = _db.Create();
        return await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM DerivedKey WHERE Id = @Id",
            new { Id = id },
            cancellationToken: cancellationToken)) > 0;
    }
}
