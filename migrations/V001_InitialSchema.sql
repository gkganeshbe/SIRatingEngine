-- =============================================================================
-- SIRatingEngine – V001 Initial Schema
-- SQL Server 2019+
-- Each tenant has its own database; no TenantId columns needed.
-- =============================================================================

-- ── Product manifest ──────────────────────────────────────────────────────────
-- One row per product/version filing. EffStart/ExpireAt bracket the period
-- during which this product version is available for new-business quoting.

CREATE TABLE ProductManifest (
    Id           INT           IDENTITY(1,1) PRIMARY KEY,
    ProductCode  VARCHAR(50)   NOT NULL,
    Version      VARCHAR(20)   NOT NULL,
    EffStart     DATE          NOT NULL,
    ExpireAt     DATE          NULL,        -- NULL = still active
    CreatedAt    DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy    VARCHAR(100)  NULL,
    ModifiedAt   DATETIME2     NULL,
    ModifiedBy   VARCHAR(100)  NULL,
    CONSTRAINT UQ_ProductManifest UNIQUE (ProductCode, Version)
);

-- Coverages offered under a product version (ordered list).
CREATE TABLE CoverageRef (
    Id                INT          IDENTITY(1,1) PRIMARY KEY,
    ProductManifestId INT          NOT NULL REFERENCES ProductManifest(Id) ON DELETE CASCADE,
    CoverageCode      VARCHAR(50)  NOT NULL,
    CoverageVersion   VARCHAR(20)  NOT NULL,
    SortOrder         INT          NOT NULL DEFAULT 0,
    CONSTRAINT UQ_CoverageRef UNIQUE (ProductManifestId, CoverageCode)
);

-- ── Coverage configuration ────────────────────────────────────────────────────
-- Defines the rating pipeline for one coverage/version.
-- Decoupled from ProductManifest so a coverage version can be shared across
-- multiple product versions if needed.

CREATE TABLE CoverageConfig (
    Id           INT           IDENTITY(1,1) PRIMARY KEY,
    ProductCode  VARCHAR(50)   NOT NULL,
    CoverageCode VARCHAR(50)   NOT NULL,
    Version      VARCHAR(20)   NOT NULL,
    EffStart     DATE          NOT NULL,
    ExpireAt     DATE          NULL,
    CreatedAt    DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy    VARCHAR(100)  NULL,
    ModifiedAt   DATETIME2     NULL,
    ModifiedBy   VARCHAR(100)  NULL,
    CONSTRAINT UQ_CoverageConfig UNIQUE (ProductCode, CoverageCode, Version)
);

-- Perils applicable to a coverage (e.g. GRP1, GRP2, SPL, GL).
-- The engine runs the full pipeline once per peril in SortOrder.
CREATE TABLE CoveragePeril (
    Id               INT          IDENTITY(1,1) PRIMARY KEY,
    CoverageConfigId INT          NOT NULL REFERENCES CoverageConfig(Id) ON DELETE CASCADE,
    PerilCode        VARCHAR(50)  NOT NULL,
    SortOrder        INT          NOT NULL DEFAULT 0,
    CONSTRAINT UQ_CoveragePeril UNIQUE (CoverageConfigId, PerilCode)
);

-- ── Pipeline steps ────────────────────────────────────────────────────────────
-- Ordered steps within a coverage pipeline.
-- Operation = lookup | compute | round

CREATE TABLE PipelineStep (
    Id                    INT           IDENTITY(1,1) PRIMARY KEY,
    CoverageConfigId      INT           NOT NULL REFERENCES CoverageConfig(Id) ON DELETE CASCADE,
    StepOrder             INT           NOT NULL,
    StepId                VARCHAR(100)  NOT NULL,
    Name                  VARCHAR(200)  NOT NULL DEFAULT '',
    Operation             VARCHAR(20)   NOT NULL,   -- lookup | compute | round

    -- lookup fields
    RateTableName         VARCHAR(100)  NULL,
    MathType              VARCHAR(10)   NULL,        -- set | mul | add | sub
    InterpolateKey        VARCHAR(50)   NULL,        -- key column used for linear interpolation
    RangeKeyName          VARCHAR(50)   NULL,        -- risk-bag key whose value must fall within RangeFrom/RangeTo

    -- compute fields
    ComputeExpr           VARCHAR(500)  NULL,        -- e.g. "$premium * $coverage.InsuredValue / 100"
    ComputeStoreAs        VARCHAR(100)  NULL,        -- risk-bag key to store result under
    ComputeApplyToPremium BIT           NULL,        -- 1 = result becomes new running premium

    -- round fields
    RoundPrecision        INT           NULL,
    RoundMode             VARCHAR(20)   NULL,        -- AwayFromZero | ToEven

    -- conditional guard (when)
    -- WhenOperator values: equals | notEquals | isTrue | in | notIn |
    --                      greaterThan | lessThan | greaterThanOrEqual | lessThanOrEqual
    WhenPath              VARCHAR(200)  NULL,
    WhenOperator          VARCHAR(30)   NULL,
    WhenValue             VARCHAR(500)  NULL,

    CONSTRAINT UQ_PipelineStep UNIQUE (CoverageConfigId, StepId)
);

-- Key→value pairs passed to a rate table lookup (the "keys: {}" block).
-- KeyValue may be a $risk.X path, $coverage.X path, $peril, or a literal.
CREATE TABLE PipelineStepKey (
    Id             INT           IDENTITY(1,1) PRIMARY KEY,
    PipelineStepId INT           NOT NULL REFERENCES PipelineStep(Id) ON DELETE CASCADE,
    KeyName        VARCHAR(100)  NOT NULL,
    KeyValue       VARCHAR(200)  NOT NULL,
    CONSTRAINT UQ_PipelineStepKey UNIQUE (PipelineStepId, KeyName)
);

