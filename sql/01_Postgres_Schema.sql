-- ==============================================================================
-- SI Rating Engine - PostgreSQL Schema Setup
-- All identifiers are quoted so names are stored exactly as written (PascalCase).
-- PostgreSQL folds unquoted names to lowercase; quoting prevents that.
-- ==============================================================================

-- 1. Products & Hierarchy
CREATE TABLE "Product" (
    "Id"         SERIAL PRIMARY KEY,
    "ProductCode" VARCHAR(100) NOT NULL,
    "Version"    VARCHAR(50)  NOT NULL,
    "EffStart"   DATE         NOT NULL,
    "ExpireAt"   DATE,
    "Notes"      TEXT,
    "CreatedAt"  TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    "CreatedBy"  VARCHAR(255),
    "ModifiedAt" TIMESTAMP WITH TIME ZONE,
    "ModifiedBy" VARCHAR(255),
    UNIQUE ("ProductCode", "Version")
);

CREATE TABLE "ProductState" (
    "Id"          SERIAL PRIMARY KEY,
    "ProductCode" VARCHAR(100) NOT NULL,
    "ProductId"   INT          REFERENCES "Product"("Id") ON DELETE CASCADE,
    "StateCode"   VARCHAR(10)  NOT NULL,
    UNIQUE ("ProductCode", "StateCode")
);

-- Commercial LOB Support
CREATE TABLE "ProductLob" (
    "Id"        SERIAL PRIMARY KEY,
    "ProductId" INT          NOT NULL REFERENCES "Product"("Id") ON DELETE CASCADE,
    "LobCode"   VARCHAR(100) NOT NULL,
    "SortOrder" INT          NOT NULL DEFAULT 0,
    UNIQUE ("ProductId", "LobCode")
);

CREATE TABLE "LobScope" (
    "Id"             SERIAL PRIMARY KEY,
    "LobCode"        VARCHAR(100) NOT NULL,
    "PermittedScope" VARCHAR(100) NOT NULL,
    UNIQUE ("LobCode", "PermittedScope")
);

CREATE TABLE "CoverageRef" (
    "Id"               SERIAL PRIMARY KEY,
    "ProductId"        INT          NOT NULL REFERENCES "Product"("Id") ON DELETE CASCADE,
    "LobId"            INT          REFERENCES "ProductLob"("Id") ON DELETE CASCADE,
    "CoverageCode"     VARCHAR(100) NOT NULL,
    "SortOrder"        INT          NOT NULL DEFAULT 0,
    "CoverageVersion"  VARCHAR(50)  DEFAULT '',
    "AggregationRule"  VARCHAR(100),
    "PerilRollup"      VARCHAR(100)
);

CREATE INDEX "IX_CoverageRef_ProductId" ON "CoverageRef"("ProductId");
CREATE INDEX "IX_CoverageRef_LobId"     ON "CoverageRef"("LobId");

-- 2. Coverage Configuration
CREATE TABLE "CoverageConfig" (
    "Id"              SERIAL PRIMARY KEY,
    "ProductCode"     VARCHAR(100) NOT NULL,
    "State"           VARCHAR(10)  NOT NULL,
    "CoverageCode"    VARCHAR(100) NOT NULL,
    "Version"         VARCHAR(50)  NOT NULL,
    "EffStart"        DATE         NOT NULL,
    "ExpireAt"        DATE,
    "Perils"          JSONB,
    "DependsOn"       JSONB,
    "Publish"         JSONB,
    "AggregateConfig" JSONB,
    "Notes"           TEXT,
    "CreatedAt"       TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    "CreatedBy"       VARCHAR(255),
    "ModifiedAt"      TIMESTAMP WITH TIME ZONE,
    "ModifiedBy"      VARCHAR(255),
    UNIQUE ("ProductCode", "State", "CoverageCode", "Version")
);

-- 3. Rate Tables & Lookups
CREATE TABLE "RateTable" (
    "Id"                  SERIAL PRIMARY KEY,
    "CoverageConfigId"    INT          NOT NULL REFERENCES "CoverageConfig"("Id") ON DELETE CASCADE,
    "Name"                VARCHAR(255) NOT NULL,
    "Description"         TEXT,
    "LookupType"          VARCHAR(50)  NOT NULL DEFAULT 'exact',
    "InterpolationKeyCol" VARCHAR(100),
    "EffStart"            DATE         NOT NULL,
    "ExpireAt"            DATE,
    "CreatedAt"           TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    "CreatedBy"           VARCHAR(255),
    UNIQUE ("CoverageConfigId", "Name")
);

