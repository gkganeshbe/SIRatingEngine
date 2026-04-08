
using System.Text.Json.Serialization;

namespace RatingEngine.Core;

// ---------------------------------------------------------------------------
// Risk bag – replaces the old typed PropertyRisk + Risk records.
// The engine is now fully LOB-agnostic: any set of string key/value pairs
// can describe a risk, no code change required for new attributes.
// ---------------------------------------------------------------------------

/// <summary>
/// Merges a property snapshot and coverage-specific params into one flat
/// dictionary that the pipeline can reference via $risk.&lt;Key&gt; paths.
/// Coverage params take precedence on key collision.
/// </summary>
public static class RiskBag
{
    public static Dictionary<string, string> Merge(
        IReadOnlyDictionary<string, string> property,
        IReadOnlyDictionary<string, string> coverageParams)
    {
        var bag = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in property)       bag[kv.Key] = kv.Value;
        foreach (var kv in coverageParams) bag[kv.Key] = kv.Value; // params win
        return bag;
    }
}

// ---------------------------------------------------------------------------
// RateContext – the immutable snapshot passed through a pipeline run.
// Risk is a mutable dict so that ComputeStep can write derived values that
// later steps can read via $risk.<Key>.
// ---------------------------------------------------------------------------
public record RateContext(
    string ProductCode,
    string Version,
    DateOnly EffectiveDate,
    string Jurisdiction,
    Dictionary<string, string> Risk,
    string Peril,
    decimal Premium
);

public interface IRatingStep
{
    string Id { get; }
    string Name { get; }
    string? Category { get; }
    bool ShouldExecute(RateContext ctx);
    RateResult Execute(RateContext ctx, IRateLookup lookup);
}

/// <summary>
/// Represents the result of a single pipeline step.
/// </summary>
public record RateResult(decimal NewPremium, RatingTrace Trace);

/// <summary>
/// Detailed audit trail for a rating step. 
/// Enhanced with Metadata to support UI "Explain this Math" features.
/// </summary>
public record RatingTrace(
    string StepId,
    string StepName,
    string? RateTable,
    IReadOnlyDictionary<string,string>? Keys,
    decimal? Factor,
    decimal Before,
    decimal After,
    string? Note,
    string? Category = null)
{
    /// <summary>
    /// The raw values retrieved from the risk bag or external lookups used in this step.
    /// Key: The logical name (e.g. "Amount of Insurance"), Value: The actual value used (e.g. "250000").
    /// This allows the UI to show exactly what data drove the calculation.
    /// </summary>
    public Dictionary<string, string> Metadata { get; init; } = new();
    public string? Formula { get; init; }
}

public interface IRateLookup
{
    decimal GetFactor(string rateTable, IReadOnlyDictionary<string,string> keys, DateOnly effDate);
    decimal GetInterpolatedFactor(string rateTable, IReadOnlyDictionary<string,string> keys, string interpolationKey, DateOnly effDate);
    /// <summary>
    /// Looks up a factor where one dimension is a numeric range (RangeFrom ≤ value ≤ RangeTo).
    /// All keys must match exactly (or wildcard), and rangeValue must fall within the row's range.
    /// </summary>
    decimal GetRangeKeyFactor(string rateTable, IReadOnlyDictionary<string,string> keys, string rangeKey, decimal rangeValue, DateOnly effDate);
}

public interface IPipelineFactory
{
    IReadOnlyList<IRatingStep> Build(IReadOnlyList<StepConfig> steps);
}

public interface IProductManifestRepository
{
    /// <summary>Returns the latest active manifest for <paramref name="productCode"/> whose EffectiveStart ≤ <paramref name="effectiveDate"/>.</summary>
    Task<ProductManifest?> GetAsync(string productCode, DateOnly effectiveDate, CancellationToken cancellationToken = default);
}

public interface ICoverageConfigRepository
{
    /// <summary>
    /// Returns the latest active coverage config for the given product/state/coverage whose
    /// EffectiveStart ≤ <paramref name="effectiveDate"/>. Exact state match takes priority over wildcard (*).
    /// </summary>
    Task<CoverageConfig?> GetAsync(string productCode, string state, string coverageCode, DateOnly effectiveDate, CancellationToken cancellationToken = default);
}

public interface IRateLookupFactory
{
    /// <summary>Creates or retrieves a rate lookup scoped to the given coverage config.</summary>
    IRateLookup CreateForCoverage(CoverageConfig coverage);
}

