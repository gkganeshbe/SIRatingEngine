using Dapper;

namespace RatingEngine.Data.Admin;

public sealed class PgSqlLookupDimensionAdminRepository : ILookupDimensionAdminRepository
{
    private readonly DbConnectionFactory _db;
    public PgSqlLookupDimensionAdminRepository(DbConnectionFactory db) => _db = db;

    public async Task<IReadOnlyList<LookupDimensionSummary>> ListAsync(int? productManifestId = null, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT "Id", "ProductId", "Name", "Description", "SortOrder"
            FROM "LookupDimension"
            WHERE "ProductId" IS NULL OR "ProductId" = @ManifestId
            ORDER BY "Name"
            """;
        using var conn = _db.Create();
        return (await conn.QueryAsync<LookupDimensionSummary>(new CommandDefinition(
            sql,
            new { ManifestId = productManifestId },
            cancellationToken: cancellationToken))).AsList();
    }

    public async Task<LookupDimensionDetail?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        const string dimSql = """
            SELECT "Id", "ProductId", "Name", "Description", "SortOrder"
            FROM "LookupDimension" WHERE "Id" = @Id
            """;
        const string valSql = """
            SELECT "Id", "LookupDimensionId", "Value", "DisplayLabel", "SortOrder"
            FROM "LookupDimensionValue" WHERE "LookupDimensionId" = @Id ORDER BY "SortOrder"
            """;

        using var conn = _db.Create();
        var dim = await conn.QueryFirstOrDefaultAsync<LookupDimensionSummary>(new CommandDefinition(
            dimSql, new { Id = id }, cancellationToken: cancellationToken));
        if (dim is null) return null;
        var values = (await conn.QueryAsync<LookupDimensionValueDetail>(new CommandDefinition(
            valSql, new { Id = id }, cancellationToken: cancellationToken))).AsList();
        return new LookupDimensionDetail(dim.Id, dim.ProductManifestId, dim.Name, dim.Description, dim.SortOrder, values);
    }

    public async Task<int> CreateAsync(int? productManifestId, CreateLookupDimensionRequest req, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO "LookupDimension" ("ProductId", "Name", "Description", "SortOrder")
            VALUES (@ManifestId, @Name, @Description, @SortOrder)
            RETURNING "Id"
            """;
        using var conn = _db.Create();
        return await conn.QuerySingleAsync<int>(new CommandDefinition(
            sql,
            new { ManifestId = productManifestId, req.Name, req.Description, req.SortOrder },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> UpdateAsync(int id, UpdateLookupDimensionRequest req, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE "LookupDimension" SET "Description" = @Description, "SortOrder" = @SortOrder WHERE "Id" = @Id
            """;
        using var conn = _db.Create();
        return await conn.ExecuteAsync(new CommandDefinition(
            sql,
            new { Id = id, req.Description, req.SortOrder },
            cancellationToken: cancellationToken)) > 0;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        using var conn = _db.Create();
        return await conn.ExecuteAsync(new CommandDefinition(
            """DELETE FROM "LookupDimension" WHERE "Id" = @Id""",
            new { Id = id },
            cancellationToken: cancellationToken)) > 0;
    }

    public async Task<int> AddValueAsync(int dimensionId, CreateLookupValueRequest req, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO "LookupDimensionValue" ("LookupDimensionId", "Value", "DisplayLabel", "SortOrder")
            VALUES (@DimensionId, @Value, @DisplayLabel, @SortOrder)
            RETURNING "Id"
            """;
        using var conn = _db.Create();
        return await conn.QuerySingleAsync<int>(new CommandDefinition(
            sql,
            new { DimensionId = dimensionId, req.Value, req.DisplayLabel, req.SortOrder },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> DeleteValueAsync(int valueId, CancellationToken cancellationToken = default)
    {
        using var conn = _db.Create();
        return await conn.ExecuteAsync(new CommandDefinition(
            """DELETE FROM "LookupDimensionValue" WHERE "Id" = @Id""",
            new { Id = valueId },
            cancellationToken: cancellationToken)) > 0;
    }
}
