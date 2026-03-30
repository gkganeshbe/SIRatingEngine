using System.Data;
using Dapper;
using RatingEngine.Core;

namespace RatingEngine.Data.Admin;

public sealed class SqlCoverageAdminRepository : ICoverageAdminRepository
{
    private readonly DbConnectionFactory _db;
    public SqlCoverageAdminRepository(DbConnectionFactory db) => _db = db;

    public async Task<IReadOnlyList<CoverageSummary>> ListAsync(string? productCode = null)
    {
        var sql = productCode is null
            ? """
              SELECT Id, ProductCode, State, CoverageCode, Version, EffStart, ExpireAt, CreatedAt, CreatedBy
              FROM CoverageConfig ORDER BY ProductCode, State, CoverageCode, Version
              """
            : """
              SELECT Id, ProductCode, State, CoverageCode, Version, EffStart, ExpireAt, CreatedAt, CreatedBy
              FROM CoverageConfig WHERE ProductCode = @ProductCode ORDER BY State, CoverageCode, Version
              """;

        using var conn = _db.Create();
        return (await conn.QueryAsync<CoverageSummary>(sql, new { ProductCode = productCode })).AsList();
    }

    public async Task<CoverageDetail?> GetAsync(string productCode, string coverageCode, string version)
    {
        const string configSql = """
            SELECT Id, ProductCode, State, CoverageCode, Version, EffStart, ExpireAt,
                   CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
            FROM CoverageConfig
            WHERE ProductCode = @ProductCode AND CoverageCode = @CoverageCode AND Version = @Version
            """;

        const string perilSql = """
            SELECT PerilCode FROM CoveragePeril
            WHERE CoverageConfigId = @ConfigId ORDER BY SortOrder
            """;

        const string stepSql = """
            SELECT Id, StepOrder, StepId, Name, Operation,
                   RateTableName, MathType, InterpolateKey, RangeKeyName,
                   ComputeExpr, ComputeStoreAs, ComputeApplyToPremium,
                   RoundPrecision, RoundMode,
                   WhenPath, WhenOperator, WhenValue
            FROM PipelineStep WHERE CoverageConfigId = @ConfigId ORDER BY StepOrder
            """;

        const string stepKeySql = """
            SELECT PipelineStepId, KeyName, KeyValue
            FROM PipelineStepKey WHERE PipelineStepId IN @StepIds
            """;

        using var conn = _db.Create();

        var row = await conn.QueryFirstOrDefaultAsync<CoverageRow>(configSql,
            new { ProductCode = productCode, CoverageCode = coverageCode, Version = version });

        if (row is null) return null;

        var perils = (await conn.QueryAsync<string>(perilSql, new { ConfigId = row.Id })).AsList();
        var stepRows = (await conn.QueryAsync<StepRow>(stepSql, new { ConfigId = row.Id })).AsList();

        List<StepKeyRow> keyRows = [];
        if (stepRows.Count > 0)
        {
            var stepIds = stepRows.Select(s => s.Id).ToArray();
            keyRows = (await conn.QueryAsync<StepKeyRow>(stepKeySql, new { StepIds = stepIds })).AsList();
        }

        var keysByStep = keyRows
            .GroupBy(k => k.PipelineStepId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(k => k.KeyName, k => k.KeyValue));

        var pipeline = stepRows.Select(s => BuildStep(s, keysByStep)).ToList();

        return new CoverageDetail(
            row.Id, row.ProductCode, row.State, row.CoverageCode, row.Version,
            row.EffStart, row.ExpireAt,
            row.CreatedAt, row.CreatedBy,
            row.ModifiedAt, row.ModifiedBy,
            perils, pipeline);
    }

    public async Task<int> CreateAsync(CreateCoverageRequest req, string? actor = null)
    {
        const string insertSql = """
            INSERT INTO CoverageConfig
                (ProductCode, State, CoverageCode, Version, EffStart, ExpireAt, CreatedAt, CreatedBy)
            OUTPUT INSERTED.Id
            VALUES (@ProductCode, @State, @CoverageCode, @Version, @EffStart, @ExpireAt, GETUTCDATE(), @Actor)
            """;

        using var conn = _db.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();

        var id = await conn.ExecuteScalarAsync<int>(insertSql,
            new { req.ProductCode, req.State, req.CoverageCode, req.Version, req.EffStart, req.ExpireAt, Actor = actor }, tx);

        await InsertPerilsAsync(conn, tx, id, req.Perils);
        await InsertPipelineAsync(conn, tx, id, req.Pipeline);

        tx.Commit();
        return id;
    }

