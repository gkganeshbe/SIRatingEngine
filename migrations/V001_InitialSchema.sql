-- =============================================================================
-- SIRatingEngine – V001 Initial Schema
-- SQL Server 2019+
-- Each tenant has its own database; no TenantId columns needed.
-- =============================================================================

-- ── Product manifest ──────────────────────────────────────────────────────────
CREATE TABLE ProductManifest (
    Id           INT           IDENTITY(1,1) PRIMARY KEY,
    ProductCode  VARCHAR(50)   NOT NULL,
    Version      VARCHAR(20)   NOT NULL,
    EffStart     DATE          NOT NULL,
    ExpireAt     DATE          NULL,
    CreatedAt    DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy    VARCHAR(100)  NULL,
    ModifiedAt   DATETIME2     NULL,
    ModifiedBy   VARCHAR(100)  NULL,
    CONSTRAINT UQ_ProductManifest UNIQUE (ProductCode, Version)
);

-- ── Line of Business (commercial products) ───────────────────────────────────
-- Groups coverages within a product under a named LOB (PROP, GL, AUTO…).
-- Personal lines products have no LOB rows; their coverages have LobId = NULL.
CREATE TABLE ProductLob (
    Id                INT          IDENTITY(1,1) PRIMARY KEY,
    ProductManifestId INT          NOT NULL REFERENCES ProductManifest(Id) ON DELETE CASCADE,
    LobCode           NVARCHAR(50) NOT NULL,
    SortOrder         INT          NOT NULL DEFAULT 0,
    INDEX IX_ProductLob_ManifestId (ProductManifestId)
);

-- ── Coverage catalog ──────────────────────────────────────────────────────────
-- "Table of contents" — what coverage types a product version offers.
-- No pipeline here; pipeline lives in CoverageConfig per state.
-- LobId = NULL means flat (personal lines); non-NULL means LOB-grouped (commercial).
CREATE TABLE CoverageRef (
    Id                INT          IDENTITY(1,1) PRIMARY KEY,
    ProductManifestId INT          NOT NULL REFERENCES ProductManifest(Id) ON DELETE CASCADE,
    LobId             INT          NULL     REFERENCES ProductLob(Id)       ON DELETE CASCADE,
    CoverageCode      VARCHAR(50)  NOT NULL,
    SortOrder         INT          NOT NULL DEFAULT 0,
    CONSTRAINT UQ_CoverageRef UNIQUE (ProductManifestId, CoverageCode)
);

-- ── Coverage configuration (state pipeline) ───────────────────────────────────
-- One row per CoverageRef × State × Version.
-- Links back to the catalog entry via CoverageRefId.
-- State = '*' is a wildcard fallback; exact state match takes priority at runtime.
CREATE TABLE CoverageConfig (
    Id             INT          IDENTITY(1,1) PRIMARY KEY,
    CoverageRefId  INT          NOT NULL REFERENCES CoverageRef(Id) ON DELETE CASCADE,
    State          VARCHAR(10)  NOT NULL DEFAULT '*',
    Version        VARCHAR(20)  NOT NULL,
    EffStart       DATE         NOT NULL,
    ExpireAt       DATE         NULL,
    CreatedAt      DATETIME2    NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy      VARCHAR(100) NULL,
    ModifiedAt     DATETIME2    NULL,
    ModifiedBy     VARCHAR(100) NULL,
    CONSTRAINT UQ_CoverageConfig UNIQUE (CoverageRefId, State, Version)
);

-- ── Perils ────────────────────────────────────────────────────────────────────
CREATE TABLE CoveragePeril (
    Id               INT         IDENTITY(1,1) PRIMARY KEY,
    CoverageConfigId INT         NOT NULL REFERENCES CoverageConfig(Id) ON DELETE CASCADE,
    PerilCode        VARCHAR(50) NOT NULL,
    SortOrder        INT         NOT NULL DEFAULT 0,
    CONSTRAINT UQ_CoveragePeril UNIQUE (CoverageConfigId, PerilCode)
);

-- ── Pipeline steps ────────────────────────────────────────────────────────────
CREATE TABLE PipelineStep (
    Id                    INT           IDENTITY(1,1) PRIMARY KEY,
    CoverageConfigId      INT           NOT NULL REFERENCES CoverageConfig(Id) ON DELETE CASCADE,
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
    CONSTRAINT UQ_PipelineStep UNIQUE (CoverageConfigId, StepId)
);

CREATE TABLE PipelineStepKey (
    Id             INT          IDENTITY(1,1) PRIMARY KEY,
    PipelineStepId INT          NOT NULL REFERENCES PipelineStep(Id) ON DELETE CASCADE,
    KeyName        VARCHAR(100) NOT NULL,
    KeyValue       VARCHAR(200) NOT NULL,
    CONSTRAINT UQ_PipelineStepKey UNIQUE (PipelineStepId, KeyName)
);

