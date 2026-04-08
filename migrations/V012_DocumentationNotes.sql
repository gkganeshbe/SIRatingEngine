-- V012: Documentation Notes fields
--
-- Adds an optional free-text Notes column to key entities so business users
-- can document their configuration decisions directly in the Admin UI.
-- This replaces the need for external spreadsheets or email chains explaining
-- why a rate table was set up in a particular way.

ALTER TABLE ProductManifest  ADD Notes NVARCHAR(MAX) NULL;
ALTER TABLE CoverageConfig   ADD Notes NVARCHAR(MAX) NULL;
ALTER TABLE RateTable        ADD Notes NVARCHAR(MAX) NULL;
ALTER TABLE PipelineStep     ADD Notes NVARCHAR(MAX) NULL;