-- ── Rate tables ───────────────────────────────────────────────────────────────
-- One row in RateTable per logical rate table (e.g. "CondoBaseRate").
-- LookupType drives engine behaviour:
--   EXACT       – exact key match (wildcard "*" allowed per row)
--   INTERPOLATE – one key column holds numeric breakpoints; engine interpolates
--   RANGE       – RangeFrom/RangeTo used for one numeric dimension
--   WILDCARD    – single row applies to all inputs (Key1 = "*")

CREATE TABLE RateTable (
    Id                  INT           IDENTITY(1,1) PRIMARY KEY,
    Name                VARCHAR(100)  NOT NULL,
    Description         VARCHAR(500)  NULL,
    ProductCode         VARCHAR(50)   NULL,    -- NULL = shared across products
    LookupType          VARCHAR(20)   NOT NULL DEFAULT 'EXACT',
    InterpolationKeyCol VARCHAR(20)   NULL,    -- Key1..Key5: which column is the interp dimension
    EffStart            DATE          NOT NULL,
    ExpireAt            DATE          NULL,
    CreatedAt           DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy           VARCHAR(100)  NULL,
    ModifiedAt          DATETIME2     NULL,
    ModifiedBy          VARCHAR(100)  NULL,
    CONSTRAINT UQ_RateTable UNIQUE (Name),
    CONSTRAINT CK_LookupType CHECK (LookupType IN ('EXACT','INTERPOLATE','RANGE','WILDCARD'))
);

-- Column metadata for each rate table — drives admin-portal grid headers and
-- data-entry validation without hard-coding column meanings in application code.
CREATE TABLE RateTableColumnDef (
    Id           INT           IDENTITY(1,1) PRIMARY KEY,
    RateTableId  INT           NOT NULL REFERENCES RateTable(Id) ON DELETE CASCADE,
    ColumnName   VARCHAR(20)   NOT NULL,   -- Key1..Key5 | RangeFrom | RangeTo |
                                           -- Factor | Additive | AdditionalUnit | AdditionalRate
    DisplayLabel VARCHAR(100)  NOT NULL,   -- e.g. "Territory", "Construction Type", "Insured Value"
    DataType     VARCHAR(20)   NOT NULL,   -- string | numeric | date
    SortOrder    INT           NOT NULL DEFAULT 0,
    IsRequired   BIT           NOT NULL DEFAULT 0,
    CONSTRAINT UQ_RateTableColumnDef UNIQUE (RateTableId, ColumnName),
    CONSTRAINT CK_ColumnName CHECK (ColumnName IN (
        'Key1','Key2','Key3','Key4','Key5',
        'RangeFrom','RangeTo',
        'Factor','Additive','AdditionalUnit','AdditionalRate'))
);

-- Individual rate rows.  The engine loads all rows for a table name, filters
-- by EffStart/ExpireAt, then applies wildcard/range/interpolation matching.
CREATE TABLE RateTableRow (
    Id             BIGINT          IDENTITY(1,1) PRIMARY KEY,
    RateTableId    INT             NOT NULL REFERENCES RateTable(Id) ON DELETE CASCADE,

    -- Categorical / exact-match keys (wildcard value = "*")
    Key1           VARCHAR(100)    NULL,
    Key2           VARCHAR(100)    NULL,
    Key3           VARCHAR(100)    NULL,
    Key4           VARCHAR(100)    NULL,
    Key5           VARCHAR(100)    NULL,

    -- Numeric range bounds (used when LookupType = RANGE)
    RangeFrom      DECIMAL(18,4)   NULL,
    RangeTo        DECIMAL(18,4)   NULL,

    -- Rate values
    Factor         DECIMAL(18,6)   NULL,
    Additive       DECIMAL(18,6)   NULL,

    -- Excess / additional-unit rate:
    --   when interpolated value > highest breakpoint:
    --   result = top Factor + ((value - topBreakpoint) / AdditionalUnit) * AdditionalRate
    AdditionalUnit DECIMAL(18,4)   NULL,
    AdditionalRate DECIMAL(18,6)   NULL,

    -- Timeline — row is active when EffStart <= policyDate AND (ExpireAt IS NULL OR ExpireAt > policyDate)
    EffStart       DATE            NOT NULL,
    ExpireAt       DATE            NULL
);

-- ── Indexes ───────────────────────────────────────────────────────────────────

CREATE INDEX IX_CoverageRef_ProductManifestId
    ON CoverageRef (ProductManifestId);

CREATE INDEX IX_CoveragePeril_CoverageConfigId
    ON CoveragePeril (CoverageConfigId, SortOrder);

CREATE INDEX IX_PipelineStep_CoverageConfigId
    ON PipelineStep (CoverageConfigId, StepOrder);

CREATE INDEX IX_PipelineStepKey_StepId
    ON PipelineStepKey (PipelineStepId);

CREATE INDEX IX_RateTableColumnDef_RateTableId
    ON RateTableColumnDef (RateTableId);

-- Primary access pattern: all rows for a given table name, filtered by date.
CREATE INDEX IX_RateTableRow_TableEff
    ON RateTableRow (RateTableId, EffStart)
    INCLUDE (Key1, Key2, Key3, Key4, Key5, RangeFrom, RangeTo,
             Factor, Additive, AdditionalUnit, AdditionalRate, ExpireAt);

-- Covering index to support key-based filtering pushed down to SQL
-- (optimises the most common EXACT lookup pattern).
CREATE INDEX IX_RateTableRow_Keys
    ON RateTableRow (RateTableId, Key1, Key2, Key3, EffStart)
    INCLUDE (Key4, Key5, Factor, Additive, ExpireAt);
