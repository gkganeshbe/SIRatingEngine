// ── Shared ───────────────────────────────────────────────────────────────────

export interface ExpireRequest { expireAt: string; }

// ── Products ─────────────────────────────────────────────────────────────────

export interface ProductSummary {
  id: number;
  productCode: string;
  version: string;
  effStart: string;
  expireAt: string | null;
  createdAt: string;
  createdBy: string | null;
  /** UI Hint: 'Draft' | 'Active' | 'Expired' | 'Future' */
  status?: string; 
}

export interface ProductVersionTimeline {
  productCode: string;
  versions: ProductSummary[];
  currentVersionId: number;
}

export interface ProductDetail extends ProductSummary {
  modifiedAt: string | null;
  modifiedBy: string | null;
  notes: string | null;
  /** Flat coverage list (personal lines). Empty for commercial products. */
  coverages: CoverageRefDetail[];
  /** LOB-grouped coverages (commercial products). Empty for personal lines. */
  lobs: LobRefDetail[];
  /** Policy-level adjustment pipelines run after all coverages are rated. */
  policyAdjustments: PolicyAdjustmentDetail[];
}

export interface CoverageRefDetail {
  id: number;
  coverageCode: string;
  sortOrder: number;
  aggregationRule: string | null;
  perilRollup: string | null;
}

/** A Line of Business grouping within a commercial product manifest. */
export interface LobRefDetail {
  id: number;
  lobCode: string;
  sortOrder: number;
  coverages: CoverageRefDetail[];
}

export interface CoverageRefRequest {
  coverageCode: string;
}

export interface AddCoverageRefRequest {
  coverageCode: string;
  lobId: number | null;
  sortOrder: number;
}

export interface AddLobRequest {
  lobCode: string;
  sortOrder: number;
}

/** LOB grouping for product create/update requests (commercial products). */
export interface LobRefRequest {
  lobCode: string;
  coverages: CoverageRefRequest[];
}

export interface CreateProductRequest {
  productCode: string;
  version: string;
  effStart: string;
  expireAt: string | null;
  notes?: string | null;
  /** Flat coverage list — used for personal lines products. */
  coverages: CoverageRefRequest[];
  /** LOB-grouped coverages — used for commercial products. Overrides coverages when present. */
  lobs?: LobRefRequest[];
}

export interface UpdateProductRequest {
  effStart: string;
  expireAt: string | null;
  notes?: string | null;
  /** Flat coverage list — used for personal lines products. */
  coverages: CoverageRefRequest[];
  /** LOB-grouped coverages — used for commercial products. Overrides coverages when present. */
  lobs?: LobRefRequest[];
}

// ── Coverages ────────────────────────────────────────────────────────────────

export interface CoverageSummary {
  id: number;
  coverageRefId: number;
  productCode: string;
  state: string;
  coverageCode: string;
  version: string;
  effStart: string;
  expireAt: string | null;
  createdAt: string;
  createdBy: string | null;
}

// ── Aggregate rating configuration ────────────────────────────────────────────

export type AggFunction = 'SUM' | 'AVG' | 'MAX' | 'MIN' | 'COUNT';

export interface AggregateFieldDetail {
  id: number;
  sourceField: string;
  aggFunction: AggFunction;
  resultKey: string;
  sortOrder: number;
}

export interface AggregateConfigDetail {
  id: number;
  whenPath: string;
  whenOp: string;
  whenValue: string;
  fields: AggregateFieldDetail[];
}

export interface AggregateFieldRequest {
  sourceField: string;
  aggFunction: AggFunction;
  resultKey: string;
  sortOrder: number;
}

export interface AggregateConfigRequest {
  whenPath: string;
  whenOp: string;
  whenValue: string;
  fields: AggregateFieldRequest[];
}

export interface CoverageDetail extends CoverageSummary {
  modifiedAt: string | null;
  modifiedBy: string | null;
  notes: string | null;
  perils: string[];
  pipeline: StepConfig[];
  /** Coverage codes that must be rated before this one. */
  dependsOn: string[];
  /** Risk-bag keys exported after rating for downstream coverages. */
  publish: string[];
  /** Aggregate rating config — null means standard per-risk rating. */
  aggregate: AggregateConfigDetail | null;
}


export interface CreateCoverageRequest {
  coverageRefId: number;
  state: string;
  version: string;
  effStart: string;
  expireAt: string | null;
  notes?: string | null;
  perils: string[];
  pipeline: StepConfig[];
  dependsOn?: string[];
  publish?: string[];
  aggregate?: AggregateConfigRequest | null;
}

