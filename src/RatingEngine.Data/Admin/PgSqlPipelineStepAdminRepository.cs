using System.Data;
using Dapper;
using RatingEngine.Core;

namespace RatingEngine.Data.Admin;

public sealed class PgSqlPipelineStepAdminRepository : IPipelineStepAdminRepository
{
    private readonly DbConnectionFactory _db;
    public PgSqlPipelineStepAdminRepository(DbConnectionFactory db) => _db = db;

    public async Task<IReadOnlyList<StepConfig>> ListStepsAsync(int coverageConfigId, CancellationToken cancellationToken = default)
    {
        const string stepSql = """
            SELECT "Id", "StepOrder", "StepId", "Name", "Operation",
                   "RateTableName", "MathType", "InterpolateKey", "RangeKeyName",
                   "SourceType", "ConstantValue", "OutputAlias", "OperationScope",
                   "ComputeExpr", "ComputeStoreAs", "ComputeApplyToPremium",
                   "RoundPrecision", "RoundMode",
                   "WhenPath", "WhenOperator", "WhenValue"
            FROM "PipelineStep" WHERE "CoverageConfigId" = @ConfigId ORDER BY "StepOrder"
            """;

        const string stepKeySql = """
            SELECT "PipelineStepId", "KeyName", "KeyValue"
            FROM "PipelineStepKey" WHERE "PipelineStepId" IN @StepIds
            """;

        const string stepWhenSql = """
            SELECT "PipelineStepId", "GroupId", "ClausePath", "ClauseOp", "ClauseValue", "SortOrder"
            FROM "PipelineStepWhenClause" WHERE "PipelineStepId" IN @StepIds
            ORDER BY "PipelineStepId", "GroupId", "SortOrder"
            """;

        using var conn = _db.Create();

        var stepRows = (await conn.QueryAsync<StepRow>(new CommandDefinition(
            stepSql,
            new { ConfigId = coverageConfigId },
            cancellationToken: cancellationToken))).AsList();
        if (stepRows.Count == 0) return [];

        var stepIds = stepRows.Select(s => s.Id).ToArray();
        var keyRows  = (await conn.QueryAsync<StepKeyRow>(new CommandDefinition(stepKeySql, new { StepIds = stepIds }, cancellationToken: cancellationToken))).AsList();
        var whenRows = (await conn.QueryAsync<SqlCoverageAdminRepository.WhenClauseRow>(new CommandDefinition(stepWhenSql, new { StepIds = stepIds }, cancellationToken: cancellationToken))).AsList();

        var keysByStep = keyRows .GroupBy(k => k.PipelineStepId).ToDictionary(g => g.Key, g => g.ToDictionary(k => k.KeyName, k => k.KeyValue));
        var whenByStep = whenRows.GroupBy(w => w.PipelineStepId).ToDictionary(g => g.Key, g => g.ToList());

        return stepRows.Select(s => BuildStep(s, keysByStep, whenByStep)).ToList();
    }

    public async Task<int> AddStepAsync(int coverageConfigId, StepConfig step, int? insertAfterOrder = null, CancellationToken cancellationToken = default)
    {
        using var conn = _db.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();

        int newOrder;
        if (insertAfterOrder.HasValue)
        {
            await conn.ExecuteAsync(new CommandDefinition("""
                UPDATE "PipelineStep"
                SET "StepOrder" = "StepOrder" + 1
                WHERE "CoverageConfigId" = @ConfigId AND "StepOrder" > @AfterOrder
                """, new { ConfigId = coverageConfigId, AfterOrder = insertAfterOrder.Value }, transaction: tx, cancellationToken: cancellationToken));

            newOrder = insertAfterOrder.Value + 1;
        }
        else
        {
            var maxOrder = await conn.ExecuteScalarAsync<int?>(new CommandDefinition(
                """SELECT MAX("StepOrder") FROM "PipelineStep" WHERE "CoverageConfigId" = @ConfigId""",
                new { ConfigId = coverageConfigId },
                transaction: tx,
                cancellationToken: cancellationToken)) ?? -1;
            newOrder = maxOrder + 1;
        }

        var stepDbId = await InsertStepAsync(conn, tx, coverageConfigId, newOrder, step, cancellationToken);

        tx.Commit();
        return stepDbId;
    }

