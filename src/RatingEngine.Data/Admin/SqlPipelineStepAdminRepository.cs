using System.Data;
using Dapper;
using RatingEngine.Core;

namespace RatingEngine.Data.Admin;

public sealed class SqlPipelineStepAdminRepository : IPipelineStepAdminRepository
{
    private readonly DbConnectionFactory _db;
    public SqlPipelineStepAdminRepository(DbConnectionFactory db) => _db = db;

    public async Task<IReadOnlyList<StepConfig>> ListStepsAsync(int coverageConfigId)
    {
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

        var stepRows = (await conn.QueryAsync<StepRow>(stepSql, new { ConfigId = coverageConfigId })).AsList();
        if (stepRows.Count == 0) return [];

        var stepIds = stepRows.Select(s => s.Id).ToArray();
        var keyRows = (await conn.QueryAsync<StepKeyRow>(stepKeySql, new { StepIds = stepIds })).AsList();

        var keysByStep = keyRows
            .GroupBy(k => k.PipelineStepId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(k => k.KeyName, k => k.KeyValue));

        return stepRows.Select(s => BuildStep(s, keysByStep)).ToList();
    }

    public async Task<int> AddStepAsync(int coverageConfigId, StepConfig step, int? insertAfterOrder = null)
    {
        using var conn = _db.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();

        int newOrder;
        if (insertAfterOrder.HasValue)
        {
            // Shift all steps after the insertion point up by 1
            await conn.ExecuteAsync("""
                UPDATE PipelineStep
                SET StepOrder = StepOrder + 1
                WHERE CoverageConfigId = @ConfigId AND StepOrder > @AfterOrder
                """, new { ConfigId = coverageConfigId, AfterOrder = insertAfterOrder.Value }, tx);

            newOrder = insertAfterOrder.Value + 1;
        }
        else
        {
            // Append at the end
            var maxOrder = await conn.ExecuteScalarAsync<int?>(
                "SELECT MAX(StepOrder) FROM PipelineStep WHERE CoverageConfigId = @ConfigId",
                new { ConfigId = coverageConfigId }, tx) ?? -1;
            newOrder = maxOrder + 1;
        }

        var stepDbId = await InsertStepAsync(conn, tx, coverageConfigId, newOrder, step);

        tx.Commit();
        return stepDbId;
    }

    public async Task<bool> UpdateStepAsync(int coverageConfigId, string stepId, StepConfig step)
    {
        const string getIdSql = """
            SELECT Id, StepOrder FROM PipelineStep
            WHERE CoverageConfigId = @ConfigId AND StepId = @StepId
            """;

        const string updateSql = """
            UPDATE PipelineStep
            SET StepId = @StepId, Name = @Name, Operation = @Operation,
                RateTableName = @RateTableName, MathType = @MathType,
                InterpolateKey = @InterpolateKey, RangeKeyName = @RangeKeyName,
                ComputeExpr = @ComputeExpr, ComputeStoreAs = @ComputeStoreAs,
                ComputeApplyToPremium = @ComputeApplyToPremium,
                RoundPrecision = @RoundPrecision, RoundMode = @RoundMode,
                WhenPath = @WhenPath, WhenOperator = @WhenOperator, WhenValue = @WhenValue
            WHERE Id = @Id
            """;

        const string deleteKeysSql = "DELETE FROM PipelineStepKey WHERE PipelineStepId = @StepDbId";
        const string insertKeySql = """
            INSERT INTO PipelineStepKey (PipelineStepId, KeyName, KeyValue)
            VALUES (@PipelineStepId, @KeyName, @KeyValue)
            """;

        using var conn = _db.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();

        var existing = await conn.QueryFirstOrDefaultAsync<(int Id, int StepOrder)>(
            getIdSql, new { ConfigId = coverageConfigId, StepId = stepId }, tx);

        if (existing == default) { tx.Rollback(); return false; }

        var (whenPath, whenOp, whenVal) = ExtractWhen(step.When);

        await conn.ExecuteAsync(updateSql, new
        {
            Id = existing.Id,
            StepId = step.Id,
            step.Name,
            step.Operation,
            RateTableName         = step.RateTable,
            MathType              = step.Math?.Type,
            InterpolateKey        = step.Interpolate?.Key,
            RangeKeyName          = step.RangeKey?.Key,
            ComputeExpr           = step.Compute?.Expr,
            ComputeStoreAs        = step.Compute?.StoreAs,
            ComputeApplyToPremium = step.Compute?.ApplyToPremium,
            RoundPrecision        = step.Round?.Precision,
            RoundMode             = step.Round?.Mode,
            WhenPath              = whenPath,
            WhenOperator          = whenOp,
            WhenValue             = whenVal
        }, tx);

        await conn.ExecuteAsync(deleteKeysSql, new { StepDbId = existing.Id }, tx);

        if (step.Keys is not null)
            foreach (var kv in step.Keys)
                await conn.ExecuteAsync(insertKeySql,
                    new { PipelineStepId = existing.Id, KeyName = kv.Key, KeyValue = kv.Value }, tx);

        tx.Commit();
        return true;
    }

