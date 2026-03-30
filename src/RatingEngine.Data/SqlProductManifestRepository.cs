using Dapper;
using RatingEngine.Core;

namespace RatingEngine.Data;

public sealed class SqlProductManifestRepository : IProductManifestRepository
{
    private readonly DbConnectionFactory _db;
    public SqlProductManifestRepository(DbConnectionFactory db) => _db = db;

    public async Task<ProductManifest?> GetAsync(string productCode, DateOnly effectiveDate)
    {
        const string manifestSql = """
            SELECT TOP 1 Id, ProductCode, Version, EffStart AS EffectiveStart
            FROM ProductManifest
            WHERE ProductCode = @ProductCode
              AND EffStart <= @EffectiveDate
              AND (ExpireAt IS NULL OR ExpireAt > @EffectiveDate)
            ORDER BY EffStart DESC
            """;

        const string coverageSql = """
            SELECT CoverageCode, CoverageVersion AS Version
            FROM CoverageRef
            WHERE ProductManifestId = @ManifestId
            ORDER BY SortOrder
            """;

        using var conn = _db.Create();

        var row = await conn.QueryFirstOrDefaultAsync<ManifestRow>(
            manifestSql, new { ProductCode = productCode, EffectiveDate = effectiveDate });

        if (row is null) return null;

        var coverages = (await conn.QueryAsync<CoverageRef>(
            coverageSql, new { ManifestId = row.Id })).AsList();

        return new ProductManifest(row.ProductCode, row.Version, row.EffectiveStart, coverages);
    }

    private sealed class ManifestRow
    {
        public int Id { get; init; }
        public string ProductCode { get; init; } = string.Empty;
        public string Version { get; init; } = string.Empty;
        public DateOnly EffectiveStart { get; init; }
    }
}
