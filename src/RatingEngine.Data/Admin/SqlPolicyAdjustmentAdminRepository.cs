using System.Data;
using Dapper;
using RatingEngine.Core;

namespace RatingEngine.Data.Admin;

public sealed class SqlPolicyAdjustmentAdminRepository : IPolicyAdjustmentAdminRepository
{
    private readonly DbConnectionFactory _db;
    public SqlPolicyAdjustmentAdminRepository(DbConnectionFactory db) => _db = db;

    public async Task<IReadOnlyList<PolicyAdjustmentDetail>> ListAsync(int productManifestId, CancellationToken cancellationToken = default)
    {
        using var conn = _db.Create();
        return await LoadAdjustmentsAsync(conn, productManifestId, cancellationToken);
    }

    public async Task<PolicyAdjustmentDetail?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, AdjustmentId, Name, SortOrder, RateLookupCoverage
            FROM PolicyAdjustment WHERE Id = @Id
            """;

        using var conn = _db.Create();
        var row = await conn.QueryFirstOrDefaultAsync<AdjRow>(new CommandDefinition(
            sql,
            new { Id = id },
            cancellationToken: cancellationToken));
        if (row is null) return null;

        return await BuildDetailAsync(conn, row, cancellationToken);
    }

    public async Task<int> CreateAsync(int productManifestId, CreatePolicyAdjustmentRequest req, string? actor = null, CancellationToken cancellationToken = default)
    {
        const string insertSql = """
            INSERT INTO PolicyAdjustment (ProductManifestId, AdjustmentId, Name, SortOrder, RateLookupCoverage)
            OUTPUT INSERTED.Id
            VALUES (@ProductManifestId, @AdjustmentId, @Name, @SortOrder, @RateLookupCoverage)
            """;

        using var conn = _db.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();

        var id = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            insertSql,
            new { ProductManifestId = productManifestId, req.AdjustmentId, req.Name, req.SortOrder, req.RateLookupCoverage },
            transaction: tx,
            cancellationToken: cancellationToken));

        await InsertAppliesToAsync(conn, tx, id, req.AppliesTo, cancellationToken);
        await InsertStepsAsync(conn, tx, id, req.Pipeline, cancellationToken);

        tx.Commit();
        return id;
    }

    public async Task<bool> UpdateAsync(int id, UpdatePolicyAdjustmentRequest req, string? actor = null, CancellationToken cancellationToken = default)
    {
        const string updateSql = """
            UPDATE PolicyAdjustment
            SET Name = @Name, SortOrder = @SortOrder, RateLookupCoverage = @RateLookupCoverage
            WHERE Id = @Id
            """;

        using var conn = _db.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();

        var affected = await conn.ExecuteAsync(new CommandDefinition(
            updateSql,
            new { Id = id, req.Name, req.SortOrder, req.RateLookupCoverage },
            transaction: tx,
            cancellationToken: cancellationToken));

        if (affected == 0) { tx.Rollback(); return false; }

        await conn.ExecuteAsync(new CommandDefinition("DELETE FROM PolicyAdjustmentAppliesTo WHERE PolicyAdjustmentId = @Id", new { Id = id }, transaction: tx, cancellationToken: cancellationToken));
        await conn.ExecuteAsync(new CommandDefinition("DELETE FROM PolicyAdjustmentStep       WHERE PolicyAdjustmentId = @Id", new { Id = id }, transaction: tx, cancellationToken: cancellationToken));

        await InsertAppliesToAsync(conn, tx, id, req.AppliesTo, cancellationToken);
        await InsertStepsAsync(conn, tx, id, req.Pipeline, cancellationToken);

        tx.Commit();
        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        using var conn = _db.Create();
        return await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM PolicyAdjustment WHERE Id = @Id",
            new { Id = id },
            cancellationToken: cancellationToken)) > 0;
    }

    // ── Internal helpers ───────────────────────────────────────────────────────

    /// Loads all adjustments for a product manifest, including their appliesTo and pipelines.
    internal static async Task<IReadOnlyList<PolicyAdjustmentDetail>> LoadAdjustmentsAsync(
        IDbConnection conn, int productManifestId, CancellationToken cancellationToken = default)
    {
        const string adjSql = """
            SELECT Id, AdjustmentId, Name, SortOrder, RateLookupCoverage
            FROM PolicyAdjustment
            WHERE ProductManifestId = @ProductManifestId
            ORDER BY SortOrder
            """;

        var rows = (await conn.QueryAsync<AdjRow>(new CommandDefinition(
            adjSql,
            new { ProductManifestId = productManifestId },
            cancellationToken: cancellationToken))).AsList();
        if (rows.Count == 0) return Array.Empty<PolicyAdjustmentDetail>();

        var ids = rows.Select(r => r.Id).ToArray();

        const string appliesToSql = """
            SELECT PolicyAdjustmentId, CoverageCode FROM PolicyAdjustmentAppliesTo
            WHERE PolicyAdjustmentId IN @Ids ORDER BY SortOrder
            """;
        const string stepSql = """
            SELECT Id, PolicyAdjustmentId, StepOrder, StepId, Name, Operation,
                   RateTableName, MathType, InterpolateKey, RangeKeyName,
                   ComputeExpr, ComputeStoreAs, ComputeApplyToPremium,
                   RoundPrecision, RoundMode,
                   WhenPath, WhenOperator, WhenValue
            FROM PolicyAdjustmentStep WHERE PolicyAdjustmentId IN @Ids ORDER BY PolicyAdjustmentId, StepOrder
            """;
        const string stepKeySql = """
            SELECT PolicyAdjustmentStepId AS PipelineStepId, KeyName, KeyValue
            FROM PolicyAdjustmentStepKey WHERE PolicyAdjustmentStepId IN @StepIds
            """;

        var appliesToRows = (await conn.QueryAsync<AppliesToRow>(new CommandDefinition(appliesToSql, new { Ids = ids }, cancellationToken: cancellationToken))).AsList();
        var stepRows      = (await conn.QueryAsync<AdjStepRow>(new CommandDefinition(stepSql, new { Ids = ids }, cancellationToken: cancellationToken))).AsList();

        List<StepKeyRow> keyRows = [];
        if (stepRows.Count > 0)
        {
            var stepIds = stepRows.Select(s => s.Id).ToArray();
            keyRows = (await conn.QueryAsync<StepKeyRow>(new CommandDefinition(stepKeySql, new { StepIds = stepIds }, cancellationToken: cancellationToken))).AsList();
        }

        var appliesToByAdj = appliesToRows
            .GroupBy(r => r.PolicyAdjustmentId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(r => r.CoverageCode).ToList());

        var stepsByAdj = stepRows
            .GroupBy(s => s.PolicyAdjustmentId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var keysByStep = keyRows
            .GroupBy(k => k.PipelineStepId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(k => k.KeyName, k => k.KeyValue));

        return rows.Select(r =>
        {
            appliesToByAdj.TryGetValue(r.Id, out var appliesTo);
            stepsByAdj.TryGetValue(r.Id, out var steps);
            var pipeline = steps?.Select(s => BuildStep(s, keysByStep)).ToList()
                           ?? new List<StepConfig>();
            return new PolicyAdjustmentDetail(
                r.Id, r.AdjustmentId, r.Name, r.SortOrder, r.RateLookupCoverage,
                appliesTo ?? Array.Empty<string>(), pipeline);
        }).ToList();
    }

    private static async Task<PolicyAdjustmentDetail> BuildDetailAsync(IDbConnection conn, AdjRow row, CancellationToken cancellationToken)
    {
        var details = await LoadAdjustmentsForIds(conn, new[] { row.Id }, cancellationToken);
        return details[0];
    }

    private static async Task<IReadOnlyList<PolicyAdjustmentDetail>> LoadAdjustmentsForIds(
        IDbConnection conn, int[] ids, CancellationToken cancellationToken)
    {
        const string appliesToSql = """
            SELECT PolicyAdjustmentId, CoverageCode FROM PolicyAdjustmentAppliesTo
            WHERE PolicyAdjustmentId IN @Ids ORDER BY SortOrder
            """;
        const string stepSql = """
            SELECT Id, PolicyAdjustmentId, StepOrder, StepId, Name, Operation,
                   RateTableName, MathType, InterpolateKey, RangeKeyName,
                   ComputeExpr, ComputeStoreAs, ComputeApplyToPremium,
                   RoundPrecision, RoundMode, WhenPath, WhenOperator, WhenValue
            FROM PolicyAdjustmentStep WHERE PolicyAdjustmentId IN @Ids ORDER BY PolicyAdjustmentId, StepOrder
            """;
        const string stepKeySql = """
            SELECT PolicyAdjustmentStepId AS PipelineStepId, KeyName, KeyValue
            FROM PolicyAdjustmentStepKey WHERE PolicyAdjustmentStepId IN @StepIds
            """;
        const string adjSql = """
            SELECT Id, AdjustmentId, Name, SortOrder, RateLookupCoverage
            FROM PolicyAdjustment WHERE Id IN @Ids ORDER BY SortOrder
            """;

        var rows = (await conn.QueryAsync<AdjRow>(new CommandDefinition(adjSql, new { Ids = ids }, cancellationToken: cancellationToken))).AsList();
        var appliesToRows = (await conn.QueryAsync<AppliesToRow>(new CommandDefinition(appliesToSql, new { Ids = ids }, cancellationToken: cancellationToken))).AsList();
        var stepRows      = (await conn.QueryAsync<AdjStepRow>(new CommandDefinition(stepSql, new { Ids = ids }, cancellationToken: cancellationToken))).AsList();

        List<StepKeyRow> keyRows = [];
        if (stepRows.Count > 0)
        {
            var stepIds = stepRows.Select(s => s.Id).ToArray();
            keyRows = (await conn.QueryAsync<StepKeyRow>(new CommandDefinition(stepKeySql, new { StepIds = stepIds }, cancellationToken: cancellationToken))).AsList();
        }

        var appliesToByAdj = appliesToRows
            .GroupBy(r => r.PolicyAdjustmentId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(r => r.CoverageCode).ToList());

        var stepsByAdj = stepRows
            .GroupBy(s => s.PolicyAdjustmentId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var keysByStep = keyRows
            .GroupBy(k => k.PipelineStepId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(k => k.KeyName, k => k.KeyValue));

        return rows.Select(r =>
        {
            appliesToByAdj.TryGetValue(r.Id, out var appliesTo);
            stepsByAdj.TryGetValue(r.Id, out var steps);
            var pipeline = steps?.Select(s => BuildStep(s, keysByStep)).ToList() ?? new List<StepConfig>();
            return new PolicyAdjustmentDetail(
                r.Id, r.AdjustmentId, r.Name, r.SortOrder, r.RateLookupCoverage,
                appliesTo ?? Array.Empty<string>(), pipeline);
        }).ToList();
    }

    private static async Task InsertAppliesToAsync(
        IDbConnection conn, IDbTransaction tx, int adjId, IReadOnlyList<string> codes, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO PolicyAdjustmentAppliesTo (PolicyAdjustmentId, CoverageCode, SortOrder)
            VALUES (@PolicyAdjustmentId, @CoverageCode, @SortOrder)
            """;
        for (int i = 0; i < codes.Count; i++)
            await conn.ExecuteAsync(new CommandDefinition(sql, new { PolicyAdjustmentId = adjId, CoverageCode = codes[i], SortOrder = i }, transaction: tx, cancellationToken: cancellationToken));
    }

    private static async Task InsertStepsAsync(
        IDbConnection conn, IDbTransaction tx, int adjId, IReadOnlyList<StepConfig> steps, CancellationToken cancellationToken)
    {
        const string stepSql = """
            INSERT INTO PolicyAdjustmentStep (
                PolicyAdjustmentId, StepOrder, StepId, Name, Operation,
                RateTableName, MathType, InterpolateKey, RangeKeyName,
                ComputeExpr, ComputeStoreAs, ComputeApplyToPremium,
                RoundPrecision, RoundMode,
                WhenPath, WhenOperator, WhenValue)
            OUTPUT INSERTED.Id
            VALUES (
                @PolicyAdjustmentId, @StepOrder, @StepId, @Name, @Operation,
                @RateTableName, @MathType, @InterpolateKey, @RangeKeyName,
                @ComputeExpr, @ComputeStoreAs, @ComputeApplyToPremium,
                @RoundPrecision, @RoundMode,
                @WhenPath, @WhenOperator, @WhenValue)
            """;

        const string keySql = """
            INSERT INTO PolicyAdjustmentStepKey (PolicyAdjustmentStepId, KeyName, KeyValue)
            VALUES (@PolicyAdjustmentStepId, @KeyName, @KeyValue)
            """;

        for (int i = 0; i < steps.Count; i++)
        {
            var s = steps[i];
            var (whenPath, whenOp, whenVal) = ExtractWhen(s.When);

            var stepId = await conn.ExecuteScalarAsync<int>(new CommandDefinition(stepSql, new
            {
                PolicyAdjustmentId    = adjId,
                StepOrder             = i,
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
            }, transaction: tx, cancellationToken: cancellationToken));

            if (s.Keys is not null)
                foreach (var kv in s.Keys)
                    await conn.ExecuteAsync(new CommandDefinition(
                        keySql,
                        new { PolicyAdjustmentStepId = stepId, KeyName = kv.Key, KeyValue = kv.Value },
                        transaction: tx,
                        cancellationToken: cancellationToken));
        }
    }

    // ── Shared with SqlCoverageAdminRepository / PgSql implementations ────────

    /// <summary>Exposed as internal static so PostgreSQL sibling can reuse.</summary>
    internal static (string? path, string? op, string? val) ExtractWhenStatic(WhenConfig? when)
        => ExtractWhen(when);

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

    private static StepConfig BuildStep(AdjStepRow s, Dictionary<int, Dictionary<string, string>> keysByStep)
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
            Round       = s.RoundPrecision.HasValue
                        ? new RoundConfig { Precision = s.RoundPrecision.Value, Mode = s.RoundMode ?? "AwayFromZero" }
                        : null,
            Interpolate = s.InterpolateKey is not null ? new InterpolateConfig { Key = s.InterpolateKey } : null,
            RangeKey    = s.RangeKeyName   is not null ? new RangeKeyConfig    { Key = s.RangeKeyName   } : null,
            When        = !string.IsNullOrEmpty(s.WhenPath) && !string.IsNullOrEmpty(s.WhenOperator)
                        ? BuildWhen(s.WhenPath, s.WhenOperator, s.WhenValue)
                        : null
        };
    }

    private static WhenConfig BuildWhen(string path, string op, string? value) => op switch
    {
        "equals"             => new WhenConfig { Path = path, EqualsTo           = value },
        "notEquals"          => new WhenConfig { Path = path, NotEquals          = value },
        "isTrue"             => new WhenConfig { Path = path, IsTrue             = value ?? "true" },
        "in"                 => new WhenConfig { Path = path, In                 = value },
        "notIn"              => new WhenConfig { Path = path, NotIn              = value },
        "greaterThan"        => new WhenConfig { Path = path, GreaterThan        = value },
        "lessThan"           => new WhenConfig { Path = path, LessThan           = value },
        "greaterThanOrEqual" => new WhenConfig { Path = path, GreaterThanOrEqual = value },
        "lessThanOrEqual"    => new WhenConfig { Path = path, LessThanOrEqual    = value },
        _ => throw new InvalidOperationException($"Unknown WhenOperator '{op}'")
    };

    // ── Row types ──────────────────────────────────────────────────────────────

    private sealed class AdjRow
    {
        public int Id { get; init; }
        public string AdjustmentId { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public int SortOrder { get; init; }
        public string? RateLookupCoverage { get; init; }
    }

    private sealed class AppliesToRow
    {
        public int PolicyAdjustmentId { get; init; }
        public string CoverageCode { get; init; } = string.Empty;
    }

    private sealed class AdjStepRow
    {
        public int Id { get; init; }
        public int PolicyAdjustmentId { get; init; }
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
