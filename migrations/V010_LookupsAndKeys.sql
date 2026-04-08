-- V010: Lookup Dimensions and Derived Keys (Lookups & Keys admin section)
--
-- LookupDimension: defines the allowed values for a categorical field.
-- Business users pick from these dropdowns rather than typing free text.
--
-- DerivedKey: defines a composite value calculated from the request before rating.
-- These become available as named inputs in steps and conditions.

CREATE TABLE LookupDimension (
    Id              INT           IDENTITY(1,1) PRIMARY KEY,
    ProductManifestId INT         NULL REFERENCES ProductManifest(Id) ON DELETE CASCADE,
    -- NULL = global (all products); non-NULL = product-specific
    Name            NVARCHAR(100) NOT NULL,
    Description     NVARCHAR(500) NULL,
    SortOrder       INT           NOT NULL DEFAULT 0,
    CONSTRAINT UQ_LookupDimension UNIQUE (ProductManifestId, Name)
);

CREATE TABLE LookupDimensionValue (
    Id                  INT           IDENTITY(1,1) PRIMARY KEY,
    LookupDimensionId   INT           NOT NULL REFERENCES LookupDimension(Id) ON DELETE CASCADE,
    Value               NVARCHAR(100) NOT NULL,
    DisplayLabel        NVARCHAR(200) NULL,
    SortOrder           INT           NOT NULL DEFAULT 0,
    CONSTRAINT UQ_LookupDimensionValue UNIQUE (LookupDimensionId, Value)
);

CREATE TABLE DerivedKey (
    Id                  INT           IDENTITY(1,1) PRIMARY KEY,
    ProductManifestId   INT           NULL REFERENCES ProductManifest(Id) ON DELETE CASCADE,
    Name                NVARCHAR(100) NOT NULL,
    ReadableName        NVARCHAR(200) NOT NULL,
    AggFunction         VARCHAR(10)   NOT NULL,  -- SUM | COUNT | AVG | MAX | MIN
    SourceField         NVARCHAR(200) NOT NULL,
    Description         NVARCHAR(500) NULL,
    CONSTRAINT UQ_DerivedKey UNIQUE (ProductManifestId, Name)
);

CREATE INDEX IX_LookupDimension_ManifestId   ON LookupDimension     (ProductManifestId);
CREATE INDEX IX_LookupDimensionValue_DimId   ON LookupDimensionValue (LookupDimensionId);
CREATE INDEX IX_DerivedKey_ManifestId        ON DerivedKey           (ProductManifestId);
