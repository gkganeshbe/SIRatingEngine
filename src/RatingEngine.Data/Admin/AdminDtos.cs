using RatingEngine.Core;

namespace RatingEngine.Data.Admin;

// ── Summary records (list responses) ──────────────────────────────────────────

public record ProductSummary(
    int Id, string ProductCode, string Version,
    DateOnly EffStart, DateOnly? ExpireAt,
    DateTime CreatedAt, string? CreatedBy);

public record CoverageSummary(
    int Id, int CoverageRefId, string ProductCode, string State, string CoverageCode, string Version,
    DateOnly EffStart, DateOnly? ExpireAt,
    DateTime CreatedAt, string? CreatedBy);

public record RateTableSummary(
    int Id, int CoverageConfigId, string Name, string? Description, string? IntendedCoverage,
    string LookupType, string ValueType, string? InterpolationKeyCol,
    DateOnly EffStart, DateOnly? ExpireAt,
    DateTime CreatedAt, string? CreatedBy);

// ── Detail records (single-item responses) ────────────────────────────────────

public record ProductDetail(
    int Id, string ProductCode, string Version,
    DateOnly EffStart, DateOnly? ExpireAt,
    DateTime CreatedAt, string? CreatedBy,
    DateTime? ModifiedAt, string? ModifiedBy,
    string? Notes,
    IReadOnlyList<CoverageRefDetail> Coverages)
{
    /// <summary>LOB-grouped coverages for commercial products. Empty for personal lines products.</summary>
    public IReadOnlyList<LobRefDetail> Lobs { get; init; } = Array.Empty<LobRefDetail>();
    /// <summary>Policy-level adjustment pipelines run after all coverages are rated.</summary>
    public IReadOnlyList<PolicyAdjustmentDetail> PolicyAdjustments { get; init; } = Array.Empty<PolicyAdjustmentDetail>();
}

// ── Policy Adjustment DTOs ────────────────────────────────────────────────────

public record PolicyAdjustmentDetail(
    int Id,
    string AdjustmentId,
    string Name,
    int SortOrder,
    string? RateLookupCoverage,
    IReadOnlyList<string> AppliesTo,
    IReadOnlyList<StepConfig> Pipeline);

public record CreatePolicyAdjustmentRequest(
    string AdjustmentId,
    string Name,
    int SortOrder,
    string? RateLookupCoverage,
    IReadOnlyList<string> AppliesTo,
    IReadOnlyList<StepConfig> Pipeline);

public record UpdatePolicyAdjustmentRequest(
    string Name,
    int SortOrder,
    string? RateLookupCoverage,
    IReadOnlyList<string> AppliesTo,
    IReadOnlyList<StepConfig> Pipeline);

public record CoverageRefDetail(int Id, string CoverageCode, int SortOrder,
    string? AggregationRule = null, string? PerilRollup = null);

/// <summary>A Line of Business grouping within a product manifest (commercial products).</summary>
public record LobRefDetail(int Id, string LobCode, int SortOrder, IReadOnlyList<CoverageRefDetail> Coverages);

// ── Aggregate rating DTOs ─────────────────────────────────────────────────────

public record AggregateFieldDetail(
    int Id, string SourceField, string AggFunction, string ResultKey, int SortOrder);

public record AggregateConfigDetail(
    int Id, string WhenPath, string WhenOp, string WhenValue,
    IReadOnlyList<AggregateFieldDetail> Fields);

public record AggregateFieldRequest(
    string SourceField, string AggFunction, string ResultKey, int SortOrder);

public record AggregateConfigRequest(
    string WhenPath, string WhenOp, string WhenValue,
    IReadOnlyList<AggregateFieldRequest> Fields);

