
-- Minimal SQL Server schema for versioned products and generic rate rows
CREATE TABLE ProductVersion (
  ProductVersionId BIGINT IDENTITY PRIMARY KEY,
  ProductCode NVARCHAR(50) NOT NULL,
  Version NVARCHAR(20) NOT NULL,
  EffectiveStart DATE NOT NULL,
  EffectiveEnd DATE NULL,
  UNIQUE(ProductCode, Version)
);

CREATE TABLE RateTable (
  RateTableId BIGINT IDENTITY PRIMARY KEY,
  ProductVersionId BIGINT NOT NULL,
  Name NVARCHAR(200) NOT NULL,
  UNIQUE(ProductVersionId, Name)
);

CREATE TABLE RateRow (
  RateRowId BIGINT IDENTITY PRIMARY KEY,
  RateTableId BIGINT NOT NULL,
  Key1 NVARCHAR(100) NULL,
  Key2 NVARCHAR(100) NULL,
  Key3 NVARCHAR(100) NULL,
  Key4 NVARCHAR(100) NULL,
  Key5 NVARCHAR(100) NULL,
  Factor DECIMAL(18,6) NULL,
  Additive DECIMAL(18,6) NULL,
  EffStart DATE NOT NULL,
  EffEnd DATE NULL,
  Jurisdiction NVARCHAR(10) NULL,
  INDEX IX_RateRow_Lookup (RateTableId, EffStart, ISNULL(EffEnd,'99991231'))
);
