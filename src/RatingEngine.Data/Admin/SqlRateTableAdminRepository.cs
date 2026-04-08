using Dapper;

namespace RatingEngine.Data.Admin;

public sealed class SqlRateTableAdminRepository : IRateTableAdminRepository
{
    private readonly DbConnectionFactory _db;
    public SqlRateTableAdminRepository(DbConnectionFactory db) => _db = db;

    // ── Rate Table CRUD ────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<RateTableSummary>> ListAsync(int coverageConfigId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, CoverageConfigId, Name, Description, IntendedCoverage,
                   LookupType, ValueType, InterpolationKeyCol,
                   EffStart, ExpireAt, CreatedAt, CreatedBy
            FROM RateTable
            WHERE CoverageConfigId = @CoverageConfigId
            ORDER BY Name
            """;

        using var conn = _db.Create();
        return (await conn.QueryAsync<RateTableSummary>(new CommandDefinition(
            sql,
            new { CoverageConfigId = coverageConfigId },
            cancellationToken: cancellationToken))).AsList();
    }

    public async Task<RateTableDetail?> GetAsync(int coverageConfigId, string name, CancellationToken cancellationToken = default)
    {
        const string tableSql = """
            SELECT Id, CoverageConfigId, Name, Description, IntendedCoverage,
                   LookupType, ValueType, InterpolationKeyCol,
                   EffStart, ExpireAt, CreatedAt, CreatedBy
            FROM RateTable
            WHERE CoverageConfigId = @CoverageConfigId AND Name = @Name
            """;

        const string colDefSql = """
            SELECT Id, ColumnName, DisplayLabel, DataType, SortOrder, IsRequired
            FROM RateTableColumnDef WHERE RateTableId = @TableId ORDER BY SortOrder
            """;

        using var conn = _db.Create();

        var row = await conn.QueryFirstOrDefaultAsync<TableRow>(new CommandDefinition(
            tableSql,
            new { CoverageConfigId = coverageConfigId, Name = name },
            cancellationToken: cancellationToken));
        if (row is null) return null;

        var colDefs = (await conn.QueryAsync<ColumnDefDetail>(new CommandDefinition(
            colDefSql,
            new { TableId = row.Id },
            cancellationToken: cancellationToken))).AsList();

        return new RateTableDetail(
            row.Id, row.CoverageConfigId, row.Name, row.Description, row.IntendedCoverage,
            row.LookupType, row.ValueType, row.InterpolationKeyCol,
            row.EffStart, row.ExpireAt,
            row.CreatedAt, row.CreatedBy,
            colDefs);
    }

    public async Task<int> CreateAsync(CreateRateTableRequest req, string? actor = null, CancellationToken cancellationToken = default)
    {
        const string insertSql = """
            INSERT INTO RateTable
                (CoverageConfigId, Name, Description, IntendedCoverage,
                 LookupType, ValueType, InterpolationKeyCol,
                 EffStart, ExpireAt, CreatedAt, CreatedBy)
            OUTPUT INSERTED.Id
            VALUES (@CoverageConfigId, @Name, @Description, @IntendedCoverage,
                    @LookupType, @ValueType, @InterpolationKeyCol,
                    @EffStart, @ExpireAt, GETUTCDATE(), @Actor)
            """;

        const string colDefSql = """
            INSERT INTO RateTableColumnDef
                (RateTableId, ColumnName, DisplayLabel, DataType, SortOrder, IsRequired)
            VALUES (@RateTableId, @ColumnName, @DisplayLabel, @DataType, @SortOrder, @IsRequired)
            """;

        using var conn = _db.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();

        var id = await conn.ExecuteScalarAsync<int>(new CommandDefinition(insertSql, new
        {
            req.CoverageConfigId, req.Name, req.Description, req.IntendedCoverage,
            req.LookupType, req.ValueType, req.InterpolationKeyCol,
            req.EffStart, req.ExpireAt, Actor = actor
        }, transaction: tx, cancellationToken: cancellationToken));

        foreach (var col in req.ColumnDefs)
            await conn.ExecuteAsync(new CommandDefinition(colDefSql, new
            {
                RateTableId = id,
                col.ColumnName, col.DisplayLabel, col.DataType, col.SortOrder, col.IsRequired
            }, transaction: tx, cancellationToken: cancellationToken));

        tx.Commit();
        return id;
    }

    public async Task<bool> UpdateAsync(int id, UpdateRateTableRequest req, string? actor = null, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE RateTable
            SET Description         = @Description,
                IntendedCoverage    = @IntendedCoverage,
                LookupType          = @LookupType,
                ValueType           = @ValueType,
                InterpolationKeyCol = @InterpolationKeyCol,
                ExpireAt            = @ExpireAt,
                ModifiedAt          = GETUTCDATE(),
                ModifiedBy          = @Actor
            WHERE Id = @Id
            """;