public record ProductManifest(string ProductCode, string Version, DateOnly EffectiveStart, IReadOnlyList<CoverageRef> Coverages)
{
    /// <summary>LOB-grouped coverages for commercial products. Empty for personal lines products.</summary>
    public IReadOnlyList<LobRef> Lobs { get; init; } = Array.Empty<LobRef>();

    /// <summary>
    /// All coverage codes across all LOBs (commercial) or the flat Coverages list (personal lines).
    /// Use this when validating coverage membership without caring about LOB structure.
    /// </summary>
    public IReadOnlyList<CoverageRef> AllCoverages =>
        Lobs.Count > 0 ? Lobs.SelectMany(l => l.Coverages).ToList() : Coverages;

    /// <summary>
    /// Policy-level adjustments run after all coverages are rated.
    /// Typical uses: multi-LOB credit factors, LOB minimum premiums, cross-coverage surcharges.
    /// Each adjustment receives a pre-populated risk bag with PolicyTotal, LobCount,
    /// cov_{code}_Premium, lob_{code}_Premium, ScopedTotal, and any values published
    /// by coverage pipelines via their Publish lists.
    /// The adjustment pipeline starts with $premium = ScopedTotal and produces an adjusted total;
    /// adjustmentAmount = adjustedTotal - scopedTotal (negative = credit, positive = surcharge).
    /// </summary>
    public IReadOnlyList<PolicyAdjustmentConfig> PolicyAdjustments { get; init; } = Array.Empty<PolicyAdjustmentConfig>();
}

/// <summary>
/// Configures a single policy-level adjustment step applied after all coverages are rated.
/// </summary>
public record PolicyAdjustmentConfig
{
    public required string Id { get; init; }
    public string Name { get; init; } = string.Empty;
    /// <summary>
    /// Coverage codes whose premiums are summed to produce $risk.ScopedTotal.
    /// Empty list means all coverages ($risk.ScopedTotal == $risk.PolicyTotal).
    /// </summary>
    public IReadOnlyList<string> AppliesTo { get; init; } = Array.Empty<string>();
    /// <summary>
    /// Coverage code whose rate lookup tables are available to this adjustment pipeline.
    /// When null, only compute/round steps are supported (no rate table lookups).
    /// </summary>
    public string? RateLookupCoverage { get; init; }
    public required IReadOnlyList<StepConfig> Pipeline { get; init; }
}

public record CoverageRef(string CoverageCode);

/// <summary>Groups coverages under a Line of Business within a commercial product manifest.</summary>
public record LobRef(string LobCode, IReadOnlyList<CoverageRef> Coverages);

public record CoverageConfig(string ProductCode, string State, string CoverageCode, string Version, DateOnly EffectiveStart, IReadOnlyList<string> Perils, IReadOnlyList<StepConfig> Pipeline)
{
    /// <summary>Database primary key — populated by SQL repositories; null for file-based configs.</summary>
    public int? DbId { get; init; }
    /// <summary>FK to CoverageRef — populated by SQL repositories; null for file-based configs.</summary>
    public int? CoverageRefId { get; init; }
    /// <summary>
    /// Coverage codes that must be rated before this one. After each dependency is rated its final
    /// premium is injected into this coverage's risk bag as $risk.cov_{code}_Premium, and any keys
    /// the dependency listed in its Publish property are also available via $risk.{key}.
    /// </summary>
    public IReadOnlyList<string> DependsOn { get; init; } = Array.Empty<string>();
    /// <summary>
    /// Risk bag keys to export after this coverage is rated so that downstream coverages
    /// (listed in their own DependsOn) can read them via $risk.{key}.
    /// Typical use: snapshot an intermediate premium after a base-rate step.
    /// </summary>
    public IReadOnlyList<string> Publish { get; init; } = Array.Empty<string>();
    /// <summary>
    /// When set, the engine switches to aggregate mode for this coverage whenever
    /// the AggregateConfig.When condition is true. All standard (non-SCHEDLEVEL)
    /// risks are collapsed into one merged context with the specified fields summed/
    /// averaged, and the pipeline runs once producing a single coverage premium.
    /// </summary>
    public AggregateConfig? Aggregate { get; init; }
}

// ── Segment / coverage input models ────────────────────────────────────────────

