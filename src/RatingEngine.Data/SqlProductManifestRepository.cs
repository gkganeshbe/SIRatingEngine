using Dapper;
using RatingEngine.Core;

namespace RatingEngine.Data;

public sealed class SqlProductManifestRepository : IProductManifestRepository
{
    private readonly DbConnectionFactory _db;
    public SqlProductManifestRepository(DbConnectionFactory db) => _db = db;

    public async Task<ProductManifest?> GetAsync(string productCode, string version)
    {
        const string manifestSql = """
            SELECT Id, ProductCode, Version, EffStart AS EffectiveStart
            FROM ProductManifest
            WHERE ProductCode = @ProductCode AND Version = @Version
            """;

        const string coverageSql = """
            SELECT CoverageCode, CoverageVersion AS Version
            FROM CoverageRef
            WHERE ProductManifestId = @ManifestId
            ORDER BY SortOrder
            """;

        using var conn = _db.Create();

        var row = await conn.QueryFirstOrDefaultAsync<ManifestRow>(
            manifestSql, new { ProductCode = productCode, Version = version });

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