-- ── Rate tables ───────────────────────────────────────────────────────────────
CREATE TABLE RateTable (
    Id                  INT           IDENTITY(1,1) PRIMARY KEY,
    CoverageConfigId    INT           NOT NULL REFERENCES CoverageConfig(Id) ON DELETE CASCADE,
    Name                VARCHAR(100)  NOT NULL,
    Description         VARCHAR(500)  NULL,
    LookupType          VARCHAR(20)   NOT NULL DEFAULT 'EXACT',
    InterpolationKeyCol VARCHAR(20)   NULL,
    EffStart            DATE          NOT NULL,
    ExpireAt            DATE          NULL,
    CreatedAt           DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy           VARCHAR(100)  NULL,
    ModifiedAt          DATETIME2     NULL,
    ModifiedBy          VARCHAR(100)  NULL,
    CONSTRAINT UQ_RateTable UNIQUE (CoverageConfigId, Name),
    CONSTRAINT CK_LookupType CHECK (LookupType IN ('EXACT','INTERPOLATE','RANGE','WILDCARD'))
);

CREATE TABLE RateTableColumnDef (
    Id           INT           IDENTITY(1,1) PRIMARY KEY,
    RateTableId  INT           NOT NULL REFERENCES RateTable(Id) ON DELETE CASCADE,
    ColumnName   VARCHAR(20)   NOT NULL,
    DisplayLabel VARCHAR(100)  NOT NULL,
    DataType     VARCHAR(20)   NOT NULL,
    SortOrder    INT           NOT NULL DEFAULT 0,
    IsRequired   BIT           NOT NULL DEFAULT 0,
    CONSTRAINT UQ_RateTableColumnDef UNIQUE (RateTableId, ColumnName),
    CONSTRAINT CK_ColumnName CHECK (ColumnName IN (
        'Key1','Key2','Key3','Key4','Key5',
        'RangeFrom','RangeTo',
        'Factor','AdditionalUnit','AdditionalRate'))
);

CREATE TABLE RateTableRow (
    Id             BIGINT        IDENTITY(1,1) PRIMARY KEY,
    RateTableId    INT           NOT NULL REFERENCES RateTable(Id) ON DELETE CASCADE,
    Key1           VARCHAR(100)  NULL,
    Key2           VARCHAR(100)  NULL,
    Key3           VARCHAR(100)  NULL,
    Key4           VARCHAR(100)  NULL,
    Key5           VARCHAR(100)  NULL,
    RangeFrom      DECIMAL(18,4) NULL,
    RangeTo        DECIMAL(18,4) NULL,
    Factor         DECIMAL(18,6) NOT NULL DEFAULT 0,
    AdditionalUnit DECIMAL(18,4) NULL,
    AdditionalRate DECIMAL(18,6) NULL,
    EffStart       DATE          NOT NULL,
    ExpireAt       DATE          NULL
);

-- ── Risk Field Registry ───────────────────────────────────────────────────────
-- Human-readable labels for $risk.X path expressions used in pipeline steps.
-- ProductCode = NULL → global/system (visible for all products).
-- ProductCode = 'HO-PRIMARY' → visible only when configuring that product's steps.
CREATE TABLE RiskField (
    Id          INT           IDENTITY(1,1) PRIMARY KEY,
    DisplayName NVARCHAR(100) NOT NULL,
    Path        NVARCHAR(200) NOT NULL,
    Description NVARCHAR(500) NULL,
    Category    NVARCHAR(50)  NULL,
    SortOrder   INT           NOT NULL DEFAULT 0,
    ProductCode NVARCHAR(50)  NULL,
    INDEX IX_RiskField_Category    (Category),
    INDEX IX_RiskField_Path        (Path),
    INDEX IX_RiskField_ProductCode (ProductCode)
);

-- System seed data — global fields (ProductCode IS NULL)
INSERT INTO RiskField (DisplayName, Path, Description, Category, SortOrder) VALUES
    ('Current Peril', '$peril', 'The peril being rated in this pipeline pass. Set by the engine.', 'System', 0),
    ('Wildcard',      '*',      'Matches any value in this rate table key column.',                 'System', 1);

INSERT INTO RiskField (DisplayName, Path, Description, Category, SortOrder) VALUES
    ('State / Jurisdiction', '$risk.State',          'Rating state.', 'Policy', 10),
    ('Policy Form',          '$risk.PolicyForm',     'Policy form type (e.g. HO3, BOP-STD).', 'Policy', 11),
    ('Occupancy Type',       '$risk.Occupancy',      'Occupancy classification.', 'Policy', 12),
    ('Territory / Zone',     '$risk.Zone',           'Rating territory or zone code.', 'Policy', 13),
    ('Sub-Zone',             '$risk.SubZone',        'Sub-territory code.', 'Policy', 14),
    ('Claim-Free Years',     '$risk.ClaimFreeYears', 'Years without a claim.', 'Policy', 15);