export interface UpdateCoverageRequest {
  effStart: string;
  expireAt: string | null;
  notes?: string | null;
  perils: string[];
  pipeline: StepConfig[];
  dependsOn?: string[];
  publish?: string[];
  aggregate?: AggregateConfigRequest | null;
}

// ── Pipeline Steps ───────────────────────────────────────────────────────────

/** Operation types supported by the rating pipeline. */
export type PipelineOperation = 'lookup' | 'compute' | 'adjustment' | 'round';

/** Math operations for lookup steps. */
export type MathType = 'set' | 'mul' | 'add' | 'sub';

export interface MathConfig {
  type: MathType;
  target?: string;  // default: "premium"
}

export interface WhenConfig {
  path?: string;
  // Note: C# serializes WhenConfig.EqualsTo as "Equals" (capital E) due to [JsonPropertyName("Equals")]
  Equals?: string;
  notEquals?: string;
  isTrue?: string;
  greaterThan?: string;
  lessThan?: string;
  greaterThanOrEqual?: string;
  lessThanOrEqual?: string;
  in?: string;
  notIn?: string;
  /** All sub-conditions must be true (AND). */
  allOf?: WhenConfig[];
  /** At least one sub-condition must be true (OR). AnyOf[AllOf[...], AllOf[...]] = DNF. */
  anyOf?: WhenConfig[];
}

export interface InterpolateConfig { key: string; }
export interface RangeKeyConfig    { key: string; }

export interface ComputeConfig {
  expr: string;
  storeAs: string;
  applyToPremium: boolean;
}

export interface RoundConfig {
  precision: number;
  mode: 'AwayFromZero' | 'ToEven';
}

/** Standardized categories for visual coloring and grouping in the UI Pipeline Editor */
export type StepCategory = 'BaseRate' | 'Modification' | 'Discount' | 'Surcharge' | 'Tax' | 'Fee' | 'Rounding' | 'Custom';

export interface StepConfig {
  id: string;
  name: string;
  operation: PipelineOperation;
  rateTable?: string;
  keys?: Record<string, string>;
  math?: MathConfig;
  when?: WhenConfig;
  interpolate?: InterpolateConfig;
  rangeKey?: RangeKeyConfig;
  compute?: ComputeConfig;
  round?: RoundConfig;
  /** For adjustment steps: 'rateTable' | 'constant' | 'stepOutput' */
  sourceType?: string;
  /** Used when sourceType = 'constant' */
  constantValue?: number;
  /** Optional name under which the step result is stored in the risk bag. */
  outputAlias?: string;
  /** 'policy' | 'coverage' | 'peril' — null means use the engine default. */
  operationScope?: string;
  /** Controls the visual indicator/color in the UI flow */
  stepCategory?: StepCategory | string;
}

export interface AddPipelineStepRequest {
  step: StepConfig;
  insertAfterOrder?: number | null;
}

export interface ReorderStepsRequest { orderedStepIds: string[]; }

// ── Rate Tables ──────────────────────────────────────────────────────────────

export type LookupType = 'EXACT' | 'INTERPOLATE' | 'RANGE' | 'WILDCARD';
export type ValueType  = 'Factor' | 'Rate' | 'FlatAmount' | 'Multiplier';

export interface RateTableSummary {
  id: number;
  coverageConfigId: number;
  name: string;
  description: string | null;
  intendedCoverage: string | null;
  lookupType: LookupType;
  valueType: ValueType;
  interpolationKeyCol: string | null;
  effStart: string;
  expireAt: string | null;
  createdAt: string;
  createdBy: string | null;
}

export interface ColumnDefDetail {
  id: number;
  columnName: string;
  displayLabel: string;
  dataType: string;
  sortOrder: number;
  isRequired: boolean;
}

export interface RateTableDetail extends RateTableSummary {
  columnDefs: ColumnDefDetail[];
}

export interface RateTableRowDetail {
  id: number;
  key1: string | null;
  key2: string | null;
  key3: string | null;
  key4: string | null;
  key5: string | null;
  rangeFrom: number | null;
  rangeTo: number | null;
  factor: number;
  additionalUnit: number | null;
  additionalRate: number | null;
  effStart: string;
  expireAt: string | null;
}

export interface ColumnDefRequest {
  columnName: string;
  displayLabel: string;
  dataType: string;
  sortOrder: number;
  isRequired: boolean;
}

export interface CreateRateTableRequest {
  coverageConfigId: number;
  name: string;
  description: string | null;
  intendedCoverage: string | null;
  lookupType: LookupType;
  valueType: ValueType;
  interpolationKeyCol: string | null;
  effStart: string;
  expireAt: string | null;
  columnDefs: ColumnDefRequest[];
}

