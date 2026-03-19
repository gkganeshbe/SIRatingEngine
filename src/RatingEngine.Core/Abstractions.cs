
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
    bool ShouldExecute(RateContext ctx);
    RateResult Execute(RateContext ctx, IRateLookup lookup);
}

public record RateResult(decimal NewPremium, RatingTrace Trace);

public record RatingTrace(
    string StepId,
    string StepName,
    string? RateTable,
    IReadOnlyDictionary<string,string>? Keys,
    decimal? Factor,
    decimal Before,
    decimal After,
    string? Note
);

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
    Task<ProductManifest?> GetAsync(string productCode, string version);
}

public interface ICoverageConfigRepository
{
    Task<CoverageConfig?> GetAsync(string productCode, string coverageCode, string version);
}

public record ProductManifest(string ProductCode, string Version, DateOnly EffectiveStart, IReadOnlyList<CoverageRef> Coverages);
public record CoverageRef(string CoverageCode, string Version);
public record CoverageConfig(string ProductCode, string CoverageCode, string Version, DateOnly EffectiveStart, IReadOnlyList<string> Perils, IReadOnlyList<StepConfig> Pipeline);

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
    DateOnly EffectiveDate,
    IReadOnlyDictionary<string, string> Property,
    IReadOnlyList<CoverageInput> Coverages
);

// One temporal segment at the coverage level.
public record CoverageSegment(
    DateOnly From,
    DateOnly To,
    DateOnly EffectiveDate,
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

// ── Pipeline step configuration ─────────────────────────────────────────────

public record StepConfig
{
    public required string Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public required string Operation { get; init; } // lookup | compute | round
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
    public string Source { get; init; } = "Factor"; // Factor or Additive
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
/// Path resolves a $risk.&lt;Key&gt; or $peril value.
/// Exactly one condition operator should be set.
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
}

public record RoundConfig
{
    public int Precision { get; init; } = 2;
    public string Mode { get; init; } = "AwayFromZero"; // ToEven, AwayFromZero
}