    public async Task<bool> UpdateStepAsync(int coverageConfigId, string stepId, StepConfig step, CancellationToken cancellationToken = default)
    {
        const string getIdSql = """
            SELECT "Id", "StepOrder" FROM "PipelineStep"
            WHERE "CoverageConfigId" = @ConfigId AND "StepId" = @StepId
            """;

        const string updateSql = """
            UPDATE "PipelineStep"
            SET "StepId" = @StepId, "Name" = @Name, "Operation" = @Operation,
                "RateTableName" = @RateTableName, "MathType" = @MathType,
                "InterpolateKey" = @InterpolateKey, "RangeKeyName" = @RangeKeyName,
                "SourceType" = @SourceType, "ConstantValue" = @ConstantValue,
                "OutputAlias" = @OutputAlias, "OperationScope" = @OperationScope,
                "ComputeExpr" = @ComputeExpr, "ComputeStoreAs" = @ComputeStoreAs,
                "ComputeApplyToPremium" = @ComputeApplyToPremium,
                "RoundPrecision" = @RoundPrecision, "RoundMode" = @RoundMode,
                "WhenPath" = @WhenPath, "WhenOperator" = @WhenOperator, "WhenValue" = @WhenValue
            WHERE "Id" = @Id
            """;

        const string clauseSql = """
            INSERT INTO "PipelineStepWhenClause" ("PipelineStepId", "GroupId", "ClausePath", "ClauseOp", "ClauseValue", "SortOrder")
            VALUES (@PipelineStepId, @GroupId, @ClausePath, @ClauseOp, @ClauseValue, @SortOrder)
            """;

        using var conn = _db.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();

        var existing = await conn.QueryFirstOrDefaultAsync<(int Id, int StepOrder)>(new CommandDefinition(
            getIdSql,
            new { ConfigId = coverageConfigId, StepId = stepId },
            transaction: tx,
            cancellationToken: cancellationToken));

        if (existing == default) { tx.Rollback(); return false; }

        var groups = SqlCoverageAdminRepository.ExtractWhenGroups(step.When);
        var (whenPath, whenOp, whenVal) = groups.Count == 0
            ? SqlCoverageAdminRepository.ExtractWhen(step.When)
            : (null, null, null);

        await conn.ExecuteAsync(new CommandDefinition(updateSql, new
        {
            Id                    = existing.Id,
            StepId                = step.Id,
            step.Name,
            step.Operation,
            RateTableName         = step.RateTable,
            MathType              = step.Math?.Type,
            InterpolateKey        = step.Interpolate?.Key,
            RangeKeyName          = step.RangeKey?.Key,
            step.SourceType,
            step.ConstantValue,
            step.OutputAlias,
            step.OperationScope,
            ComputeExpr           = step.Compute?.Expr,
            ComputeStoreAs        = step.Compute?.StoreAs,
            ComputeApplyToPremium = step.Compute?.ApplyToPremium,
            RoundPrecision        = step.Round?.Precision,
            RoundMode             = step.Round?.Mode,
            WhenPath              = whenPath,
            WhenOperator          = whenOp,
            WhenValue             = whenVal
        }, transaction: tx, cancellationToken: cancellationToken));

        await conn.ExecuteAsync(new CommandDefinition("""DELETE FROM "PipelineStepKey"         WHERE "PipelineStepId" = @Id""", new { existing.Id }, transaction: tx, cancellationToken: cancellationToken));
        await conn.ExecuteAsync(new CommandDefinition("""DELETE FROM "PipelineStepWhenClause"  WHERE "PipelineStepId" = @Id""", new { existing.Id }, transaction: tx, cancellationToken: cancellationToken));

        if (step.Keys is not null)
            foreach (var kv in step.Keys)
                await conn.ExecuteAsync(new CommandDefinition(
                    """INSERT INTO "PipelineStepKey" ("PipelineStepId", "KeyName", "KeyValue") VALUES (@PipelineStepId, @KeyName, @KeyValue)""",
                    new { PipelineStepId = existing.Id, KeyName = kv.Key, KeyValue = kv.Value }, transaction: tx, cancellationToken: cancellationToken));

        foreach (var (groupId, clauses) in groups)
            for (int ci = 0; ci < clauses.Count; ci++)
            {
                var (cp, co, cv) = clauses[ci];
                await conn.ExecuteAsync(new CommandDefinition(
                    clauseSql,
                    new { PipelineStepId = existing.Id, GroupId = groupId, ClausePath = cp, ClauseOp = co, ClauseValue = cv, SortOrder = ci },
                    transaction: tx,
                    cancellationToken: cancellationToken));
            }

        tx.Commit();
        return true;
    }