export interface UpdateRateTableRequest {
  description: string | null;
  intendedCoverage: string | null;
  lookupType: LookupType;
  valueType: ValueType;
  interpolationKeyCol: string | null;
  expireAt: string | null;
}

export interface CreateRateTableRowRequest {
  key1: string | null;
  key2: string | null;
  key3: string | null;
  key4: string | null;
  key5: string | null;
  rangeFrom: number | null;
  rangeTo: number | null;
  factor: number;
  additionalUnit: number | null;
  additionalRate: number | null;
  effStart: string;
  expireAt: string | null;
}

export interface BulkInsertRowsRequest {
  rows: CreateRateTableRowRequest[];
}

// ── LOB Aggregation Scopes ────────────────────────────────────────────────────

export interface LobScopeDetail {
  id: number;
  lobId: number;
  scope: string;
}

export interface UpdateCoverageRefRequest {
  aggregationRule: string | null;
  perilRollup: string | null;
}

// ── Product States ───────────────────────────────────────────────────────────

export interface ProductStateDetail {
  id: number;
  productManifestId: number;
  stateCode: string;
}

export interface AddProductStateRequest {
  stateCode: string;
}

// ── Risk Field Registry ───────────────────────────────────────────────────────

/** Maps a human-readable display name to a path expression used in pipeline steps. */
export interface RiskField {
  id: number;
  displayName: string;   // "Construction Type"
  path: string;          // "$risk.Construction"
  description: string | null;
  category: string | null;  // "Policy" | "Building" | "Location" | "System"
  sortOrder: number;
  /** null = global/system (visible for all products); non-null = product-specific */
  productCode: string | null;
}

export interface CreateRiskFieldRequest {
  displayName: string;
  path: string;
  description: string | null;
  category: string | null;
  sortOrder: number;
  /** Injected server-side from the route; not sent by the client for the product-scoped POST. */
  productCode?: string | null;
}

export interface UpdateRiskFieldRequest {
  displayName: string;
  path: string;
  description: string | null;
  category: string | null;
  sortOrder: number;
  productCode?: string | null;
}

// ── When condition UI helper ─────────────────────────────────────────────────

export type WhenOperator =
  'equals' | 'notEquals' | 'isTrue' | 'greaterThan' | 'lessThan' |
  'greaterThanOrEqual' | 'lessThanOrEqual' | 'in' | 'notIn';

/** Flat UI model for the when condition — converted to/from WhenConfig by helpers. */
export interface WhenUi {
  path: string;
  operator: WhenOperator;
  value: string;
}

const OPERATOR_MAP: Record<WhenOperator, keyof WhenConfig> = {
  equals:               'Equals',
  notEquals:            'notEquals',
  isTrue:               'isTrue',
  greaterThan:          'greaterThan',
  lessThan:             'lessThan',
  greaterThanOrEqual:   'greaterThanOrEqual',
  lessThanOrEqual:      'lessThanOrEqual',
  in:                   'in',
  notIn:                'notIn',
};

export function whenUiToConfig(ui: WhenUi): WhenConfig {
  const key = OPERATOR_MAP[ui.operator];
  return { path: ui.path, [key]: ui.value };
}

export function whenConfigToUi(cfg: WhenConfig): WhenUi | null {
  if (!cfg.path) return null;
  for (const [op, key] of Object.entries(OPERATOR_MAP) as [WhenOperator, keyof WhenConfig][]) {
    if (cfg[key] !== undefined && cfg[key] !== null) {
      return { path: cfg.path, operator: op, value: cfg[key] as string };
    }
  }
  return null;
}

// ── DNF (compound When) UI model ─────────────────────────────────────────────
// Groups of clauses: clauses within a group are ANDed; groups are ORed.
// This maps directly to PipelineStepWhenClause.GroupId in SQL.

export interface WhenClause {
  path: string;
  operator: WhenOperator;
  value: string;
}

export interface WhenGroup {
  clauses: WhenClause[];
}

/** Converts a WhenConfig (simple or compound) into the DNF UI group model. */
export function whenConfigToGroups(cfg: WhenConfig | undefined | null): WhenGroup[] {
  if (!cfg) return [{ clauses: [{ path: '', operator: 'equals', value: '' }] }];

  // AnyOf at the top level → multiple groups
  if (cfg.anyOf && cfg.anyOf.length > 0) {
    return cfg.anyOf.map(sub => ({
      clauses: flattenAllOf(sub)
    }));
  }

  // AllOf or single predicate → single group
  return [{ clauses: flattenAllOf(cfg) }];
}

