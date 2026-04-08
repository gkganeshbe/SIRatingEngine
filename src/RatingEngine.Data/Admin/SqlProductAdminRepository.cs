using System.Data;
using Dapper;

namespace RatingEngine.Data.Admin;

public sealed class SqlProductAdminRepository : IProductAdminRepository
{
    private readonly DbConnectionFactory _db;
    public SqlProductAdminRepository(DbConnectionFactory db) => _db = db;

    public async Task<IReadOnlyList<ProductSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, ProductCode, Version, EffStart, ExpireAt, CreatedAt, CreatedBy
            FROM ProductManifest
            ORDER BY ProductCode, Version
            """;

        using var conn = _db.Create();
        return (await conn.QueryAsync<ProductSummary>(new CommandDefinition(
            sql,
            cancellationToken: cancellationToken))).AsList();
    }

    public async Task<ProductDetail?> GetAsync(string productCode, string version, CancellationToken cancellationToken = default)
    {
        const string manifestSql = """
            SELECT Id, ProductCode, Version, EffStart, ExpireAt,
                   CreatedAt, CreatedBy, ModifiedAt, ModifiedBy, Notes
            FROM ProductManifest
            WHERE ProductCode = @ProductCode AND Version = @Version
            """;

        const string flatCoverageSql = """
            SELECT Id, CoverageCode, SortOrder, AggregationRule, PerilRollup
            FROM CoverageRef
            WHERE ProductManifestId = @ManifestId AND LobId IS NULL
            ORDER BY SortOrder
            """;

        const string lobSql = """
            SELECT Id, LobCode, SortOrder
            FROM ProductLob
            WHERE ProductManifestId = @ManifestId
            ORDER BY SortOrder
            """;

        const string lobCoverageSql = """
            SELECT Id, LobId, CoverageCode, SortOrder, AggregationRule, PerilRollup
            FROM CoverageRef
            WHERE ProductManifestId = @ManifestId AND LobId IS NOT NULL
            ORDER BY LobId, SortOrder
            """;

        using var conn = _db.Create();

        var row = await conn.QueryFirstOrDefaultAsync<ManifestRow>(new CommandDefinition(
            manifestSql,
            new { ProductCode = productCode, Version = version },
            cancellationToken: cancellationToken));

        if (row is null) return null;

        var flatCoverages = (await conn.QueryAsync<CoverageRefDetail>(new CommandDefinition(
            flatCoverageSql,
            new { ManifestId = row.Id },
            cancellationToken: cancellationToken))).AsList();

        var lobRows = (await conn.QueryAsync<LobAdminRow>(new CommandDefinition(
            lobSql,
            new { ManifestId = row.Id },
            cancellationToken: cancellationToken))).AsList();

        IReadOnlyList<LobRefDetail> lobs = Array.Empty<LobRefDetail>();
        if (lobRows.Count > 0)
        {
            var lobCovRows = (await conn.QueryAsync<LobCoverageAdminRow>(new CommandDefinition(
                lobCoverageSql,
                new { ManifestId = row.Id },
                cancellationToken: cancellationToken))).AsList();

            var covsByLob = lobCovRows
                .GroupBy(c => c.LobId)
                .ToDictionary(g => g.Key, g => g.Select(c =>
                    new CoverageRefDetail(c.Id, c.CoverageCode, c.SortOrder, c.AggregationRule, c.PerilRollup)).ToList());

            lobs = lobRows.Select(l =>
            {
                covsByLob.TryGetValue(l.Id, out var covs);
                return new LobRefDetail(l.Id, l.LobCode, l.SortOrder, covs ?? new List<CoverageRefDetail>());
            }).ToList();
        }

        var policyAdjustments = await SqlPolicyAdjustmentAdminRepository.LoadAdjustmentsAsync(conn, row.Id, cancellationToken);

        return new ProductDetail(
            row.Id, row.ProductCode, row.Version,
            row.EffStart, row.ExpireAt,
            row.CreatedAt, row.CreatedBy,
            row.ModifiedAt, row.ModifiedBy,
            row.Notes,
            flatCoverages)
        {
            Lobs              = lobs,
            PolicyAdjustments = policyAdjustments,
        };
    }

    public async Task<int> CreateAsync(CreateProductRequest req, string? actor = null, CancellationToken cancellationToken = default)
    {
        const string insertSql = """
            INSERT INTO ProductManifest (ProductCode, Version, EffStart, ExpireAt, Notes, CreatedAt, CreatedBy)
            OUTPUT INSERTED.Id
            VALUES (@ProductCode, @Version, @EffStart, @ExpireAt, @Notes, GETUTCDATE(), @Actor)
            """;

        const string flatCoverageSql = """
            INSERT INTO CoverageRef (ProductManifestId, CoverageCode, SortOrder)
            VALUES (@ProductManifestId, @CoverageCode, @SortOrder)
            """;

        const string lobSql = """
            INSERT INTO ProductLob (ProductManifestId, LobCode, SortOrder)
            OUTPUT INSERTED.Id
            VALUES (@ProductManifestId, @LobCode, @SortOrder)
            """;

        const string lobCoverageSql = """
            INSERT INTO CoverageRef (ProductManifestId, LobId, CoverageCode, SortOrder)
            VALUES (@ProductManifestId, @LobId, @CoverageCode, @SortOrder)
            """;

        using var conn = _db.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();

        var id = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            insertSql,
            new { req.ProductCode, req.Version, req.EffStart, req.ExpireAt, req.Notes, Actor = actor },
            transaction: tx,
            cancellationToken: cancellationToken));

        if (req.Lobs.Count > 0)
        {
            // Commercial product — insert LOBs and their coverages
            for (int li = 0; li < req.Lobs.Count; li++)
            {
                var lob   = req.Lobs[li];
                var lobId = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
                    lobSql,
                    new { ProductManifestId = id, lob.LobCode, SortOrder = li },
                    transaction: tx,
                    cancellationToken: cancellationToken));

                for (int ci = 0; ci < lob.Coverages.Count; ci++)
                {
                    var c = lob.Coverages[ci];
                    await conn.ExecuteAsync(new CommandDefinition(
                        lobCoverageSql,
                        new { ProductManifestId = id, LobId = lobId, c.CoverageCode, SortOrder = ci },
                        transaction: tx,
                        cancellationToken: cancellationToken));
                }
            }
        }
        else
        {
            // Personal lines — flat coverage list
            for (int i = 0; i < req.Coverages.Count; i++)
            {
                var c = req.Coverages[i];
                await conn.ExecuteAsync(new CommandDefinition(
                    flatCoverageSql,
                    new { ProductManifestId = id, c.CoverageCode, SortOrder = i },
                    transaction: tx,
                    cancellationToken: cancellationToken));
            }
        }

        tx.Commit();
        return id;
    }

    public async Task<bool> UpdateAsync(int id, UpdateProductRequest req, string? actor = null, CancellationToken cancellationToken = default)
    {
        const string updateSql = """
            UPDATE ProductManifest
            SET EffStart = @EffStart, ExpireAt = @ExpireAt, Notes = @Notes,
                ModifiedAt = GETUTCDATE(), ModifiedBy = @Actor
            WHERE Id = @Id
            """;

        const string deleteCoveragesSql = "DELETE FROM CoverageRef WHERE ProductManifestId = @Id";
        const string deleteLobsSql      = "DELETE FROM ProductLob    WHERE ProductManifestId = @Id";

        const string flatCoverageSql = """
            INSERT INTO CoverageRef (ProductManifestId, CoverageCode, SortOrder)
            VALUES (@ProductManifestId, @CoverageCode, @SortOrder)
            """;

        const string lobSql = """
            INSERT INTO ProductLob (ProductManifestId, LobCode, SortOrder)
            OUTPUT INSERTED.Id
            VALUES (@ProductManifestId, @LobCode, @SortOrder)
            """;

        const string lobCoverageSql = """
            INSERT INTO CoverageRef (ProductManifestId, LobId, CoverageCode, SortOrder)
            VALUES (@ProductManifestId, @LobId, @CoverageCode, @SortOrder)
            """;

        using var conn = _db.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();

        var affected = await conn.ExecuteAsync(new CommandDefinition(
            updateSql,
            new { Id = id, req.EffStart, req.ExpireAt, req.Notes, Actor = actor },
            transaction: tx,
            cancellationToken: cancellationToken));

        if (affected == 0) { tx.Rollback(); return false; }

        // Delete all existing coverages and LOBs; re-insert from the request
        await conn.ExecuteAsync(new CommandDefinition(deleteCoveragesSql, new { Id = id }, transaction: tx, cancellationToken: cancellationToken));
        await conn.ExecuteAsync(new CommandDefinition(deleteLobsSql, new { Id = id }, transaction: tx, cancellationToken: cancellationToken));

        if (req.Lobs.Count > 0)
        {
            for (int li = 0; li < req.Lobs.Count; li++)
            {
                var lob   = req.Lobs[li];
                var lobId = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
                    lobSql,
                    new { ProductManifestId = id, lob.LobCode, SortOrder = li },
                    transaction: tx,
                    cancellationToken: cancellationToken));

                for (int ci = 0; ci < lob.Coverages.Count; ci++)
                {
                    var c = lob.Coverages[ci];
                    await conn.ExecuteAsync(new CommandDefinition(
                        lobCoverageSql,
                        new { ProductManifestId = id, LobId = lobId, c.CoverageCode, SortOrder = ci },
                        transaction: tx,
                        cancellationToken: cancellationToken));
                }
            }
        }
        else
        {
            for (int i = 0; i < req.Coverages.Count; i++)
            {
                var c = req.Coverages[i];
                await conn.ExecuteAsync(new CommandDefinition(
                    flatCoverageSql,
                    new { ProductManifestId = id, c.CoverageCode, SortOrder = i },
                    transaction: tx,
                    cancellationToken: cancellationToken));
            }
        }

        tx.Commit();
        return true;
    }

    public async Task<bool> ExpireAsync(int id, DateOnly expireAt, string? actor = null, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE ProductManifest
            SET ExpireAt = @ExpireAt, ModifiedAt = GETUTCDATE(), ModifiedBy = @Actor
            WHERE Id = @Id
            """;

        using var conn = _db.Create();
        return await conn.ExecuteAsync(new CommandDefinition(
            sql,
            new { Id = id, ExpireAt = expireAt, Actor = actor },
            cancellationToken: cancellationToken)) > 0;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        using var conn = _db.Create();
        return await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM ProductManifest WHERE Id = @Id",
            new { Id = id },
            cancellationToken: cancellationToken)) > 0;
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
        public string? Notes { get; init; }
    }

    private sealed class LobAdminRow
    {
        public int Id { get; init; }
        public string LobCode { get; init; } = string.Empty;
        public int SortOrder { get; init; }
    }

    private sealed class LobCoverageAdminRow
    {
        public int Id { get; init; }
        public int LobId { get; init; }
        public string CoverageCode { get; init; } = string.Empty;
        public int SortOrder { get; init; }
        public string? AggregationRule { get; init; }
        public string? PerilRollup { get; init; }
    }
}