    public async Task<bool> UpdateAsync(int id, UpdateCoverageRequest req, string? actor = null)
    {
        const string updateSql = """
            UPDATE CoverageConfig
            SET EffStart = @EffStart, ExpireAt = @ExpireAt,
                ModifiedAt = GETUTCDATE(), ModifiedBy = @Actor
            WHERE Id = @Id
            """;

        using var conn = _db.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();

        var affected = await conn.ExecuteAsync(updateSql,
            new { Id = id, req.EffStart, req.ExpireAt, Actor = actor }, tx);

        if (affected == 0) { tx.Rollback(); return false; }

        // Replace perils and pipeline (cascade deletes PipelineStepKey via FK)
        await conn.ExecuteAsync("DELETE FROM CoveragePeril WHERE CoverageConfigId = @Id", new { Id = id }, tx);
        await conn.ExecuteAsync("DELETE FROM PipelineStep WHERE CoverageConfigId = @Id", new { Id = id }, tx);

        await InsertPerilsAsync(conn, tx, id, req.Perils);
        await InsertPipelineAsync(conn, tx, id, req.Pipeline);

        tx.Commit();
        return true;
    }

    public async Task<bool> ExpireAsync(int id, DateOnly expireAt, string? actor = null)
    {
        const string sql = """
            UPDATE CoverageConfig
            SET ExpireAt = @ExpireAt, ModifiedAt = GETUTCDATE(), ModifiedBy = @Actor
            WHERE Id = @Id
            """;

        using var conn = _db.Create();
        return await conn.ExecuteAsync(sql, new { Id = id, ExpireAt = expireAt, Actor = actor }) > 0;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        using var conn = _db.Create();
        return await conn.ExecuteAsync("DELETE FROM CoverageConfig WHERE Id = @Id", new { Id = id }) > 0;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static async Task InsertPerilsAsync(
        System.Data.IDbConnection conn, IDbTransaction tx, int configId, IReadOnlyList<string> perils)
    {
        const string sql = """
            INSERT INTO CoveragePeril (CoverageConfigId, PerilCode, SortOrder)
            VALUES (@CoverageConfigId, @PerilCode, @SortOrder)
            """;

        for (int i = 0; i < perils.Count; i++)
            await conn.ExecuteAsync(sql, new { CoverageConfigId = configId, PerilCode = perils[i], SortOrder = i }, tx);
    }

    private static async Task InsertPipelineAsync(
        System.Data.IDbConnection conn, IDbTransaction tx, int configId, IReadOnlyList<StepConfig> steps)
    {
        const string stepSql = """
            INSERT INTO PipelineStep (
                CoverageConfigId, StepOrder, StepId, Name, Operation,
                RateTableName, MathType, InterpolateKey, RangeKeyName,
                ComputeExpr, ComputeStoreAs, ComputeApplyToPremium,
                RoundPrecision, RoundMode,
                WhenPath, WhenOperator, WhenValue)
            OUTPUT INSERTED.Id
            VALUES (
                @CoverageConfigId, @StepOrder, @StepId, @Name, @Operation,
                @RateTableName, @MathType, @InterpolateKey, @RangeKeyName,
                @ComputeExpr, @ComputeStoreAs, @ComputeApplyToPremium,
                @RoundPrecision, @RoundMode,
                @WhenPath, @WhenOperator, @WhenValue)
            """;

        const string keySql = """
            INSERT INTO PipelineStepKey (PipelineStepId, KeyName, KeyValue)
            VALUES (@PipelineStepId, @KeyName, @KeyValue)
            """;

        for (int i = 0; i < steps.Count; i++)
        {
            var s = steps[i];
            var (whenPath, whenOp, whenVal) = ExtractWhen(s.When);

            var stepId = await conn.ExecuteScalarAsync<int>(stepSql, new
            {
                CoverageConfigId = configId,
                StepOrder        = i,
                StepId           = s.Id,
                s.Name,
                s.Operation,
                RateTableName         = s.RateTable,
                MathType              = s.Math?.Type,
                InterpolateKey        = s.Interpolate?.Key,
                RangeKeyName          = s.RangeKey?.Key,
                ComputeExpr           = s.Compute?.Expr,
                ComputeStoreAs        = s.Compute?.StoreAs,
                ComputeApplyToPremium = s.Compute?.ApplyToPremium,
                RoundPrecision        = s.Round?.Precision,
                RoundMode             = s.Round?.Mode,
                WhenPath              = whenPath,
                WhenOperator          = whenOp,
                WhenValue             = whenVal
            }, tx);

            if (s.Keys is not null)
            {
                foreach (var kv in s.Keys)
                    await conn.ExecuteAsync(keySql,
                        new { PipelineStepId = stepId, KeyName = kv.Key, KeyValue = kv.Value }, tx);
            }
        }
    }

    private static (string? path, string? op, string? val) ExtractWhen(WhenConfig? when)
    {
        if (when is null)                        return (null, null, null);
        if (when.EqualsTo is not null)           return (when.Path, "equals",             when.EqualsTo);
        if (when.NotEquals is not null)          return (when.Path, "notEquals",          when.NotEquals);
        if (when.IsTrue is not null)             return (when.Path, "isTrue",             when.IsTrue);
        if (when.In is not null)                 return (when.Path, "in",                 when.In);
        if (when.NotIn is not null)              return (when.Path, "notIn",              when.NotIn);
        if (when.GreaterThan is not null)        return (when.Path, "greaterThan",        when.GreaterThan);
        if (when.LessThan is not null)           return (when.Path, "lessThan",           when.LessThan);
        if (when.GreaterThanOrEqual is not null) return (when.Path, "greaterThanOrEqual", when.GreaterThanOrEqual);
        if (when.LessThanOrEqual is not null)    return (when.Path, "lessThanOrEqual",    when.LessThanOrEqual);
        return (when.Path, null, null);
    }

    private static StepConfig BuildStep(StepRow s, Dictionary<int, Dictionary<string, string>> keysByStep)
    {
        keysByStep.TryGetValue(s.Id, out var keys);

        return new StepConfig
        {
            Id        = s.StepId,
            Name      = s.Name,
            Operation = s.Operation,
            RateTable = s.RateTableName,
            Keys      = keys,

            Math = s.MathType is not null
                ? new MathConfig { Type = s.MathType }
                : null,

            Compute = s.ComputeExpr is not null && s.ComputeStoreAs is not null
                ? new ComputeConfig
                  {
                      Expr           = s.ComputeExpr,
                      StoreAs        = s.ComputeStoreAs,
                      ApplyToPremium = s.ComputeApplyToPremium ?? false
                  }
                : null,

            Round = s.RoundPrecision.HasValue
                ? new RoundConfig { Precision = s.RoundPrecision.Value, Mode = s.RoundMode ?? "AwayFromZero" }
                : null,

            Interpolate = s.InterpolateKey is not null
                ? new InterpolateConfig { Key = s.InterpolateKey }
                : null,

            RangeKey = s.RangeKeyName is not null
                ? new RangeKeyConfig { Key = s.RangeKeyName }
                : null,

            When = !string.IsNullOrEmpty(s.WhenPath) && !string.IsNullOrEmpty(s.WhenOperator)
                ? BuildWhen(s.WhenPath, s.WhenOperator, s.WhenValue)
                : null
        };
    }

    private static WhenConfig BuildWhen(string path, string op, string? value) => op switch
    {
        "equals"             => new WhenConfig { Path = path, EqualsTo  = value },
        "notEquals"          => new WhenConfig { Path = path, NotEquals = value },
        "isTrue"             => new WhenConfig { Path = path, IsTrue    = value ?? "true" },
        "in"                 => new WhenConfig { Path = path, In        = value },
        "notIn"              => new WhenConfig { Path = path, NotIn     = value },
        "greaterThan"        => new WhenConfig { Path = path, GreaterThan       = value },
        "lessThan"           => new WhenConfig { Path = path, LessThan          = value },
        "greaterThanOrEqual" => new WhenConfig { Path = path, GreaterThanOrEqual = value },
        "lessThanOrEqual"    => new WhenConfig { Path = path, LessThanOrEqual    = value },
        _ => throw new InvalidOperationException($"Unknown WhenOperator '{op}'")
    };

    // ── Private row types ──────────────────────────────────────────────────────

    private sealed class CoverageRow
    {
        public int Id { get; init; }
        public string ProductCode { get; init; } = string.Empty;
        public string State { get; init; } = "*";
        public string CoverageCode { get; init; } = string.Empty;
        public string Version { get; init; } = string.Empty;
        public DateOnly EffStart { get; init; }
        public DateOnly? ExpireAt { get; init; }
        public DateTime CreatedAt { get; init; }
        public string? CreatedBy { get; init; }
        public DateTime? ModifiedAt { get; init; }
        public string? ModifiedBy { get; init; }
    }

    private sealed class StepRow
    {
        public int Id { get; init; }
        public int StepOrder { get; init; }
        public string StepId { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Operation { get; init; } = string.Empty;
        public string? RateTableName { get; init; }
        public string? MathType { get; init; }
        public string? InterpolateKey { get; init; }
        public string? RangeKeyName { get; init; }
        public string? ComputeExpr { get; init; }
        public string? ComputeStoreAs { get; init; }
        public bool? ComputeApplyToPremium { get; init; }
        public int? RoundPrecision { get; init; }
        public string? RoundMode { get; init; }
        public string? WhenPath { get; init; }
        public string? WhenOperator { get; init; }
        public string? WhenValue { get; init; }
    }

    private sealed class StepKeyRow
    {
        public int PipelineStepId { get; init; }
        public string KeyName { get; init; } = string.Empty;
        public string KeyValue { get; init; } = string.Empty;
    }
}