public record CoverageDetail(
    int Id, int CoverageRefId, string ProductCode, string State, string CoverageCode, string Version,
    DateOnly EffStart, DateOnly? ExpireAt,
    DateTime CreatedAt, string? CreatedBy,
    DateTime? ModifiedAt, string? ModifiedBy,
    string? Notes,
    IReadOnlyList<string> Perils,
    IReadOnlyList<StepConfig> Pipeline)
{
    /// <summary>Coverage codes that must be rated before this one (populated from CoverageDependency table).</summary>
    public IReadOnlyList<string> DependsOn { get; init; } = Array.Empty<string>();
    /// <summary>Risk-bag keys exported after rating so downstream coverages can read them (CoveragePublish table).</summary>
    public IReadOnlyList<string> Publish { get; init; } = Array.Empty<string>();
    /// <summary>Aggregate rating configuration — null means standard per-risk rating.</summary>
    public AggregateConfigDetail? Aggregate { get; init; }
}

public record RateTableDetail(
    int Id, int CoverageConfigId, string Name, string? Description, string? IntendedCoverage,
    string LookupType, string ValueType, string? InterpolationKeyCol,
    DateOnly EffStart, DateOnly? ExpireAt,
    DateTime CreatedAt, string? CreatedBy,
    IReadOnlyList<ColumnDefDetail> ColumnDefs);

public record ColumnDefDetail(int Id, string ColumnName, string DisplayLabel, string DataType, int SortOrder, bool IsRequired);

public record RateTableRowDetail(
    long Id,
    string? Key1, string? Key2, string? Key3, string? Key4, string? Key5,
    decimal? RangeFrom, decimal? RangeTo,
    decimal Factor,
    decimal? AdditionalUnit, decimal? AdditionalRate,
    DateOnly EffStart, DateOnly? ExpireAt);

// ── Request models ────────────────────────────────────────────────────────────

public record CreateProductRequest(
    string ProductCode,
    string Version,
    DateOnly EffStart,
    DateOnly? ExpireAt,
    IReadOnlyList<CoverageRefRequest> Coverages,
    string? Notes = null)
{
    /// <summary>LOB-grouped coverages for commercial products. When set, Coverages is ignored.</summary>
    public IReadOnlyList<LobRefRequest> Lobs { get; init; } = Array.Empty<LobRefRequest>();
}

public record UpdateProductRequest(
    DateOnly EffStart,
    DateOnly? ExpireAt,
    IReadOnlyList<CoverageRefRequest> Coverages,
    string? Notes = null)
{
    /// <summary>LOB-grouped coverages for commercial products. When set, Coverages is ignored.</summary>
    public IReadOnlyList<LobRefRequest> Lobs { get; init; } = Array.Empty<LobRefRequest>();
}

public record CoverageRefRequest(string CoverageCode);

/// <summary>Adds a coverage type to a product's catalog (creates a CoverageRef row).</summary>
public record AddCoverageRefRequest(string CoverageCode, int? LobId, int SortOrder);

/// <summary>Adds a Line of Business to a commercial product manifest.</summary>
public record AddLobRequest(string LobCode, int SortOrder);

/// <summary>LOB grouping for product create/update requests.</summary>
public record LobRefRequest(string LobCode, IReadOnlyList<CoverageRefRequest> Coverages);

public record ExpireRequest(DateOnly ExpireAt);