// A coverage entry with its identifier, display name, and coverage-specific
// rating parameters (e.g. CoverageA, AmountBand, DeductiblePct, DeductibleFlat).
// Params is open-ended: any key understood by that coverage's pipeline is valid.
//
// RatingType controls how the premium is derived for this coverage:
//   "NORMAL"     – single pass: policy risk merged with coverage Params (default)
//   "SCHEDLEVEL" – one pass per entry in Schedules[]; each schedule's fields are
//                  merged on top of the policy risk + coverage Params, and the
//                  individual premiums are summed to produce the coverage total.
//                  Use this for any schedule-level rating (blanket buildings,
//                  scheduled equipment, individual locations, etc.).
// RatingType is per-coverage: coverages within the same request can differ.
public record CoverageInput(
    string Id,
    string Name,
    IReadOnlyDictionary<string, string> Params
)
{
    public string RatingType { get; init; } = "NORMAL";
    public IReadOnlyList<IReadOnlyDictionary<string, string>>? Schedules { get; init; }
};

// One temporal segment at the policy level.
// Property is a free-form dict — any LOB-specific risk attributes go here.
public record PolicySegment(
    DateOnly From,
    DateOnly To,
    DateOnly RateEffectiveDate,
    IReadOnlyDictionary<string, string> Property,
    IReadOnlyList<CoverageInput> Coverages
);

// One temporal segment at the coverage level.
public record CoverageSegment(
    DateOnly From,
    DateOnly To,
    DateOnly RateEffectiveDate,
    IReadOnlyDictionary<string, string> Property,
    IReadOnlyDictionary<string, string> Params
);

// A coverage with its own independent segment timeline.
public record CoverageLevelInput(
    string Id,
    string Name,
    IReadOnlyList<CoverageSegment> Segments
)
{
    public string RatingType { get; init; } = "NORMAL";
    public IReadOnlyList<IReadOnlyDictionary<string, string>>? Schedules { get; init; }
};

// ── Commercial multi-LOB input models ────────────────────────────────────────

/// <summary>
/// A rated risk unit (building, location, or policy-level) in a commercial submission.
/// RiskLevel signals the hierarchy tier: "BUILDING", "LOCATION", or "POLICY".
/// LocationId optionally links a building risk to its parent location.
/// Each risk declares exactly which coverages apply to it — so a building that does not
/// carry BPP simply omits it from its Coverages list.
/// </summary>
public record CommercialRiskInput(
    string RiskId,
    string RiskLevel,
    IReadOnlyDictionary<string, string> Attributes,
    IReadOnlyList<CoverageInput> Coverages
)
{
    public string? LocationId { get; init; }
}

/// <summary>
/// A Line of Business within a commercial policy.
/// LobRisk holds attributes shared by every risk in this LOB (e.g. OccupancyType for PROP).
/// The merge chain is: PolicyRisk → LobRisk → Risk.Attributes → CoverageParams → ScheduleFields.
/// </summary>
public record LobInput(
    string LobCode,
    IReadOnlyDictionary<string, string> LobRisk,
    IReadOnlyList<CommercialRiskInput> Risks
);

// ── Pipeline step configuration ─────────────────────────────────────────────

public record StepConfig
{
    public required string Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public required string Operation { get; init; } // lookup | compute | adjustment | round
    public string? RateTable { get; init; }
    public Dictionary<string,string>? Keys { get; init; }
    public MathConfig? Math { get; init; }
    public WhenConfig? When { get; init; }
    public RoundConfig? Round { get; init; }
    public InterpolateConfig? Interpolate { get; init; }
    public ComputeConfig? Compute { get; init; }
    /// <summary>
    /// When set, the lookup selects rows where risk[$key] falls within [RangeFrom, RangeTo].
    /// Use this for range-based lookups such as deductible schedules keyed on building limit.
    /// </summary>
    public RangeKeyConfig? RangeKey { get; init; }
    /// <summary>
    /// For adjustment steps: 'rateTable' | 'constant' | 'stepOutput'.
    /// When null, falls back to 'rateTable' if RateTable is set.
    /// </summary>
    public string? SourceType { get; init; }
    /// <summary>Used when SourceType = 'constant'. The fixed value applied by the math operation.</summary>
    public decimal? ConstantValue { get; init; }
    /// <summary>
    /// Optional name under which the step's result is stored in the risk bag.
    /// When set, the result is accessible as $risk.{OutputAlias} in downstream steps.
    /// </summary>
    public string? OutputAlias { get; init; }
    /// <summary>
    /// Controls what portion of the rating context the step operates on.
    /// 'policy' | 'coverage' | 'peril' — null means use the engine default.
    /// </summary>
    public string? OperationScope { get; init; }
    /// <summary>
    /// UI Hint to categorize steps (e.g., 'BaseRate', 'Modification', 'Tax', 'Rounding').
    /// Allows the Admin UI to color-code or group steps in the pipeline view.
    /// </summary>
    public string? StepCategory { get; init; }
}

public record InterpolateConfig
{
    public required string Key { get; init; }
}