        using var conn = _db.Create();
        return await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = id, req.Description, req.IntendedCoverage,
            req.LookupType, req.ValueType, req.InterpolationKeyCol, req.ExpireAt, Actor = actor
        }, cancellationToken: cancellationToken)) > 0;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        using var conn = _db.Create();
        return await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM RateTable WHERE Id = @Id",
            new { Id = id },
            cancellationToken: cancellationToken)) > 0;
    }

    // ── Rate Table Rows ────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<RateTableRowDetail>> GetRowsAsync(int coverageConfigId, string tableName, DateOnly? effectiveDate = null, CancellationToken cancellationToken = default)
    {
        var sql = effectiveDate.HasValue
            ? """
              SELECT r.Id, r.Key1, r.Key2, r.Key3, r.Key4, r.Key5,
                     r.RangeFrom, r.RangeTo, r.Factor,
                     r.AdditionalUnit, r.AdditionalRate, r.EffStart, r.ExpireAt
              FROM RateTableRow r
              JOIN RateTable t ON t.Id = r.RateTableId
              WHERE t.CoverageConfigId = @CoverageConfigId AND t.Name = @TableName
                AND r.EffStart <= @EffDate
                AND (r.ExpireAt IS NULL OR r.ExpireAt > @EffDate)
              ORDER BY r.Id
              """
            : """
              SELECT r.Id, r.Key1, r.Key2, r.Key3, r.Key4, r.Key5,
                     r.RangeFrom, r.RangeTo, r.Factor,
                     r.AdditionalUnit, r.AdditionalRate, r.EffStart, r.ExpireAt
              FROM RateTableRow r
              JOIN RateTable t ON t.Id = r.RateTableId
              WHERE t.CoverageConfigId = @CoverageConfigId AND t.Name = @TableName
              ORDER BY r.EffStart, r.Id
              """;

        using var conn = _db.Create();
        return (await conn.QueryAsync<RateTableRowDetail>(new CommandDefinition(
            sql,
            new { CoverageConfigId = coverageConfigId, TableName = tableName, EffDate = effectiveDate },
            cancellationToken: cancellationToken))).AsList();
    }

    public async Task<long> AddRowAsync(int coverageConfigId, string tableName, CreateRateTableRowRequest req, CancellationToken cancellationToken = default)
    {
        const string tableIdSql = "SELECT Id FROM RateTable WHERE CoverageConfigId = @CoverageConfigId AND Name = @Name";
        const string insertSql = """
            INSERT INTO RateTableRow (
                RateTableId, Key1, Key2, Key3, Key4, Key5,
                RangeFrom, RangeTo, Factor,
                AdditionalUnit, AdditionalRate, EffStart, ExpireAt)
            OUTPUT INSERTED.Id
            VALUES (
                @RateTableId, @Key1, @Key2, @Key3, @Key4, @Key5,
                @RangeFrom, @RangeTo, @Factor,
                @AdditionalUnit, @AdditionalRate, @EffStart, @ExpireAt)
            """;

        using var conn = _db.Create();

        var tableId = await conn.ExecuteScalarAsync<int?>(new CommandDefinition(
            tableIdSql,
            new { CoverageConfigId = coverageConfigId, Name = tableName },
            cancellationToken: cancellationToken))
            ?? throw new InvalidOperationException($"Rate table '{tableName}' not found in coverage {coverageConfigId}.");

        return await conn.ExecuteScalarAsync<long>(new CommandDefinition(insertSql, new
        {
            RateTableId = tableId,
            req.Key1, req.Key2, req.Key3, req.Key4, req.Key5,
            req.RangeFrom, req.RangeTo, req.Factor,
            req.AdditionalUnit, req.AdditionalRate, req.EffStart, req.ExpireAt
        }, cancellationToken: cancellationToken));
    }

    public async Task<bool> UpdateRowAsync(int coverageConfigId, string tableName, long rowId, CreateRateTableRowRequest req, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE RateTableRow
            SET Key1 = @Key1, Key2 = @Key2, Key3 = @Key3, Key4 = @Key4, Key5 = @Key5,
                RangeFrom = @RangeFrom, RangeTo = @RangeTo,
                Factor = @Factor, AdditionalUnit = @AdditionalUnit, AdditionalRate = @AdditionalRate,
                EffStart = @EffStart, ExpireAt = @ExpireAt
            WHERE Id = @Id
              AND RateTableId = (
                  SELECT Id FROM RateTable
                  WHERE CoverageConfigId = @CoverageConfigId AND Name = @TableName
              )
            """;

        using var conn = _db.Create();
        return await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = rowId,
            CoverageConfigId = coverageConfigId,
            TableName = tableName,
            req.Key1, req.Key2, req.Key3, req.Key4, req.Key5,
            req.RangeFrom, req.RangeTo, req.Factor,
            req.AdditionalUnit, req.AdditionalRate, req.EffStart, req.ExpireAt
        }, cancellationToken: cancellationToken)) > 0;
    }

    public async Task<bool> ExpireRowAsync(long rowId, DateOnly expireAt, CancellationToken cancellationToken = default)
    {
        using var conn = _db.Create();
        return await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE RateTableRow SET ExpireAt = @ExpireAt WHERE Id = @Id",
            new { Id = rowId, ExpireAt = expireAt },
            cancellationToken: cancellationToken)) > 0;
    }

    public async Task<bool> DeleteRowAsync(long rowId, CancellationToken cancellationToken = default)
    {
        using var conn = _db.Create();
        return await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM RateTableRow WHERE Id = @Id",
            new { Id = rowId },
            cancellationToken: cancellationToken)) > 0;
    }

    public async Task<int> BulkInsertRowsAsync(int coverageConfigId, string tableName, IReadOnlyList<CreateRateTableRowRequest> rows, CancellationToken cancellationToken = default)
    {
        const string tableIdSql = "SELECT Id FROM RateTable WHERE CoverageConfigId = @CoverageConfigId AND Name = @Name";
        const string insertSql = """
            INSERT INTO RateTableRow (
                RateTableId, Key1, Key2, Key3, Key4, Key5,
                RangeFrom, RangeTo, Factor,
                AdditionalUnit, AdditionalRate, EffStart, ExpireAt)
            VALUES (
                @RateTableId, @Key1, @Key2, @Key3, @Key4, @Key5,
                @RangeFrom, @RangeTo, @Factor,
                @AdditionalUnit, @AdditionalRate, @EffStart, @ExpireAt)
            """;

        using var conn = _db.Create();
        conn.Open();

        var tableId = await conn.ExecuteScalarAsync<int?>(new CommandDefinition(
            tableIdSql,
            new { CoverageConfigId = coverageConfigId, Name = tableName },
            cancellationToken: cancellationToken))
            ?? throw new InvalidOperationException($"Rate table '{tableName}' not found in coverage {coverageConfigId}.");

        using var tx = conn.BeginTransaction();
        var count = 0;

        foreach (var row in rows)
        {
            await conn.ExecuteAsync(new CommandDefinition(insertSql, new
            {
                RateTableId = tableId,
                row.Key1, row.Key2, row.Key3, row.Key4, row.Key5,
                row.RangeFrom, row.RangeTo, row.Factor,
                row.AdditionalUnit, row.AdditionalRate, row.EffStart, row.ExpireAt
            }, transaction: tx, cancellationToken: cancellationToken));
            count++;
        }

        tx.Commit();
        return count;
    }

    // ── Private row types ──────────────────────────────────────────────────────

    private sealed class TableRow
    {
        public int Id { get; init; }
        public int CoverageConfigId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? Description { get; init; }
        public string? IntendedCoverage { get; init; }
        public string LookupType { get; init; } = "EXACT";
        public string ValueType { get; init; } = "Factor";
        public string? InterpolationKeyCol { get; init; }
        public DateOnly EffStart { get; init; }
        public DateOnly? ExpireAt { get; init; }
        public DateTime CreatedAt { get; init; }
        public string? CreatedBy { get; init; }
    }
}
