using System.Data;
using Dapper;

namespace RatingEngine.Data.Admin;

public sealed class SqlProductAdminRepository : IProductAdminRepository
{
    private readonly DbConnectionFactory _db;
    public SqlProductAdminRepository(DbConnectionFactory db) => _db = db;

    public async Task<IReadOnlyList<ProductSummary>> ListAsync()
    {
        const string sql = """
            SELECT Id, ProductCode, Version, EffStart, ExpireAt, CreatedAt, CreatedBy
            FROM ProductManifest
            ORDER BY ProductCode, Version
            """;

        using var conn = _db.Create();
        return (await conn.QueryAsync<ProductSummary>(sql)).AsList();
    }

    public async Task<ProductDetail?> GetAsync(string productCode, string version)
    {
        const string manifestSql = """
            SELECT Id, ProductCode, Version, EffStart, ExpireAt,
                   CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
            FROM ProductManifest
            WHERE ProductCode = @ProductCode AND Version = @Version
            """;

        const string coverageSql = """
            SELECT Id, CoverageCode, CoverageVersion, SortOrder
            FROM CoverageRef
            WHERE ProductManifestId = @ManifestId
            ORDER BY SortOrder
            """;

        using var conn = _db.Create();

        var row = await conn.QueryFirstOrDefaultAsync<ManifestRow>(
            manifestSql, new { ProductCode = productCode, Version = version });

        if (row is null) return null;

        var coverages = (await conn.QueryAsync<CoverageRefDetail>(
            coverageSql, new { ManifestId = row.Id })).AsList();

        return new ProductDetail(
            row.Id, row.ProductCode, row.Version,
            row.EffStart, row.ExpireAt,
            row.CreatedAt, row.CreatedBy,
            row.ModifiedAt, row.ModifiedBy,
            coverages);
    }

    public async Task<int> CreateAsync(CreateProductRequest req, string? actor = null)
    {
        const string insertSql = """
            INSERT INTO ProductManifest (ProductCode, Version, EffStart, ExpireAt, CreatedAt, CreatedBy)
            OUTPUT INSERTED.Id
            VALUES (@ProductCode, @Version, @EffStart, @ExpireAt, GETUTCDATE(), @Actor)
            """;

        const string coverageSql = """
            INSERT INTO CoverageRef (ProductManifestId, CoverageCode, CoverageVersion, SortOrder)
            VALUES (@ProductManifestId, @CoverageCode, @CoverageVersion, @SortOrder)
            """;

        using var conn = _db.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();

        var id = await conn.ExecuteScalarAsync<int>(insertSql,
            new { req.ProductCode, req.Version, req.EffStart, req.ExpireAt, Actor = actor }, tx);

        for (int i = 0; i < req.Coverages.Count; i++)
        {
            var c = req.Coverages[i];
            await conn.ExecuteAsync(coverageSql,
                new { ProductManifestId = id, c.CoverageCode, c.CoverageVersion, SortOrder = i }, tx);
        }

        tx.Commit();
        return id;
    }

    public async Task<bool> UpdateAsync(int id, UpdateProductRequest req, string? actor = null)
    {
        const string updateSql = """
            UPDATE ProductManifest
            SET EffStart = @EffStart, ExpireAt = @ExpireAt,
                ModifiedAt = GETUTCDATE(), ModifiedBy = @Actor
            WHERE Id = @Id
            """;

        const string deleteCoveragesSql = "DELETE FROM CoverageRef WHERE ProductManifestId = @Id";

        const string coverageSql = """
            INSERT INTO CoverageRef (ProductManifestId, CoverageCode, CoverageVersion, SortOrder)
            VALUES (@ProductManifestId, @CoverageCode, @CoverageVersion, @SortOrder)
            """;

        using var conn = _db.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();

        var affected = await conn.ExecuteAsync(updateSql,
            new { Id = id, req.EffStart, req.ExpireAt, Actor = actor }, tx);

        if (affected == 0) { tx.Rollback(); return false; }

        await conn.ExecuteAsync(deleteCoveragesSql, new { Id = id }, tx);

        for (int i = 0; i < req.Coverages.Count; i++)
        {
            var c = req.Coverages[i];
            await conn.ExecuteAsync(coverageSql,
                new { ProductManifestId = id, c.CoverageCode, c.CoverageVersion, SortOrder = i }, tx);
        }

        tx.Commit();
        return true;
    }

    public async Task<bool> ExpireAsync(int id, DateOnly expireAt, string? actor = null)
    {
        const string sql = """
            UPDATE ProductManifest
            SET ExpireAt = @ExpireAt, ModifiedAt = GETUTCDATE(), ModifiedBy = @Actor
            WHERE Id = @Id
            """;

        using var conn = _db.Create();
        return await conn.ExecuteAsync(sql, new { Id = id, ExpireAt = expireAt, Actor = actor }) > 0;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        using var conn = _db.Create();
        return await conn.ExecuteAsync("DELETE FROM ProductManifest WHERE Id = @Id", new { Id = id }) > 0;
    }

    // ── Private row types ──────────────────────────────────────────────────────

    private sealed class ManifestRow
    {
        public int Id { get; init; }
        public string ProductCode { get; init; } = string.Empty;
        public string Version { get; init; } = string.Empty;
        public DateOnly EffStart { get; init; }
        public DateOnly? ExpireAt { get; init; }
        public DateTime CreatedAt { get; init; }
        public string? CreatedBy { get; init; }
        public DateTime? ModifiedAt { get; init; }
        public string? ModifiedBy { get; init; }
    }
}