/// <summary>
/// Configures a range-based key dimension for a lookup step.
/// The risk bag value at Key is compared numerically against each row's RangeFrom/RangeTo.
/// </summary>
public record RangeKeyConfig
{
    /// <summary>Name of the risk bag field whose value must fall within [RangeFrom, RangeTo].</summary>
    public required string Key { get; init; }
}

public record MathConfig
{
    public required string Type { get; init; } // set | add | sub | mul | noop
    public string Target { get; init; } = "premium";
}

/// <summary>
/// Configuration for a compute step that derives a value and stores it in
/// the risk bag so subsequent pipeline steps can reference it via $risk.&lt;StoreAs&gt;.
/// </summary>
public record ComputeConfig
{
    /// <summary>
    /// Arithmetic expression. Operands: $risk.&lt;Key&gt;, $premium, or decimal literals.
    /// Operators (+, -, *, /) must be surrounded by spaces. Evaluated left-to-right.
    /// Example: "$premium * $risk.ReplacementCost / 100"
    /// </summary>
    public required string Expr { get; init; }

    /// <summary>The key under which the result is stored in the risk bag.</summary>
    public required string StoreAs { get; init; }

    /// <summary>
    /// When true, the pipeline premium is also updated to the computed value.
    /// Use for final premium calculations such as multiplying a rate by a coverage limit.
    /// </summary>
    public bool ApplyToPremium { get; init; } = false;
}

/// <summary>
/// Conditional guard evaluated before a pipeline step executes.
/// Supports three modes:
///   • Single predicate: set Path + one operator property (e.g. EqualsTo).
///   • AllOf: all sub-conditions must be true (AND composition).
///   • AnyOf: at least one sub-condition must be true (OR composition).
/// AnyOf[AllOf[...], AllOf[...]] expresses full DNF (OR of AND groups).
/// </summary>
public record WhenConfig
{
    /// <summary>Path to the value being tested, e.g. $risk.hasPool</summary>
    public string? Path { get; init; }

    // ── equality / boolean ──────────────────────────────────────────────────
    [JsonPropertyName("Equals")]
    public string? EqualsTo { get; init; }
    public string? NotEquals { get; init; }
    /// <summary>Resolves to bool "True" / "False".</summary>
    public string? IsTrue { get; init; }

    // ── numeric comparisons ──────────────────────────────────────────────────
    public string? GreaterThan { get; init; }
    public string? LessThan { get; init; }
    public string? GreaterThanOrEqual { get; init; }
    public string? LessThanOrEqual { get; init; }

    // ── set membership ───────────────────────────────────────────────────────
    /// <summary>Comma-separated list of accepted values (case-insensitive).</summary>
    public string? In { get; init; }
    /// <summary>Comma-separated list of rejected values (case-insensitive).</summary>
    public string? NotIn { get; init; }

    // ── compound composition ─────────────────────────────────────────────────
    /// <summary>All sub-conditions must be true (AND). Overrides single-predicate fields.</summary>
    public IReadOnlyList<WhenConfig>? AllOf { get; init; }
    /// <summary>At least one sub-condition must be true (OR). Overrides single-predicate fields.</summary>
    public IReadOnlyList<WhenConfig>? AnyOf { get; init; }
}

// ── Aggregate rating configuration ───────────────────────────────────────────

/// <summary>
/// Defines one field to aggregate across all standard (non-SCHEDLEVEL) risks
/// in the LOB before running the pipeline in aggregate mode.
/// The result is injected into the merged risk bag as $risk.{ResultKey}.
/// </summary>
public record AggregateFieldConfig
{
    /// <summary>Risk attribute name to aggregate, or "*" for COUNT.</summary>
    public required string SourceField { get; init; }
    /// <summary>Aggregation function: SUM | AVG | MAX | MIN | COUNT</summary>
    public string Function { get; init; } = "SUM";
    /// <summary>Key injected into the aggregate risk bag as $risk.{ResultKey}.</summary>
    public required string ResultKey { get; init; }
}

/// <summary>
/// When the When condition evaluates to true at rating time the engine switches
/// to aggregate mode for this coverage: all standard risks are merged into one
/// context (with fields aggregated per Fields), and the pipeline runs once.
/// SCHEDLEVEL risks are always rated individually regardless of this setting.
/// </summary>
public record AggregateConfig
{
    public required WhenConfig When { get; init; }
    public required IReadOnlyList<AggregateFieldConfig> Fields { get; init; }
}

public record RoundConfig
{
    public int Precision { get; init; } = 2;
    public string Mode { get; init; } = "AwayFromZero"; // ToEven, AwayFromZero
}
