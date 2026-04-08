using Dapper;
using RatingEngine.Core;

namespace RatingEngine.Data;

public sealed class SqlProductManifestRepository : IProductManifestRepository
{
    private readonly DbConnectionFactory _db;
    public SqlProductManifestRepository(DbConnectionFactory db) => _db = db;

    public async Task<ProductManifest?> GetAsync(string productCode, DateOnly effectiveDate, CancellationToken cancellationToken = default)
    {
        const string manifestSql = """
            SELECT TOP 1 Id, ProductCode, Version, EffStart AS EffectiveStart
            FROM ProductManifest
            WHERE ProductCode = @ProductCode
              AND EffStart <= @EffectiveDate
              AND (ExpireAt IS NULL OR ExpireAt > @EffectiveDate)
            ORDER BY EffStart DESC
            """;

        // Flat coverages (personal lines — no LobId)
        const string coverageSql = """
            SELECT CoverageCode
            FROM CoverageRef
            WHERE ProductManifestId = @ManifestId
              AND LobId IS NULL
            ORDER BY SortOrder
            """;

        // LOBs and their coverages (commercial products)
        const string lobSql = """
            SELECT Id, LobCode, SortOrder
            FROM ProductLob
            WHERE ProductManifestId = @ManifestId
            ORDER BY SortOrder
            """;

        const string lobCoverageSql = """
            SELECT LobId, CoverageCode
            FROM CoverageRef
            WHERE ProductManifestId = @ManifestId
              AND LobId IS NOT NULL
            ORDER BY LobId, SortOrder
            """;

        using var conn = _db.Create();

        var row = await conn.QueryFirstOrDefaultAsync<ManifestRow>(new CommandDefinition(
            manifestSql,
            new { ProductCode = productCode, EffectiveDate = effectiveDate },
            cancellationToken: cancellationToken));

        if (row is null) return null;

        var flatCoverages = (await conn.QueryAsync<CoverageRef>(new CommandDefinition(
            coverageSql,
            new { ManifestId = row.Id },
            cancellationToken: cancellationToken))).AsList();

        var lobRows = (await conn.QueryAsync<LobRow>(new CommandDefinition(
            lobSql,
            new { ManifestId = row.Id },
            cancellationToken: cancellationToken))).AsList();

        IReadOnlyList<LobRef> lobs = Array.Empty<LobRef>();
        if (lobRows.Count > 0)
        {
            var lobCoverageRows = (await conn.QueryAsync<LobCoverageRow>(new CommandDefinition(
                lobCoverageSql,
                new { ManifestId = row.Id },
                cancellationToken: cancellationToken))).AsList();

            var covsByLob = lobCoverageRows
                .GroupBy(c => c.LobId)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<CoverageRef>)g.Select(c => new CoverageRef(c.CoverageCode)).ToList());

            lobs = lobRows.Select(l =>
            {
                covsByLob.TryGetValue(l.Id, out var covs);
                return new LobRef(l.LobCode, covs ?? Array.Empty<CoverageRef>());
            }).ToList();
        }

        return new ProductManifest(row.ProductCode, row.Version, row.EffectiveStart, flatCoverages)
        {
            Lobs = lobs
        };
    }

    private sealed class ManifestRow
    {
        public int Id { get; init; }
        public string ProductCode { get; init; } = string.Empty;
        public string Version { get; init; } = string.Empty;
        public DateOnly EffectiveStart { get; init; }
    }

    private sealed class LobRow
    {
        public int Id { get; init; }
        public string LobCode { get; init; } = string.Empty;
        public int SortOrder { get; init; }
    }

    private sealed class LobCoverageRow
    {
        public int LobId { get; init; }
        public string CoverageCode { get; init; } = string.Empty;
    }
}