CREATE INDEX "IX_RateTable_CoverageConfigId" ON "RateTable"("CoverageConfigId");

CREATE TABLE "ColumnDef" (
    "Id"           SERIAL PRIMARY KEY,
    "RateTableId"  INT          NOT NULL REFERENCES "RateTable"("Id") ON DELETE CASCADE,
    "ColumnName"   VARCHAR(100) NOT NULL,
    "DisplayLabel" VARCHAR(255) NOT NULL,
    "DataType"     VARCHAR(50)  NOT NULL,
    "SortOrder"    INT          NOT NULL DEFAULT 0,
    "IsRequired"   BOOLEAN      NOT NULL DEFAULT TRUE,
    UNIQUE ("RateTableId", "ColumnName")
);

CREATE INDEX "IX_ColumnDef_RateTableId" ON "ColumnDef"("RateTableId");

CREATE TABLE "RateTableRow" (
    "Id"             SERIAL PRIMARY KEY,
    "RateTableId"    INT          NOT NULL REFERENCES "RateTable"("Id") ON DELETE CASCADE,
    "Key1"           VARCHAR(255),
    "Key2"           VARCHAR(255),
    "Key3"           VARCHAR(255),
    "Key4"           VARCHAR(255),
    "Key5"           VARCHAR(255),
    "RangeFrom"      NUMERIC(18,6),
    "RangeTo"        NUMERIC(18,6),
    "Factor"         NUMERIC(18,6),
    "Additive"       NUMERIC(18,6),
    "AdditionalRate" NUMERIC(18,6),
    "AdditionalUnit" NUMERIC(18,6),
    "EffStart"       DATE         NOT NULL,
    "ExpireAt"       DATE
);

CREATE INDEX "IX_RateTableRow_RateTableId" ON "RateTableRow"("RateTableId");
CREATE INDEX "IX_RateTableRow_EffStart"    ON "RateTableRow"("EffStart");

-- 4. Pipelines & Steps
CREATE TABLE "PipelineStep" (
    "Id"               SERIAL PRIMARY KEY,
    "CoverageConfigId" INT          NOT NULL REFERENCES "CoverageConfig"("Id") ON DELETE CASCADE,
    "StepId"           VARCHAR(100) NOT NULL,
    "Name"             VARCHAR(255) NOT NULL,
    "Operation"        VARCHAR(50)  NOT NULL,
    "StepOrder"        INT          NOT NULL,

    -- Step Details
    "RateTable"        VARCHAR(255),
    "StepCategory"     VARCHAR(100),
    "OperationScope"   VARCHAR(100),
    "SourceType"       VARCHAR(50),
    "ConstantValue"    NUMERIC(18,6),
    "OutputAlias"      VARCHAR(100),

    -- Legacy When Fields (preserved for backward compatibility)
    "WhenPath"         VARCHAR(500),
    "WhenOperator"     VARCHAR(50),
    "WhenValue"        VARCHAR(255),

    -- JSONB Structured Configurations
    "KeysConfig"       JSONB,
    "MathConfig"       JSONB,
    "ComputeConfig"    JSONB,
    "RoundConfig"      JSONB,
    "InterpolateConfig" JSONB,
    "RangeKeyConfig"   JSONB,
    "WhenClause"       JSONB,

    UNIQUE ("CoverageConfigId", "StepId")
);

CREATE INDEX "IX_PipelineStep_CoverageConfigId" ON "PipelineStep"("CoverageConfigId");
CREATE INDEX "IX_PipelineStep_StepOrder"        ON "PipelineStep"("CoverageConfigId", "StepOrder");

-- 5. Admin & UI Domain Entities
CREATE TABLE "RiskField" (
    "Id"          SERIAL PRIMARY KEY,
    "LogicalName" VARCHAR(255) NOT NULL,
    "JsonPath"    VARCHAR(500) NOT NULL,
    "DataType"    VARCHAR(50)  NOT NULL,
    "Category"    VARCHAR(100),
    UNIQUE ("LogicalName")
);

