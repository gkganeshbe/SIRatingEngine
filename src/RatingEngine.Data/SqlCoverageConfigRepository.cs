using Dapper;
using RatingEngine.Core;

namespace RatingEngine.Data;

public sealed class SqlCoverageConfigRepository : ICoverageConfigRepository
{
    private readonly DbConnectionFactory _db;
    public SqlCoverageConfigRepository(DbConnectionFactory db) => _db = db;

    public async Task<CoverageConfig?> GetAsync(string productCode, string state, string coverageCode, DateOnly effectiveDate, CancellationToken cancellationToken = default)
    {
        // Exact state match takes priority over wildcard (*); within same specificity latest EffStart wins.
        const string configSql = """
            SELECT TOP 1
                cc.Id, pm.ProductCode, cc.State, cr.CoverageCode, cc.Version,
                cr.Id AS CoverageRefId, cc.EffStart AS EffectiveStart
            FROM CoverageConfig cc
            JOIN CoverageRef cr     ON cr.Id  = cc.CoverageRefId
            JOIN ProductManifest pm ON pm.Id  = cr.ProductManifestId
            WHERE pm.ProductCode  = @ProductCode
              AND cr.CoverageCode = @CoverageCode
              AND (cc.State = @State OR cc.State = '*')
              AND cc.EffStart    <= @EffectiveDate
              AND (cc.ExpireAt IS NULL OR cc.ExpireAt > @EffectiveDate)
            ORDER BY
                CASE WHEN cc.State = @State THEN 0 ELSE 1 END,
                cc.EffStart DESC
            """;

        const string perilSql = """
            SELECT PerilCode
            FROM CoveragePeril
            WHERE CoverageConfigId = @ConfigId
            ORDER BY SortOrder
            """;

        const string stepSql = """
            SELECT Id, StepOrder, StepId, Name, Operation,
                   RateTableName, MathType, InterpolateKey, RangeKeyName,
                   SourceType, ConstantValue, OutputAlias, OperationScope,
                   ComputeExpr, ComputeStoreAs, ComputeApplyToPremium,
                   RoundPrecision, RoundMode,
                   WhenPath, WhenOperator, WhenValue
            FROM PipelineStep
            WHERE CoverageConfigId = @ConfigId
            ORDER BY StepOrder
            """;

        const string stepKeySql = """
            SELECT PipelineStepId, KeyName, KeyValue
            FROM PipelineStepKey
            WHERE PipelineStepId IN @StepIds
            """;

        using var conn = _db.Create();

        var configRow = await conn.QueryFirstOrDefaultAsync<ConfigRow>(new CommandDefinition(
            configSql,
            new { ProductCode = productCode, State = state, CoverageCode = coverageCode, EffectiveDate = effectiveDate },
            cancellationToken: cancellationToken));

        if (configRow is null) return null;

        var perils = (await conn.QueryAsync<string>(new CommandDefinition(
            perilSql,
            new { ConfigId = configRow.Id },
            cancellationToken: cancellationToken))).AsList();

        var stepRows = (await conn.QueryAsync<StepRow>(new CommandDefinition(
            stepSql,
            new { ConfigId = configRow.Id },
            cancellationToken: cancellationToken))).AsList();

        List<StepKeyRow> keyRows = [];
        if (stepRows.Count > 0)
        {
            var stepIds = stepRows.Select(s => s.Id).ToArray();
            keyRows = (await conn.QueryAsync<StepKeyRow>(new CommandDefinition(
                stepKeySql,
                new { StepIds = stepIds },
                cancellationToken: cancellationToken))).AsList();
        }

        var keysByStep = keyRows
            .GroupBy(k => k.PipelineStepId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(k => k.KeyName, k => k.KeyValue));

        var pipeline = stepRows.Select(s => BuildStepConfig(s, keysByStep)).ToList();

        return new CoverageConfig(
            configRow.ProductCode,
            configRow.State,
            configRow.CoverageCode,
            configRow.Version,
            configRow.EffectiveStart,
            perils,
            pipeline)
        {
            DbId = configRow.Id,
            CoverageRefId = configRow.CoverageRefId
        };
    }

    private static StepConfig BuildStepConfig(StepRow s, Dictionary<int, Dictionary<string, string>> keysByStep)
    {
        keysByStep.TryGetValue(s.Id, out var keys);

        MathConfig? math = s.MathType is not null
            ? new MathConfig { Type = s.MathType }
            : null;

        ComputeConfig? compute = s.ComputeExpr is not null && s.ComputeStoreAs is not null
            ? new ComputeConfig
            {
                Expr = s.ComputeExpr,
                StoreAs = s.ComputeStoreAs,
                ApplyToPremium = s.ComputeApplyToPremium ?? false
            }
            : null;

        RoundConfig? round = s.RoundPrecision.HasValue
            ? new RoundConfig
            {
                Precision = s.RoundPrecision.Value,
                Mode = s.RoundMode ?? "AwayFromZero"
            }
            : null;

        InterpolateConfig? interpolate = s.InterpolateKey is not null
            ? new InterpolateConfig { Key = s.InterpolateKey }
            : null;

        RangeKeyConfig? rangeKey = s.RangeKeyName is not null
            ? new RangeKeyConfig { Key = s.RangeKeyName }
            : null;

        WhenConfig? when = !string.IsNullOrEmpty(s.WhenPath) && !string.IsNullOrEmpty(s.WhenOperator)
            ? BuildWhen(s.WhenPath, s.WhenOperator, s.WhenValue)
            : null;

        return new StepConfig
        {
            Id            = s.StepId,
            Name          = s.Name,
            Operation     = s.Operation,
            RateTable     = s.RateTableName,
            Keys          = keys,
            Math          = math,
            Compute       = compute,
            Round         = round,
            Interpolate   = interpolate,
            RangeKey      = rangeKey,
            SourceType    = s.SourceType,
            ConstantValue = s.ConstantValue,
            OutputAlias   = s.OutputAlias,
            OperationScope = s.OperationScope,
            When          = when
        };
    }

    private static WhenConfig BuildWhen(string path, string op, string? value) => op switch
    {
        "equals"              => new WhenConfig { Path = path, EqualsTo = value },
        "notEquals"           => new WhenConfig { Path = path, NotEquals = value },
        "isTrue"              => new WhenConfig { Path = path, IsTrue = value ?? "true" },
        "in"                  => new WhenConfig { Path = path, In = value },
        "notIn"               => new WhenConfig { Path = path, NotIn = value },
        "greaterThan"         => new WhenConfig { Path = path, GreaterThan = value },
        "lessThan"            => new WhenConfig { Path = path, LessThan = value },
        "greaterThanOrEqual"  => new WhenConfig { Path = path, GreaterThanOrEqual = value },
        "lessThanOrEqual"     => new WhenConfig { Path = path, LessThanOrEqual = value },
        _ => throw new InvalidOperationException($"Unknown WhenOperator '{op}'")
    };

    // ── Private row types ──────────────────────────────────────────────────────

    private sealed class ConfigRow
    {
        public int Id { get; init; }
        public int CoverageRefId { get; init; }
        public string ProductCode { get; init; } = string.Empty;
        public string State { get; init; } = "*";
        public string CoverageCode { get; init; } = string.Empty;
        public string Version { get; init; } = string.Empty;
        public DateOnly EffectiveStart { get; init; }
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
