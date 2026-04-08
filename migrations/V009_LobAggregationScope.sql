-- V009: LOB Aggregation Scope (guardrail) and Coverage Aggregation Rule
--
-- LobAggregationScope declares which aggregation scopes are PERMITTED for any coverage
-- under a given LOB. This is a guardrail — it doesn't dictate what must be used.
--
-- CoverageRef.AggregationRule is the actual scope SELECTED for a specific coverage
-- within the LOB's permitted set.
--
-- CoverageRef.PerilRollup defines how peril premiums combine when a coverage has multiple perils:
-- 'Sum' | 'Maximum' | 'IndependentlyCapped'

CREATE TABLE LobAggregationScope (
    Id    INT          IDENTITY(1,1) PRIMARY KEY,
    LobId INT          NOT NULL REFERENCES ProductLob(Id) ON DELETE CASCADE,
    Scope VARCHAR(50)  NOT NULL,
    CONSTRAINT UQ_LobAggregationScope UNIQUE (LobId, Scope)
);

CREATE INDEX IX_LobAggregationScope_LobId ON LobAggregationScope (LobId);

ALTER TABLE CoverageRef
    ADD AggregationRule VARCHAR(50) NULL,
        PerilRollup     VARCHAR(30) NULL;