function flattenAllOf(cfg: WhenConfig): WhenClause[] {
  if (cfg.allOf && cfg.allOf.length > 0) {
    return cfg.allOf.map(sub => singleToClause(sub)).filter((c): c is WhenClause => c !== null);
  }
  const c = singleToClause(cfg);
  return c ? [c] : [{ path: '', operator: 'equals', value: '' }];
}

function singleToClause(cfg: WhenConfig): WhenClause | null {
  if (!cfg.path) return null;
  const ui = whenConfigToUi(cfg);
  if (!ui) return null;
  return { path: ui.path, operator: ui.operator, value: ui.value };
}

/** Converts the DNF UI group model back to a WhenConfig. */
export function groupsToWhenConfig(groups: WhenGroup[]): WhenConfig | null {
  const validGroups = groups
    .map(g => g.clauses.filter(c => c.path.trim().length > 0))
    .filter(clauses => clauses.length > 0);

  if (validGroups.length === 0) return null;

  const groupWhens = validGroups.map(clauses => {
    const clauseConfigs = clauses.map(c => whenUiToConfig({ path: c.path, operator: c.operator, value: c.value }));
    return clauseConfigs.length === 1
      ? clauseConfigs[0]
      : ({ allOf: clauseConfigs } as WhenConfig);
  });

  return groupWhens.length === 1 ? groupWhens[0] : ({ anyOf: groupWhens } as WhenConfig);
}

// ── Policy Adjustments ────────────────────────────────────────────────────────

export interface PolicyAdjustmentDetail {
  id: number;
  adjustmentId: string;
  name: string;
  sortOrder: number;
  /** Coverage code whose rate tables are available to this adjustment pipeline. Null = compute-only. */
  rateLookupCoverage: string | null;
  /** Coverage codes whose premiums sum to ScopedTotal. Empty = all coverages. */
  appliesTo: string[];
  pipeline: StepConfig[];
}

export interface CreatePolicyAdjustmentRequest {
  adjustmentId: string;
  name: string;
  sortOrder: number;
  rateLookupCoverage: string | null;
  appliesTo: string[];
  pipeline: StepConfig[];
}

export interface UpdatePolicyAdjustmentRequest {
  name: string;
  sortOrder: number;
  rateLookupCoverage: string | null;
  appliesTo: string[];
  pipeline: StepConfig[];
}

// ── Lookup Dimensions ─────────────────────────────────────────────────────────

export interface LookupDimensionSummary {
  id: number;
  productManifestId: number | null;
  name: string;
  description: string | null;
  sortOrder: number;
}

export interface LookupDimensionValueDetail {
  id: number;
  lookupDimensionId: number;
  value: string;
  displayLabel: string | null;
  sortOrder: number;
}

export interface LookupDimensionDetail extends LookupDimensionSummary {
  values: LookupDimensionValueDetail[];
}

export interface CreateLookupDimensionRequest {
  name: string;
  description: string | null;
  sortOrder: number;
}

export interface UpdateLookupDimensionRequest {
  description: string | null;
  sortOrder: number;
}

export interface CreateLookupValueRequest {
  value: string;
  displayLabel: string | null;
  sortOrder: number;
}

// ── Derived Keys ──────────────────────────────────────────────────────────────

export interface DerivedKeyDetail {
  id: number;
  productManifestId: number | null;
  name: string;
  readableName: string;
  aggFunction: string;
  sourceField: string;
  description: string | null;
}

export interface CreateDerivedKeyRequest {
  name: string;
  readableName: string;
  aggFunction: string;
  sourceField: string;
  description: string | null;
}

export interface UpdateDerivedKeyRequest {
  readableName: string;
  aggFunction: string;
  sourceField: string;
  description: string | null;
}

// ── Testing Sandbox ───────────────────────────────────────────────────────────

export interface AdminTestRateRequest {
  productCode: string;
  state: string;
  coverageCode: string;
  rateEffectiveDate: string | null;
  peril: string | null;
  startingPremium: number | null;
  risk: Record<string, string>;
}

export interface RatingTraceResult {
  stepId: string;
  stepName: string;
  rateTable: string | null;
  keys: Record<string, string> | null;
  factor: number | null;
  before: number;
  after: number;
  note: string | null;
  /** Detailed inputs used in calculation for "Explain this math" UI */
  metadata?: Record<string, string>;
  /** Human-readable representation of the math performed */
  formula?: string;
}

export interface PerilResult {
  peril: string;
  premium: number;
  trace: RatingTraceResult[];
}

export interface AdminTestRateResponse {
  productCode: string;
  state: string;
  coverageCode: string;
  version: string;
  effDate: string;
  coveragePremium: number;
  perils: PerilResult[];
}
