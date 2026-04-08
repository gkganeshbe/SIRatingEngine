-- V008: Add ProductState table to track which states a product is filed in
-- State Coverage Configs should only be created for states listed here.
-- StateCode '*' is reserved to mean "all states" (wildcard), but is not stored in this table —
-- an empty table means "no explicit state restrictions" and coverage config creation is unrestricted.

CREATE TABLE ProductState (
    Id                INT          IDENTITY(1,1) PRIMARY KEY,
    ProductManifestId INT          NOT NULL REFERENCES ProductManifest(Id) ON DELETE CASCADE,
    StateCode         VARCHAR(10)  NOT NULL,
    CONSTRAINT UQ_ProductState UNIQUE (ProductManifestId, StateCode)
);

CREATE INDEX IX_ProductState_ManifestId ON ProductState (ProductManifestId);