    public async Task<bool> DeleteStepAsync(int coverageConfigId, string stepId)
    {
        const string getOrderSql = """
            SELECT Id, StepOrder FROM PipelineStep
            WHERE CoverageConfigId = @ConfigId AND StepId = @StepId
            """;

        using var conn = _db.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();

        var existing = await conn.QueryFirstOrDefaultAsync<(int Id, int StepOrder)>(
            getOrderSql, new { ConfigId = coverageConfigId, StepId = stepId }, tx);

        if (existing == default) { tx.Rollback(); return false; }

        await conn.ExecuteAsync("DELETE FROM PipelineStep WHERE Id = @Id", new { existing.Id }, tx);

        // Close the gap in StepOrder
        await conn.ExecuteAsync("""
            UPDATE PipelineStep
            SET StepOrder = StepOrder - 1
            WHERE CoverageConfigId = @ConfigId AND StepOrder > @DeletedOrder
            """, new { ConfigId = coverageConfigId, DeletedOrder = existing.StepOrder }, tx);

        tx.Commit();
        return true;
    }

    public async Task ReorderStepsAsync(int coverageConfigId, IReadOnlyList<string> orderedStepIds)
    {
        const string getAllSql = """
            SELECT Id, StepId, StepOrder FROM PipelineStep
            WHERE CoverageConfigId = @ConfigId ORDER BY StepOrder
            """;

        const string updateOrderSql = "UPDATE PipelineStep SET StepOrder = @Order WHERE Id = @Id";

        using var conn = _db.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();

        var allSteps = (await conn.QueryAsync<(int Id, string StepId, int StepOrder)>(
            getAllSql, new { ConfigId = coverageConfigId }, tx)).AsList();

        // Build ordered list: explicitly listed steps first, then any unlisted steps appended
        var explicitIds = new HashSet<string>(orderedStepIds, StringComparer.OrdinalIgnoreCase);
        var unlisted = allSteps
            .Where(s => !explicitIds.Contains(s.StepId))
            .Select(s => s.StepId)
            .ToList();

        var finalOrder = orderedStepIds.Concat(unlisted).ToList();

        var stepLookup = allSteps.ToDictionary(s => s.StepId, s => s.Id, StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < finalOrder.Count; i++)
        {
            if (stepLookup.TryGetValue(finalOrder[i], out var dbId))
                await conn.ExecuteAsync(updateOrderSql, new { Order = i, Id = dbId }, tx);
        }

        tx.Commit();
    }

    // ── Shared helpers (same pattern as SqlCoverageAdminRepository) ────────────

    private static async Task<int> InsertStepAsync(
        IDbConnection conn, IDbTransaction tx, int coverageConfigId, int stepOrder, StepConfig s)
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

        var (whenPath, whenOp, whenVal) = ExtractWhen(s.When);

        var stepDbId = await conn.ExecuteScalarAsync<int>(stepSql, new
        {
            CoverageConfigId      = coverageConfigId,
            StepOrder             = stepOrder,
            StepId                = s.Id,
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
            foreach (var kv in s.Keys)
                await conn.ExecuteAsync(keySql,
                    new { PipelineStepId = stepDbId, KeyName = kv.Key, KeyValue = kv.Value }, tx);

        return stepDbId;
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
            Math      = s.MathType is not null ? new MathConfig { Type = s.MathType } : null,
            Compute   = s.ComputeExpr is not null && s.ComputeStoreAs is not null
                ? new ComputeConfig { Expr = s.ComputeExpr, StoreAs = s.ComputeStoreAs, ApplyToPremium = s.ComputeApplyToPremium ?? false }
                : null,
            Round       = s.RoundPrecision.HasValue ? new RoundConfig { Precision = s.RoundPrecision.Value, Mode = s.RoundMode ?? "AwayFromZero" } : null,
            Interpolate = s.InterpolateKey is not null ? new InterpolateConfig { Key = s.InterpolateKey } : null,
            RangeKey    = s.RangeKeyName is not null ? new RangeKeyConfig { Key = s.RangeKeyName } : null,
            When        = s.WhenPath is not null && s.WhenOperator is not null
                ? BuildWhen(s.WhenPath, s.WhenOperator, s.WhenValue) : null
        };
    }

    private static WhenConfig BuildWhen(string path, string op, string? value) => op switch
    {
        "equals"             => new WhenConfig { Path = path, EqualsTo  = value },
        "notEquals"          => new WhenConfig { Path = path, NotEquals = value },
        "isTrue"             => new WhenConfig { Path = path, IsTrue    = value ?? "true" },
        "in"                 => new WhenConfig { Path = path, In        = value },
        "notIn"              => new WhenConfig { Path = path, NotIn     = value },
        "greaterThan"        => new WhenConfig { Path = path, GreaterThan        = value },
        "lessThan"           => new WhenConfig { Path = path, LessThan           = value },
        "greaterThanOrEqual" => new WhenConfig { Path = path, GreaterThanOrEqual = value },
        "lessThanOrEqual"    => new WhenConfig { Path = path, LessThanOrEqual    = value },
        _ => throw new InvalidOperationException($"Unknown WhenOperator '{op}'")
    };

    // ── Private row types ──────────────────────────────────────────────────────

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