    public async Task<bool> DeleteStepAsync(int coverageConfigId, string stepId, CancellationToken cancellationToken = default)
    {
        const string getOrderSql = """
            SELECT "Id", "StepOrder" FROM "PipelineStep"
            WHERE "CoverageConfigId" = @ConfigId AND "StepId" = @StepId
            """;

        using var conn = _db.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();

        var existing = await conn.QueryFirstOrDefaultAsync<(int Id, int StepOrder)>(new CommandDefinition(
            getOrderSql,
            new { ConfigId = coverageConfigId, StepId = stepId },
            transaction: tx,
            cancellationToken: cancellationToken));

        if (existing == default) { tx.Rollback(); return false; }

        await conn.ExecuteAsync(new CommandDefinition("""DELETE FROM "PipelineStep" WHERE "Id" = @Id""", new { existing.Id }, transaction: tx, cancellationToken: cancellationToken));

        await conn.ExecuteAsync(new CommandDefinition("""
            UPDATE "PipelineStep"
            SET "StepOrder" = "StepOrder" - 1
            WHERE "CoverageConfigId" = @ConfigId AND "StepOrder" > @DeletedOrder
            """, new { ConfigId = coverageConfigId, DeletedOrder = existing.StepOrder }, transaction: tx, cancellationToken: cancellationToken));

        tx.Commit();
        return true;
    }

    public async Task ReorderStepsAsync(int coverageConfigId, IReadOnlyList<string> orderedStepIds, CancellationToken cancellationToken = default)
    {
        const string getAllSql = """
            SELECT "Id", "StepId", "StepOrder" FROM "PipelineStep"
            WHERE "CoverageConfigId" = @ConfigId ORDER BY "StepOrder"
            """;

        using var conn = _db.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();

        var allSteps = (await conn.QueryAsync<(int Id, string StepId, int StepOrder)>(new CommandDefinition(
            getAllSql,
            new { ConfigId = coverageConfigId },
            transaction: tx,
            cancellationToken: cancellationToken))).AsList();

        var explicitIds = new HashSet<string>(orderedStepIds, StringComparer.OrdinalIgnoreCase);
        var unlisted    = allSteps.Where(s => !explicitIds.Contains(s.StepId)).Select(s => s.StepId).ToList();
        var finalOrder  = orderedStepIds.Concat(unlisted).ToList();
        var stepLookup  = allSteps.ToDictionary(s => s.StepId, s => s.Id, StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < finalOrder.Count; i++)
            if (stepLookup.TryGetValue(finalOrder[i], out var dbId))
                await conn.ExecuteAsync(new CommandDefinition(
                    """UPDATE "PipelineStep" SET "StepOrder" = @Order WHERE "Id" = @Id""",
                    new { Order = i, Id = dbId },
                    transaction: tx,
                    cancellationToken: cancellationToken));

        tx.Commit();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    internal static async Task<int> InsertStepAsync(
        IDbConnection conn, IDbTransaction tx, int coverageConfigId, int stepOrder, StepConfig s, CancellationToken cancellationToken)
    {
        const string stepSql = """
            INSERT INTO "PipelineStep" (
                "CoverageConfigId", "StepOrder", "StepId", "Name", "Operation",
                "RateTableName", "MathType", "InterpolateKey", "RangeKeyName",
                "SourceType", "ConstantValue", "OutputAlias", "OperationScope",
                "ComputeExpr", "ComputeStoreAs", "ComputeApplyToPremium",
                "RoundPrecision", "RoundMode",
                "WhenPath", "WhenOperator", "WhenValue")
            VALUES (
                @CoverageConfigId, @StepOrder, @StepId, @Name, @Operation,
                @RateTableName, @MathType, @InterpolateKey, @RangeKeyName,
                @SourceType, @ConstantValue, @OutputAlias, @OperationScope,
                @ComputeExpr, @ComputeStoreAs, @ComputeApplyToPremium,
                @RoundPrecision, @RoundMode,
                @WhenPath, @WhenOperator, @WhenValue)
            RETURNING "Id"
            """;

        const string clauseSql = """
            INSERT INTO "PipelineStepWhenClause" ("PipelineStepId", "GroupId", "ClausePath", "ClauseOp", "ClauseValue", "SortOrder")
            VALUES (@PipelineStepId, @GroupId, @ClausePath, @ClauseOp, @ClauseValue, @SortOrder)
            """;

        var groups = SqlCoverageAdminRepository.ExtractWhenGroups(s.When);
        var (whenPath, whenOp, whenVal) = groups.Count == 0
            ? SqlCoverageAdminRepository.ExtractWhen(s.When)
            : (null, null, null);

        var stepDbId = await conn.QuerySingleAsync<int>(new CommandDefinition(stepSql, new
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
            s.SourceType,
            s.ConstantValue,
            s.OutputAlias,
            s.OperationScope,
            ComputeExpr           = s.Compute?.Expr,
            ComputeStoreAs        = s.Compute?.StoreAs,
            ComputeApplyToPremium = s.Compute?.ApplyToPremium,
            RoundPrecision        = s.Round?.Precision,
            RoundMode             = s.Round?.Mode,
            WhenPath              = whenPath,
            WhenOperator          = whenOp,
            WhenValue             = whenVal
        }, transaction: tx, cancellationToken: cancellationToken));

        if (s.Keys is not null)
            foreach (var kv in s.Keys)
                await conn.ExecuteAsync(new CommandDefinition(
                    """INSERT INTO "PipelineStepKey" ("PipelineStepId", "KeyName", "KeyValue") VALUES (@PipelineStepId, @KeyName, @KeyValue)""",
                    new { PipelineStepId = stepDbId, KeyName = kv.Key, KeyValue = kv.Value }, transaction: tx, cancellationToken: cancellationToken));

        foreach (var (groupId, clauses) in groups)
            for (int ci = 0; ci < clauses.Count; ci++)
            {
                var (cp, co, cv) = clauses[ci];
                await conn.ExecuteAsync(new CommandDefinition(
                    clauseSql,
                    new { PipelineStepId = stepDbId, GroupId = groupId, ClausePath = cp, ClauseOp = co, ClauseValue = cv, SortOrder = ci },
                    transaction: tx,
                    cancellationToken: cancellationToken));
            }

        return stepDbId;
    }

