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
}

export interface ProductDetail extends ProductSummary {
  modifiedAt: string | null;
  modifiedBy: string | null;
  coverages: CoverageRefDetail[];
}

export interface CoverageRefDetail {
  id: number;
  coverageCode: string;
  coverageVersion: string;
  sortOrder: number;
}

export interface CoverageRefRequest {
  coverageCode: string;
  coverageVersion: string;
}

export interface CreateProductRequest {
  productCode: string;
  version: string;
  effStart: string;
  expireAt: string | null;
  coverages: CoverageRefRequest[];
}

export interface UpdateProductRequest {
  effStart: string;
  expireAt: string | null;
  coverages: CoverageRefRequest[];
}

// ── Coverages ────────────────────────────────────────────────────────────────

export interface CoverageSummary {
  id: number;
  productCode: string;
  state: string;
  coverageCode: string;
  version: string;
  effStart: string;
  expireAt: string | null;
  createdAt: string;
  createdBy: string | null;
}

export interface CoverageDetail extends CoverageSummary {
  modifiedAt: string | null;
  modifiedBy: string | null;
  perils: string[];
  pipeline: StepConfig[];
}

export interface CreateCoverageRequest {
  productCode: string;
  state: string;
  coverageCode: string;
  version: string;
  effStart: string;
  expireAt: string | null;
  perils: string[];
  pipeline: StepConfig[];
}

export interface UpdateCoverageRequest {
  effStart: string;
  expireAt: string | null;
  perils: string[];
  pipeline: StepConfig[];
}

// ── Pipeline Steps ───────────────────────────────────────────────────────────

/** Operation types supported by the rating pipeline. */
export type PipelineOperation = 'lookup' | 'compute' | 'round';

/** Math operations for lookup steps. */
export type MathType = 'set' | 'mul' | 'add' | 'sub';

export interface MathConfig {
  type: MathType;
  target?: string;  // default: "premium"
  source?: string;  // default: "Factor"
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
}

export interface AddPipelineStepRequest {
  step: StepConfig;
  insertAfterOrder?: number | null;
}

export interface ReorderStepsRequest { orderedStepIds: string[]; }

// ── Rate Tables ──────────────────────────────────────────────────────────────

export type LookupType = 'EXACT' | 'INTERPOLATE' | 'RANGE' | 'WILDCARD';

export interface RateTableSummary {
  id: number;
  coverageConfigId: number;
  name: string;
  description: string | null;
  lookupType: LookupType;
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
  factor: number | null;
  additive: number | null;
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
  lookupType: LookupType;
  interpolationKeyCol: string | null;
  effStart: string;
  expireAt: string | null;
  columnDefs: ColumnDefRequest[];
}

export interface UpdateRateTableRequest {
  description: string | null;
  lookupType: LookupType;
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
  factor: number | null;
  additive: number | null;
  additionalUnit: number | null;
  additionalRate: number | null;
  effStart: string;
  expireAt: string | null;
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