public record CreateCoverageRequest(
    int CoverageRefId,
    string State,
    string Version,
    DateOnly EffStart,
    DateOnly? ExpireAt,
    IReadOnlyList<string> Perils,
    IReadOnlyList<StepConfig> Pipeline)
{
    public IReadOnlyList<string> DependsOn { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Publish   { get; init; } = Array.Empty<string>();
    public AggregateConfigRequest? Aggregate { get; init; }
    public string? Notes { get; init; }
}

public record UpdateCoverageRequest(
    DateOnly EffStart,
    DateOnly? ExpireAt,
    IReadOnlyList<string> Perils,
    IReadOnlyList<StepConfig> Pipeline)
{
    public IReadOnlyList<string> DependsOn { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Publish   { get; init; } = Array.Empty<string>();
    public AggregateConfigRequest? Aggregate { get; init; }
    public string? Notes { get; init; }
}

public record CreateRateTableRequest(
    int CoverageConfigId,
    string Name,
    string? Description,
    string? IntendedCoverage,
    string LookupType,
    string ValueType,
    string? InterpolationKeyCol,
    DateOnly EffStart,
    DateOnly? ExpireAt,
    IReadOnlyList<ColumnDefRequest> ColumnDefs);

/// <summary>
/// Mutable fields for an existing rate table.
/// <para>
/// <b>EffStart is intentionally excluded.</b> Once a rate table is created its
/// effective-start date is considered immutable — it forms part of the filing
/// record and changing it retroactively could invalidate quotes already rated
/// against it. To change EffStart, expire the current table and create a new one.
/// </para>
/// </summary>
public record UpdateRateTableRequest(
    string? Description,
    string? IntendedCoverage,
    string LookupType,
    string ValueType,
    string? InterpolationKeyCol,
    DateOnly? ExpireAt);

public record ColumnDefRequest(
    string ColumnName,
    string DisplayLabel,
    string DataType,
    int SortOrder,
    bool IsRequired);

public record CreateRateTableRowRequest(
    string? Key1, string? Key2, string? Key3, string? Key4, string? Key5,
    decimal? RangeFrom, decimal? RangeTo,
    decimal Factor,
    decimal? AdditionalUnit, decimal? AdditionalRate,
    DateOnly EffStart, DateOnly? ExpireAt);

public record BulkInsertRowsRequest(IReadOnlyList<CreateRateTableRowRequest> Rows);

// ── Product States ────────────────────────────────────────────────────────────

public record ProductStateDetail(int Id, int ProductManifestId, string StateCode);
public record AddProductStateRequest(string StateCode);

// ── LOB Aggregation Scopes ────────────────────────────────────────────────────

public record LobScopeDetail(int Id, int LobId, string Scope);
public record AddLobScopeRequest(string Scope);
public record UpdateCoverageRefRequest(string? AggregationRule, string? PerilRollup);

// ── Lookup Dimensions ─────────────────────────────────────────────────────────

public record LookupDimensionSummary(int Id, int? ProductManifestId, string Name, string? Description, int SortOrder);
public record LookupDimensionDetail(int Id, int? ProductManifestId, string Name, string? Description, int SortOrder,
    IReadOnlyList<LookupDimensionValueDetail> Values);
public record LookupDimensionValueDetail(int Id, int LookupDimensionId, string Value, string? DisplayLabel, int SortOrder);

public record CreateLookupDimensionRequest(string Name, string? Description, int SortOrder);
public record UpdateLookupDimensionRequest(string? Description, int SortOrder);
public record CreateLookupValueRequest(string Value, string? DisplayLabel, int SortOrder);

// ── Derived Keys ──────────────────────────────────────────────────────────────

public record DerivedKeyDetail(int Id, int? ProductManifestId, string Name, string ReadableName,
    string AggFunction, string SourceField, string? Description);

public record CreateDerivedKeyRequest(string Name, string ReadableName, string AggFunction,
    string SourceField, string? Description);

public record UpdateDerivedKeyRequest(string ReadableName, string AggFunction,
    string SourceField, string? Description);

/// <summary>Adds a step to an existing coverage pipeline.</summary>
/// <param name="InsertAfterOrder">StepOrder after which to insert. Null = append at end.</param>
public record AddPipelineStepRequest(StepConfig Step, int? InsertAfterOrder = null);

/// <summary>Reorders pipeline steps by providing the desired StepId sequence.</summary>
public record ReorderStepsRequest(IReadOnlyList<string> OrderedStepIds);

// ── Risk Field Registry ───────────────────────────────────────────────────────
// Maps human-readable display names to path expressions (e.g. "$risk.Construction").
// Business users configure this registry once; the step configuration UI then
// offers these as labelled dropdown/autocomplete options instead of raw JSON paths.

public record RiskField(
    int Id,
    string DisplayName,
    string Path,
    string? Description,
    string? Category,
    int SortOrder,
    string? ProductCode);

/// <param name="ProductCode">
/// Null = global/system (visible for all products).
/// Non-null = visible only when configuring steps for this specific product.
/// </param>
public record CreateRiskFieldRequest(
    string DisplayName,
    string Path,
    string? Description,
    string? Category,
    int SortOrder,
    string? ProductCode = null);

public record UpdateRiskFieldRequest(
    string DisplayName,
    string Path,
    string? Description,
    string? Category,
    int SortOrder,
    string? ProductCode = null);