    private static StepConfig BuildStep(
        StepRow s,
        Dictionary<int, Dictionary<string, string>> keysByStep,
        Dictionary<int, List<SqlCoverageAdminRepository.WhenClauseRow>> whenByStep)
    {
        keysByStep.TryGetValue(s.Id, out var keys);
        whenByStep.TryGetValue(s.Id, out var clauseRows);

        WhenConfig? when;
        if (clauseRows is { Count: > 0 })
            when = SqlCoverageAdminRepository.BuildWhenFromClauses(clauseRows);
        else
            when = !string.IsNullOrEmpty(s.WhenPath) && !string.IsNullOrEmpty(s.WhenOperator)
                   ? SqlCoverageAdminRepository.BuildWhen(s.WhenPath, s.WhenOperator, s.WhenValue)
                   : null;

        return new StepConfig
        {
            Id             = s.StepId,
            Name           = s.Name,
            Operation      = s.Operation,
            RateTable      = s.RateTableName,
            Keys           = keys,
            Math           = s.MathType is not null ? new MathConfig { Type = s.MathType } : null,
            Compute        = s.ComputeExpr is not null && s.ComputeStoreAs is not null
                ? new ComputeConfig { Expr = s.ComputeExpr, StoreAs = s.ComputeStoreAs, ApplyToPremium = s.ComputeApplyToPremium ?? false }
                : null,
            Round          = s.RoundPrecision.HasValue ? new RoundConfig { Precision = s.RoundPrecision.Value, Mode = s.RoundMode ?? "AwayFromZero" } : null,
            Interpolate    = s.InterpolateKey is not null ? new InterpolateConfig { Key = s.InterpolateKey } : null,
            RangeKey       = s.RangeKeyName   is not null ? new RangeKeyConfig    { Key = s.RangeKeyName   } : null,
            SourceType     = s.SourceType,
            ConstantValue  = s.ConstantValue,
            OutputAlias    = s.OutputAlias,
            OperationScope = s.OperationScope,
            When           = when,
        };
    }

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
        public string? SourceType { get; init; }
        public decimal? ConstantValue { get; init; }
        public string? OutputAlias { get; init; }
        public string? OperationScope { get; init; }
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
