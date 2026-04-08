-- =============================================================================
-- SIRatingEngine – V005 Drop RateTableRow.Additive
-- The original schema had separate Factor and Additive columns. The codebase
-- was consolidated to use Factor only. This migration drops Additive and
-- updates the related indexes and check constraint.
-- =============================================================================

-- ── 1. Drop old indexes that INCLUDEd Additive ───────────────────────────────
DROP INDEX IF EXISTS IX_RateTableRow_TableEff ON RateTableRow;
DROP INDEX IF EXISTS IX_RateTableRow_Keys     ON RateTableRow;

-- ── 2. Drop Additive column ───────────────────────────────────────────────────
ALTER TABLE RateTableRow DROP COLUMN Additive;

-- ── 3. Recreate indexes ───────────────────────────────────────────────────────
CREATE INDEX IX_RateTableRow_TableEff
    ON RateTableRow (RateTableId, EffStart)
    INCLUDE (Key1, Key2, Key3, Key4, Key5, RangeFrom, RangeTo,
             Factor, AdditionalUnit, AdditionalRate, ExpireAt);

CREATE INDEX IX_RateTableRow_Keys
    ON RateTableRow (RateTableId, Key1, Key2, Key3, EffStart)
    INCLUDE (Key4, Key5, Factor, ExpireAt);

-- ── 4. Update check constraint on RateTableColumnDef ─────────────────────────
-- Remove 'Additive' from the allowed set; keep 'Factor'.
ALTER TABLE RateTableColumnDef DROP CONSTRAINT IF EXISTS CK_ColumnName;

ALTER TABLE RateTableColumnDef
    ADD CONSTRAINT CK_ColumnName CHECK (ColumnName IN (
        'Key1','Key2','Key3','Key4','Key5',
        'RangeFrom','RangeTo',
        'Factor','AdditionalUnit','AdditionalRate'));

-- ── 5. Remove any Additive RateTableColumnDef rows ───────────────────────────
DELETE FROM RateTableColumnDef WHERE ColumnName = 'Additive';
