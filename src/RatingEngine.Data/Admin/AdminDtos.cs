using RatingEngine.Core;

namespace RatingEngine.Data.Admin;

// ── Summary records (list responses) ──────────────────────────────────────────

public record ProductSummary(
    int Id, string ProductCode, string Version,
    DateOnly EffStart, DateOnly? ExpireAt,
    DateTime CreatedAt, string? CreatedBy);

public record CoverageSummary(
    int Id, string ProductCode, string State, string CoverageCode, string Version,
    DateOnly EffStart, DateOnly? ExpireAt,
    DateTime CreatedAt, string? CreatedBy);

public record RateTableSummary(
    int Id, int CoverageConfigId, string Name, string? Description,
    string LookupType, string? InterpolationKeyCol,
    DateOnly EffStart, DateOnly? ExpireAt,
    DateTime CreatedAt, string? CreatedBy);

// ── Detail records (single-item responses) ────────────────────────────────────

public record ProductDetail(
    int Id, string ProductCode, string Version,
    DateOnly EffStart, DateOnly? ExpireAt,
    DateTime CreatedAt, string? CreatedBy,
    DateTime? ModifiedAt, string? ModifiedBy,
    IReadOnlyList<CoverageRefDetail> Coverages);

public record CoverageRefDetail(int Id, string CoverageCode, string CoverageVersion, int SortOrder);

public record CoverageDetail(
    int Id, string ProductCode, string State, string CoverageCode, string Version,
    DateOnly EffStart, DateOnly? ExpireAt,
    DateTime CreatedAt, string? CreatedBy,
    DateTime? ModifiedAt, string? ModifiedBy,
    IReadOnlyList<string> Perils,
    IReadOnlyList<StepConfig> Pipeline);

public record RateTableDetail(
    int Id, int CoverageConfigId, string Name, string? Description,
    string LookupType, string? InterpolationKeyCol,
    DateOnly EffStart, DateOnly? ExpireAt,
    DateTime CreatedAt, string? CreatedBy,
    IReadOnlyList<ColumnDefDetail> ColumnDefs);

public record ColumnDefDetail(int Id, string ColumnName, string DisplayLabel, string DataType, int SortOrder, bool IsRequired);

public record RateTableRowDetail(
    long Id,
    string? Key1, string? Key2, string? Key3, string? Key4, string? Key5,
    decimal? RangeFrom, decimal? RangeTo,
    decimal? Factor, decimal? Additive,
    decimal? AdditionalUnit, decimal? AdditionalRate,
    DateOnly EffStart, DateOnly? ExpireAt);

// ── Request models ────────────────────────────────────────────────────────────

public record CreateProductRequest(
    string ProductCode,
    string Version,
    DateOnly EffStart,
    DateOnly? ExpireAt,
    IReadOnlyList<CoverageRefRequest> Coverages);

public record UpdateProductRequest(
    DateOnly EffStart,
    DateOnly? ExpireAt,
    IReadOnlyList<CoverageRefRequest> Coverages);

public record CoverageRefRequest(string CoverageCode, string CoverageVersion);

public record ExpireRequest(DateOnly ExpireAt);

public record CreateCoverageRequest(
    string ProductCode,
    string State,
    string CoverageCode,
    string Version,
    DateOnly EffStart,
    DateOnly? ExpireAt,
    IReadOnlyList<string> Perils,
    IReadOnlyList<StepConfig> Pipeline);

public record UpdateCoverageRequest(
    DateOnly EffStart,
    DateOnly? ExpireAt,
    IReadOnlyList<string> Perils,
    IReadOnlyList<StepConfig> Pipeline);

public record CreateRateTableRequest(
    int CoverageConfigId,
    string Name,
    string? Description,
    string LookupType,
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
    string LookupType,
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
    decimal? Factor, decimal? Additive,
    decimal? AdditionalUnit, decimal? AdditionalRate,
    DateOnly EffStart, DateOnly? ExpireAt);

public record BulkInsertRowsRequest(IReadOnlyList<CreateRateTableRowRequest> Rows);

/// <summary>Adds a step to an existing coverage pipeline.</summary>
/// <param name="InsertAfterOrder">StepOrder after which to insert. Null = append at end.</param>
public record AddPipelineStepRequest(StepConfig Step, int? InsertAfterOrder = null);

/// <summary>Reorders pipeline steps by providing the desired StepId sequence.</summary>
public record ReorderStepsRequest(IReadOnlyList<string> OrderedStepIds);