INSERT INTO RiskField (DisplayName, Path, Description, Category, SortOrder) VALUES
    ('Construction Class', '$risk.Construction',    'ISO construction class (FRM, MAS…).', 'Building', 20),
    ('Year Built',         '$risk.YearBuilt',       'Four-digit year the building was constructed.', 'Building', 21),
    ('Roof Type',          '$risk.Roof',            'Roof covering material.', 'Building', 22),
    ('External Cladding',  '$risk.ExternalCladding','Exterior wall cladding type.', 'Building', 23),
    ('Protection Class',   '$risk.ProtectionClass', 'ISO fire protection class (1–10).', 'Building', 24),
    ('Sprinklered',        '$risk.Sprinklered',     'Whether the building has a sprinkler system.', 'Building', 25),
    ('Number of Stories',  '$risk.Stories',         'Number of above-ground floors.', 'Building', 26),
    ('Square Footage',     '$risk.SquareFootage',   'Total floor area in square feet.', 'Building', 27);

INSERT INTO RiskField (DisplayName, Path, Description, Category, SortOrder) VALUES
    ('City',          '$risk.City',      'City where the location is situated.', 'Location', 30),
    ('ZIP Code',      '$risk.Zip',       'Five-digit ZIP code.', 'Location', 31),
    ('Class Code',    '$risk.ClassCode', 'GL class code for the location.', 'Location', 32),
    ('Exposure Base', '$risk.Exposure',  'Exposure measure (payroll, sales, area…).', 'Location', 33);

INSERT INTO RiskField (DisplayName, Path, Description, Category, SortOrder) VALUES
    ('Coverage Limit',       '$risk.CoverageLimit',   'Coverage limit amount.', 'Coverage', 40),
    ('Deductible — Flat',    '$risk.DeductibleFlat',  'Flat dollar deductible.', 'Coverage', 41),
    ('Deductible — Percent', '$risk.DeductiblePct',   'Deductible as % of limit.', 'Coverage', 42),
    ('Occurrence Limit',     '$risk.OccurrenceLimit', 'Per-occurrence limit.', 'Coverage', 43),
    ('Aggregate Limit',      '$risk.AggregateLimit',  'Annual aggregate limit.', 'Coverage', 44),
    ('Coinsurance Pct',      '$risk.CoinsurancePct',  'Coinsurance requirement %.', 'Coverage', 45),
    ('Amount Band',          '$risk.AmountBand',      'Banded range label (e.g. >250k).', 'Coverage', 46);

INSERT INTO RiskField (DisplayName, Path, Description, Category, SortOrder) VALUES
    ('Schedule ID',         '$risk.ScheduleId',    'Unique identifier for the schedule entry.', 'Schedule Item', 50),
    ('Item Value',          '$risk.ItemValue',     'Appraised or replacement value of the item.', 'Schedule Item', 51),
    ('Item Type',           '$risk.ItemType',      'Classification of the scheduled item.', 'Schedule Item', 52),
    ('Building Limit',      '$risk.BuildingLimit', 'Replacement cost limit for a scheduled building.', 'Schedule Item', 53),
    ('BPP Limit',           '$risk.BppLimit',      'Business personal property limit.', 'Schedule Item', 54),
    ('Vehicle — Year',      '$risk.VehicleYear',   'Model year of the scheduled vehicle.', 'Schedule Item', 55),
    ('Vehicle — Make/Model','$risk.VehicleModel',  'Make and model of the scheduled vehicle.', 'Schedule Item', 56),
    ('Stated Value',        '$risk.StatedValue',   'Stated value for agreed-value scheduled items.', 'Schedule Item', 57);

-- ── Indexes ───────────────────────────────────────────────────────────────────

CREATE INDEX IX_CoverageRef_ProductManifestId ON CoverageRef (ProductManifestId);
CREATE INDEX IX_CoverageRef_LobId             ON CoverageRef (LobId);

-- Engine: resolve config by CoverageRefId + State + EffStart
CREATE INDEX IX_CoverageConfig_Lookup
    ON CoverageConfig (CoverageRefId, State, EffStart)
    INCLUDE (Id, Version, ExpireAt);

CREATE INDEX IX_CoveragePeril_CoverageConfigId ON CoveragePeril  (CoverageConfigId, SortOrder);
CREATE INDEX IX_PipelineStep_CoverageConfigId  ON PipelineStep   (CoverageConfigId, StepOrder);
CREATE INDEX IX_PipelineStepKey_StepId         ON PipelineStepKey(PipelineStepId);
CREATE INDEX IX_RateTableColumnDef_RateTableId ON RateTableColumnDef(RateTableId);

CREATE INDEX IX_RateTableRow_TableEff
    ON RateTableRow (RateTableId, EffStart)
    INCLUDE (Key1, Key2, Key3, Key4, Key5, RangeFrom, RangeTo,
             Factor, AdditionalUnit, AdditionalRate, ExpireAt);

CREATE INDEX IX_RateTableRow_Keys
    ON RateTableRow (RateTableId, Key1, Key2, Key3, EffStart)
    INCLUDE (Key4, Key5, Factor, ExpireAt);

CREATE INDEX IX_RateTable_CoverageConfigId ON RateTable (CoverageConfigId, Name);
