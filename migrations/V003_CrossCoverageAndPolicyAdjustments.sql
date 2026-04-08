-- =============================================================================
-- SIRatingEngine – V003 Cross-Coverage Dependencies and Policy Adjustments
-- Adds support for:
--   • Coverage dependencies  (CoverageConfig.DependsOn)
--   • Published risk-bag keys (CoverageConfig.Publish)
--   • Policy-level adjustment pipelines (ProductManifest.PolicyAdjustments)
-- =============================================================================

-- ── Coverage dependencies ─────────────────────────────────────────────────────
-- Lists the coverage codes that must be rated before this coverage config.
-- At runtime the engine injects cov_{DependsOnCode}_Premium into the risk bag.
CREATE TABLE CoverageDependency (
    Id               INT         IDENTITY(1,1) PRIMARY KEY,
    CoverageConfigId INT         NOT NULL REFERENCES CoverageConfig(Id) ON DELETE CASCADE,
    DependsOnCode    VARCHAR(50) NOT NULL,
    SortOrder        INT         NOT NULL DEFAULT 0,
    CONSTRAINT UQ_CoverageDependency UNIQUE (CoverageConfigId, DependsOnCode)
);

CREATE INDEX IX_CoverageDependency_ConfigId ON CoverageDependency (CoverageConfigId);

-- ── Coverage published keys ───────────────────────────────────────────────────
-- Risk-bag keys that are exported from this coverage's pipeline so that
-- downstream coverages (and policy adjustments) can read them.
-- Typical use: snapshot an intermediate premium at a specific pipeline step.
CREATE TABLE CoveragePublish (
    Id               INT          IDENTITY(1,1) PRIMARY KEY,
    CoverageConfigId INT          NOT NULL REFERENCES CoverageConfig(Id) ON DELETE CASCADE,
    PublishKey       VARCHAR(100) NOT NULL,
    SortOrder        INT          NOT NULL DEFAULT 0,
    CONSTRAINT UQ_CoveragePublish UNIQUE (CoverageConfigId, PublishKey)
);

CREATE INDEX IX_CoveragePublish_ConfigId ON CoveragePublish (CoverageConfigId);

-- ── Policy adjustment definitions ─────────────────────────────────────────────
-- Adjustment pipelines run after all coverages are rated.
-- Typical uses: multi-LOB credits, minimum premiums, cross-coverage surcharges.
CREATE TABLE PolicyAdjustment (
    Id                 INT           IDENTITY(1,1) PRIMARY KEY,
    ProductManifestId  INT           NOT NULL REFERENCES ProductManifest(Id) ON DELETE CASCADE,
    AdjustmentId       VARCHAR(100)  NOT NULL,
    Name               NVARCHAR(200) NOT NULL DEFAULT '',
    SortOrder          INT           NOT NULL DEFAULT 0,
    -- When set, rate table lookups within the pipeline use this coverage's tables.
    RateLookupCoverage VARCHAR(50)   NULL,
    CONSTRAINT UQ_PolicyAdjustment UNIQUE (ProductManifestId, AdjustmentId)
);

CREATE INDEX IX_PolicyAdjustment_ManifestId ON PolicyAdjustment (ProductManifestId, SortOrder);

-- ── Coverage codes in scope for an adjustment ────────────────────────────────
-- Empty = all coverages ($risk.ScopedTotal = PolicyTotal).
-- Non-empty = sum of listed coverage premiums = $risk.ScopedTotal.
CREATE TABLE PolicyAdjustmentAppliesTo (
    Id                 INT         IDENTITY(1,1) PRIMARY KEY,
    PolicyAdjustmentId INT         NOT NULL REFERENCES PolicyAdjustment(Id) ON DELETE CASCADE,
    CoverageCode       VARCHAR(50) NOT NULL,
    SortOrder          INT         NOT NULL DEFAULT 0,
    CONSTRAINT UQ_PolicyAdjAppliesTo UNIQUE (PolicyAdjustmentId, CoverageCode)
);

-- ── Policy adjustment pipeline steps ─────────────────────────────────────────
-- Mirrors PipelineStep but scoped to a PolicyAdjustment instead of a CoverageConfig.
-- Uses the same step DSL (lookup / compute / round) with the same field semantics.
CREATE TABLE PolicyAdjustmentStep (
    Id                    INT           IDENTITY(1,1) PRIMARY KEY,
    PolicyAdjustmentId    INT           NOT NULL REFERENCES PolicyAdjustment(Id) ON DELETE CASCADE,
    StepOrder             INT           NOT NULL,
    StepId                VARCHAR(100)  NOT NULL,
    Name                  VARCHAR(200)  NOT NULL DEFAULT '',
    Operation             VARCHAR(20)   NOT NULL,
    RateTableName         VARCHAR(100)  NULL,
    MathType              VARCHAR(10)   NULL,
    InterpolateKey        VARCHAR(50)   NULL,
    RangeKeyName          VARCHAR(50)   NULL,
    ComputeExpr           VARCHAR(500)  NULL,
    ComputeStoreAs        VARCHAR(100)  NULL,
    ComputeApplyToPremium BIT           NULL,
    RoundPrecision        INT           NULL,
    RoundMode             VARCHAR(20)   NULL,
    WhenPath              VARCHAR(200)  NULL,
    WhenOperator          VARCHAR(30)   NULL,
    WhenValue             VARCHAR(500)  NULL,
    CONSTRAINT UQ_PolicyAdjustmentStep UNIQUE (PolicyAdjustmentId, StepId)
);

CREATE INDEX IX_PolicyAdjustmentStep_AdjId ON PolicyAdjustmentStep (PolicyAdjustmentId, StepOrder);

CREATE TABLE PolicyAdjustmentStepKey (
    Id                     INT          IDENTITY(1,1) PRIMARY KEY,
    PolicyAdjustmentStepId INT          NOT NULL REFERENCES PolicyAdjustmentStep(Id) ON DELETE CASCADE,
    KeyName                VARCHAR(100) NOT NULL,
    KeyValue               VARCHAR(200) NOT NULL,
    CONSTRAINT UQ_PolicyAdjustmentStepKey UNIQUE (PolicyAdjustmentStepId, KeyName)
);

-- ── Seed: $premium as a system risk field ─────────────────────────────────────
-- Enables the step-form When condition path picker to offer $premium as an option.
-- Also seeds the common cross-coverage computed fields that pipelines reference.
INSERT INTO RiskField (DisplayName, Path, Description, Category, SortOrder) VALUES
    ('Running Premium',   '$premium',           'The current running premium at this point in the pipeline. Use in When conditions to guard min-premium or credit steps.', 'System', 2),
    ('Policy Total',      '$risk.PolicyTotal',  'Total premium across all coverages — populated in policy adjustment pipelines.', 'Policy Adjustments', 100),
    ('Scoped Total',      '$risk.ScopedTotal',  'Sum of premiums for the coverages listed in AppliesTo — the starting premium for this adjustment pipeline.', 'Policy Adjustments', 101),
    ('LOB Count',         '$risk.LobCount',     'Number of LOBs in the commercial submission — populated in policy adjustment pipelines.', 'Policy Adjustments', 102);
