-- V006: Add ValueType and IntendedCoverage columns to RateTable
-- ValueType documents the mathematical role of the rate value (Factor, Rate, FlatAmount, Multiplier)
-- IntendedCoverage is a free-text documentation field naming the coverage(s) this table is designed for

ALTER TABLE RateTable
    ADD IntendedCoverage NVARCHAR(200) NULL,
        ValueType        NVARCHAR(20)  NOT NULL DEFAULT 'Factor';

-- Backfill existing rows with the default value (already set by the column default above,
-- but an explicit UPDATE ensures any rows inserted before the migration are covered)
UPDATE RateTable SET ValueType = 'Factor' WHERE ValueType IS NULL OR ValueType = '';

-- Add a check constraint so only known ValueType values can be stored
ALTER TABLE RateTable
    ADD CONSTRAINT CK_RateTable_ValueType
        CHECK (ValueType IN ('Factor', 'Rate', 'FlatAmount', 'Multiplier'));
