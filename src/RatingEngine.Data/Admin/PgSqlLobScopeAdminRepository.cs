using Dapper;

namespace RatingEngine.Data.Admin;

public sealed class PgSqlLobScopeAdminRepository : ILobScopeAdminRepository
{
    private readonly DbConnectionFactory _db;
    public PgSqlLobScopeAdminRepository(DbConnectionFactory db) => _db = db;

    public async Task<IReadOnlyList<LobScopeDetail>> ListAsync(int lobId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT "Id", "LobId", "Scope" FROM "LobAggregationScope"
            WHERE "LobId" = @LobId ORDER BY "Scope"
            """;
        using var conn = _db.Create();
        return (await conn.QueryAsync<LobScopeDetail>(new CommandDefinition(
            sql,
            new { LobId = lobId },
            cancellationToken: cancellationToken))).AsList();
    }

    public async Task<int> AddAsync(int lobId, AddLobScopeRequest req, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO "LobAggregationScope" ("LobId", "Scope") VALUES (@LobId, @Scope) RETURNING "Id"
            """;
        using var conn = _db.Create();
        return await conn.QuerySingleAsync<int>(new CommandDefinition(
            sql,
            new { LobId = lobId, req.Scope },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        using var conn = _db.Create();
        return await conn.ExecuteAsync(new CommandDefinition(
            """DELETE FROM "LobAggregationScope" WHERE "Id" = @Id""",
            new { Id = id },
            cancellationToken: cancellationToken)) > 0;
    }

    public async Task<bool> UpdateCoverageRefAsync(int coverageRefId, UpdateCoverageRefRequest req, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE "CoverageRef" SET "AggregationRule" = @AggregationRule, "PerilRollup" = @PerilRollup
            WHERE "Id" = @Id
            """;
        using var conn = _db.Create();
        return await conn.ExecuteAsync(new CommandDefinition(
            sql,
            new { Id = coverageRefId, req.AggregationRule, req.PerilRollup },
            cancellationToken: cancellationToken)) > 0;
    }
}
