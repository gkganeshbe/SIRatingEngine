-- ==============================================================================
-- SI Rating Engine – Align PostgreSQL schema with repository expectations
-- Run this against each tenant database after the initial 01_Postgres_Schema.sql
-- ==============================================================================

-- 1. Product: add Notes column
ALTER TABLE "Product" ADD COLUMN IF NOT EXISTS "Notes" TEXT;

-- 2. ProductLob: add SortOrder
ALTER TABLE "ProductLob" ADD COLUMN IF NOT EXISTS "SortOrder" INT NOT NULL DEFAULT 0;

-- 3. CoverageRef: add AggregationRule, PerilRollup; make CoverageVersion nullable
--    (repos insert rows without specifying CoverageVersion)
ALTER TABLE "CoverageRef" ADD COLUMN IF NOT EXISTS "AggregationRule" VARCHAR(100);
ALTER TABLE "CoverageRef" ADD COLUMN IF NOT EXISTS "PerilRollup" VARCHAR(100);
ALTER TABLE "CoverageRef" ALTER COLUMN "CoverageVersion" DROP NOT NULL;
ALTER TABLE "CoverageRef" ALTER COLUMN "CoverageVersion" SET DEFAULT '';

-- 4. PolicyAdjustment: add ProductId FK + SortOrder
ALTER TABLE "PolicyAdjustment" ADD COLUMN IF NOT EXISTS "ProductId" INT REFERENCES "Product"("Id") ON DELETE CASCADE;
ALTER TABLE "PolicyAdjustment" ADD COLUMN IF NOT EXISTS "SortOrder" INT NOT NULL DEFAULT 0;

-- Back-fill ProductId for any pre-existing rows
UPDATE "PolicyAdjustment" pa
SET    "ProductId" = p."Id"
FROM   "Product" p
WHERE  p."ProductCode" = pa."ProductCode"
  AND  p."Version"     = pa."Version"
  AND  pa."ProductId" IS NULL;

-- 5. PolicyAdjustmentAppliesTo (was JSONB in initial schema; repos use a relation)
CREATE TABLE IF NOT EXISTS "PolicyAdjustmentAppliesTo" (
    "Id"                 SERIAL PRIMARY KEY,
    "PolicyAdjustmentId" INT          NOT NULL REFERENCES "PolicyAdjustment"("Id") ON DELETE CASCADE,
    "CoverageCode"       VARCHAR(100) NOT NULL,
    "SortOrder"          INT          NOT NULL DEFAULT 0,
    UNIQUE ("PolicyAdjustmentId", "CoverageCode")
);

-- 6. PolicyAdjustmentStep
CREATE TABLE IF NOT EXISTS "PolicyAdjustmentStep" (
    "Id"                   SERIAL PRIMARY KEY,
    "PolicyAdjustmentId"   INT          NOT NULL REFERENCES "PolicyAdjustment"("Id") ON DELETE CASCADE,
    "StepOrder"            INT          NOT NULL DEFAULT 0,
    "StepId"               VARCHAR(100) NOT NULL,
    "Name"                 VARCHAR(255) NOT NULL,
    "Operation"            VARCHAR(50)  NOT NULL,
    "RateTableName"        VARCHAR(255),
    "MathType"             VARCHAR(50),
    "InterpolateKey"       VARCHAR(100),
    "RangeKeyName"         VARCHAR(100),
    "ComputeExpr"          TEXT,
    "ComputeStoreAs"       VARCHAR(255),
    "ComputeApplyToPremium" BOOLEAN,
    "RoundPrecision"       INT,
    "RoundMode"            VARCHAR(50),
    "WhenPath"             VARCHAR(500),
    "WhenOperator"         VARCHAR(50),
    "WhenValue"            VARCHAR(255)
);

-- 7. PolicyAdjustmentStepKey
CREATE TABLE IF NOT EXISTS "PolicyAdjustmentStepKey" (
    "Id"                    SERIAL PRIMARY KEY,
    "PolicyAdjustmentStepId" INT          NOT NULL REFERENCES "PolicyAdjustmentStep"("Id") ON DELETE CASCADE,
    "KeyName"               VARCHAR(100) NOT NULL,
    "KeyValue"              VARCHAR(500) NOT NULL
);

-- 8. ProductState: rename State → StateCode, add ProductId FK
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name   = 'ProductState'
          AND column_name  = 'State'
    ) THEN
        ALTER TABLE "ProductState" RENAME COLUMN "State" TO "StateCode";
    END IF;
END $$;

ALTER TABLE "ProductState" ADD COLUMN IF NOT EXISTS "ProductId" INT REFERENCES "Product"("Id") ON DELETE CASCADE;

-- Back-fill ProductId for any pre-existing rows
UPDATE "ProductState" ps
SET    "ProductId" = p."Id"
FROM   "Product" p
WHERE  p."ProductCode" = ps."ProductCode"
  AND  ps."ProductId" IS NULL;
