-- =============================================================================
-- SIRatingEngine – V004 Aggregate Rating Mode and Compound When Conditions
-- Adds support for:
--   • Coverage-level aggregate rating (PolicyAggregate mode driven by a When condition)
--   • Compound When conditions (DNF: OR-of-AND-groups) for pipeline steps
-- =============================================================================

-- ── Coverage aggregate configuration ─────────────────────────────────────────
-- One row per CoverageConfig that supports aggregate rating mode.
-- When the WhenPath/Op/Value condition evaluates to true at rating time,
-- the engine merges all standard (non-SCHEDLEVEL) risks into a single aggregate
-- context before running the pipeline once instead of once per risk.
CREATE TABLE CoverageAggregateConfig (
    Id               INT          IDENTITY(1,1) PRIMARY KEY,
    CoverageConfigId INT          NOT NULL REFERENCES CoverageConfig(Id) ON DELETE CASCADE,
    WhenPath         VARCHAR(200) NOT NULL,   -- e.g. '$risk.ValuationMethod'
    WhenOp           VARCHAR(30)  NOT NULL,   -- e.g. 'eq'
    WhenValue        VARCHAR(500) NOT NULL,   -- e.g. 'RC'
    CONSTRAINT UQ_CoverageAggregateConfig UNIQUE (CoverageConfigId)
);

CREATE INDEX IX_CoverageAggregateConfig_ConfigId ON CoverageAggregateConfig (CoverageConfigId);

-- ── Aggregate field definitions ───────────────────────────────────────────────
-- Each row defines one field to aggregate across all risks in the LOB.
-- The computed value is injected into the aggregate risk bag as $risk.{ResultKey}.
-- SourceField = '*' is used with AggFunction = 'COUNT' (counts risk rows).
CREATE TABLE CoverageAggregateField (
    Id                        INT          IDENTITY(1,1) PRIMARY KEY,
    CoverageAggregateConfigId INT          NOT NULL
        REFERENCES CoverageAggregateConfig(Id) ON DELETE CASCADE,
    SourceField               VARCHAR(100) NOT NULL,
    AggFunction               VARCHAR(10)  NOT NULL DEFAULT 'SUM'
        CONSTRAINT CK_AggFunction CHECK (AggFunction IN ('SUM','AVG','MAX','MIN','COUNT')),
    ResultKey                 VARCHAR(100) NOT NULL,   -- injected as $risk.{ResultKey}
    SortOrder                 INT          NOT NULL DEFAULT 0
);

CREATE INDEX IX_CoverageAggregateField_ConfigId ON CoverageAggregateField (CoverageAggregateConfigId, SortOrder);

-- ── Compound When clauses for coverage pipeline steps ─────────────────────────
-- Implements DNF (Disjunctive Normal Form): clauses within the same GroupId are
-- ANDed; distinct GroupIds are ORed. When rows exist for a step they take
-- precedence over the legacy WhenPath / WhenOperator / WhenValue columns,
-- which remain populated for backward-compat single-predicate steps.
-- Evaluation:  result = OR of { AND of clauses where GroupId = G } for each G
CREATE TABLE PipelineStepWhenClause (
    Id             INT          IDENTITY(1,1) PRIMARY KEY,
    PipelineStepId INT          NOT NULL REFERENCES PipelineStep(Id) ON DELETE CASCADE,
    GroupId        TINYINT      NOT NULL DEFAULT 1,
    ClausePath     VARCHAR(200) NOT NULL,
    ClauseOp       VARCHAR(30)  NOT NULL,
    ClauseValue    VARCHAR(500) NOT NULL,
    SortOrder      INT          NOT NULL DEFAULT 0,
    CONSTRAINT UQ_StepWhenClause UNIQUE (PipelineStepId, GroupId, SortOrder)
);

CREATE INDEX IX_StepWhenClause_StepId ON PipelineStepWhenClause (PipelineStepId, GroupId);

-- ── Compound When clauses for policy adjustment steps ────────────────────────
-- Mirrors PipelineStepWhenClause but scoped to PolicyAdjustmentStep.
CREATE TABLE PolicyAdjustmentStepWhenClause (
    Id                     INT          IDENTITY(1,1) PRIMARY KEY,
    PolicyAdjustmentStepId INT          NOT NULL REFERENCES PolicyAdjustmentStep(Id) ON DELETE CASCADE,
    GroupId                TINYINT      NOT NULL DEFAULT 1,
    ClausePath             VARCHAR(200) NOT NULL,
    ClauseOp               VARCHAR(30)  NOT NULL,
    ClauseValue            VARCHAR(500) NOT NULL,
    SortOrder              INT          NOT NULL DEFAULT 0,
    CONSTRAINT UQ_AdjStepWhenClause UNIQUE (PolicyAdjustmentStepId, GroupId, SortOrder)
);

CREATE INDEX IX_AdjStepWhenClause_StepId ON PolicyAdjustmentStepWhenClause (PolicyAdjustmentStepId, GroupId);

-- ── Seed: aggregate result key risk fields ────────────────────────────────────
-- Registers the well-known aggregate result keys so the step-form When
-- condition path picker can offer them as labelled options.
INSERT INTO RiskField (DisplayName, Path, Description, Category, SortOrder) VALUES
    ('Total Building Value',         '$risk.TotalBuildingValue',         'Sum of BuildingValue across all risks in the LOB (aggregate mode).', 'Aggregate', 200),
    ('Total Personal Property Value','$risk.TotalPersonalPropertyValue', 'Sum of PersonalPropertyValue across all risks in the LOB (aggregate mode).', 'Aggregate', 201),
    ('Total Units',                  '$risk.TotalUnits',                 'Sum of Units across all risks in the LOB (aggregate mode).', 'Aggregate', 202),
    ('Avg Units Per Building',       '$risk.AvgUnitsPerBldg',           'Average of Units across all risks in the LOB (aggregate mode).', 'Aggregate', 203),
    ('Building Count',               '$risk.BuildingCount',             'Number of standard (non-schedule) risks in the LOB (aggregate mode).', 'Aggregate', 204),
    ('Valuation Method',             '$risk.ValuationMethod',           'Blanket, RC, or ACV — drives aggregate vs per-building rating mode.', 'Building', 28);