CREATE TABLE "LookupDimension" (
    "Id"            SERIAL PRIMARY KEY,
    "Name"          VARCHAR(255) NOT NULL,
    "Description"   TEXT,
    "AllowedValues" JSONB        NOT NULL,
    UNIQUE ("Name")
);

CREATE TABLE "DerivedKey" (
    "Id"          SERIAL PRIMARY KEY,
    "Name"        VARCHAR(255) NOT NULL,
    "Expression"  TEXT         NOT NULL,
    "Description" TEXT,
    UNIQUE ("Name")
);

-- 6. Policy Adjustments
CREATE TABLE "PolicyAdjustment" (
    "Id"                 SERIAL PRIMARY KEY,
    "ProductId"          INT          REFERENCES "Product"("Id") ON DELETE CASCADE,
    "ProductCode"        VARCHAR(100) NOT NULL,
    "Version"            VARCHAR(50)  NOT NULL,
    "AdjustmentId"       VARCHAR(100) NOT NULL,
    "Name"               VARCHAR(255) NOT NULL,
    "SortOrder"          INT          NOT NULL DEFAULT 0,
    "RateLookupCoverage" VARCHAR(100),
    UNIQUE ("ProductCode", "Version", "AdjustmentId")
);

CREATE TABLE "PolicyAdjustmentAppliesTo" (
    "Id"                 SERIAL PRIMARY KEY,
    "PolicyAdjustmentId" INT          NOT NULL REFERENCES "PolicyAdjustment"("Id") ON DELETE CASCADE,
    "CoverageCode"       VARCHAR(100) NOT NULL,
    "SortOrder"          INT          NOT NULL DEFAULT 0,
    UNIQUE ("PolicyAdjustmentId", "CoverageCode")
);

CREATE TABLE "PolicyAdjustmentStep" (
    "Id"                    SERIAL PRIMARY KEY,
    "PolicyAdjustmentId"    INT          NOT NULL REFERENCES "PolicyAdjustment"("Id") ON DELETE CASCADE,
    "StepOrder"             INT          NOT NULL DEFAULT 0,
    "StepId"                VARCHAR(100) NOT NULL,
    "Name"                  VARCHAR(255) NOT NULL,
    "Operation"             VARCHAR(50)  NOT NULL,
    "RateTableName"         VARCHAR(255),
    "MathType"              VARCHAR(50),
    "InterpolateKey"        VARCHAR(100),
    "RangeKeyName"          VARCHAR(100),
    "ComputeExpr"           TEXT,
    "ComputeStoreAs"        VARCHAR(255),
    "ComputeApplyToPremium" BOOLEAN,
    "RoundPrecision"        INT,
    "RoundMode"             VARCHAR(50),
    "WhenPath"              VARCHAR(500),
    "WhenOperator"          VARCHAR(50),
    "WhenValue"             VARCHAR(255)
);

CREATE TABLE "PolicyAdjustmentStepKey" (
    "Id"                     SERIAL PRIMARY KEY,
    "PolicyAdjustmentStepId" INT          NOT NULL REFERENCES "PolicyAdjustmentStep"("Id") ON DELETE CASCADE,
    "KeyName"                VARCHAR(100) NOT NULL,
    "KeyValue"               VARCHAR(500) NOT NULL
);

-- ==============================================================================
-- Sample Data / Initial Seed (Optional)
-- ==============================================================================

INSERT INTO "RiskField" ("LogicalName", "JsonPath", "DataType", "Category") VALUES
('State',            '$risk.State',          'String', 'Policy'),
('Construction Type','$risk.Construction',   'String', 'Building'),
('Occupancy',        '$risk.Occupancy',       'String', 'Building'),
('Protection Class', '$risk.ProtectionClass','Number', 'Location'),
('Coverage Amount',  '$risk.CoverageA',       'Number', 'Coverage');

INSERT INTO "LookupDimension" ("Name", "Description", "AllowedValues") VALUES
('Construction Types', 'Standard building construction classes', '["Frame", "Masonry", "Fire Resistive", "Non-Combustible"]'),
('Occupancy Types',    'Standard building occupancies',          '["Owner", "Tenant", "Commercial"]');
