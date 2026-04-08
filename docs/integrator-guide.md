# SI Rating Engine — Integrator Guide

This guide is for teams who configure and operate the SI Rating Engine. It covers how to set up products, coverages, rate tables, and rating pipelines through the Admin API, and explains in detail how the rating algorithm computes premiums.

---

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Authentication](#2-authentication)
3. [Core Concepts](#3-core-concepts)
4. [Admin API Reference](#4-admin-api-reference)
   - [Products](#41-products)
   - [Coverages](#42-coverages)
   - [Rate Tables](#43-rate-tables)
   - [Rate Table Rows](#44-rate-table-rows)
   - [Pipeline Steps](#45-pipeline-steps)
   - [Column Definitions](#46-column-definitions)
5. [Rating Algorithm Deep Dive](#5-rating-algorithm-deep-dive)
   - [Step Types](#51-step-types)
   - [Key Resolution](#52-key-resolution)
   - [Conditional Execution (When Guards)](#53-conditional-execution-when-guards)
   - [Lookup Modes](#54-lookup-modes)
6. [End-to-End Configuration Walkthrough](#6-end-to-end-configuration-walkthrough)
7. [Quote API Reference](#7-quote-api-reference)
8. [Rate Table Design Patterns](#8-rate-table-design-patterns)
9. [Worked Premium Calculation Examples](#9-worked-premium-calculation-examples)
10. [Commercial Multi-LOB Rating](#10-commercial-multi-lob-rating)
    - [Key Concepts](#101-key-concepts)
    - [Product Manifest with LOBs](#102-product-manifest-with-lobs)
    - [Risk Bag Merge Chain](#103-risk-bag-merge-chain)
    - [Request Structure](#104-request-structure)
    - [Response Structure](#105-response-structure)
    - [Pipeline Configuration Notes](#106-notes-for-pipeline-configuration)
    - [SQL Schema](#107-sql-schema-database-storage)

---

## 1. Architecture Overview

The SI Rating Engine uses a **declarative pipeline** model. Instead of writing rating logic in code, you configure it as a sequence of steps in JSON. Each step refers to a rate table and describes how to transform the running premium.

```
Caller (Vendor / Carrier System)
  ProductCode · RateState · CoverageCode · RateEffectiveDate · Risk
        │
        ▼
┌───────────────────────────────────────┐
│   Quote API                            │
│   POST /quote/rate                     │
│   POST /quote/rate-policy-segments    │
│   POST /quote/rate-coverage-segments  │
│   POST /quote/rate-commercial         │  ← commercial multi-LOB
└───────────────────────────────────────┘
        │
        ▼ (engine resolves internally — caller never picks a pipeline)
┌─────────────────────────────────────────────────────────────────┐
│   Pipeline Resolver                                              │
│   product + state + coverage + effectiveDate → active pipeline  │
└─────────────────────────────────────────────────────────────────┘
        │
        ├──▶  Product Manifest  (which coverages belong to this product/date)
        │
        └──▶  Coverage Config   (state-specific pipeline: perils + steps)
                    │
                    │ per peril
                    ▼
          ┌──────────────────────────┐
          │  Pipeline Runner          │
          │  Step 1 → Step 2 → ...    │
          │  (each step reads a       │
          │   rate table factor)      │
          └──────────────────────────┘
                    │
                    ▼
    { coveragePremium, perils: [ { peril, premium, trace } ] }
```

Key objects:

| Object | Purpose |
|---|---|
| **Product Manifest** | Declares which coverage versions belong to a product; resolved by `productCode + effectiveDate` |
| **Coverage Config** | State-specific pipeline definition (perils + steps); resolved by `productCode + state + coverageCode + effectiveDate` |
| **Rate Table** | A keyed table of factors scoped to a specific Coverage Config — names only need to be unique within that coverage |
| **Pipeline Step** | One transformation of the running premium |
| **Risk Bag** | A flat dictionary of attributes sent by the caller (merged with coverage params) |
| **RateState** | The rating state — a first-class routing key that selects the right pipeline, not a risk factor |

---

## 2. Authentication

Both the Quote API and Admin API are protected by Bearer tokens issued by the Identity Server (`https://localhost:7000` in development).

### Obtaining a Token

Use the client credentials flow. Two clients are pre-registered for system-to-system use:

| API | Client ID | Secret | Required Scope |
|---|---|---|---|
| Quote API | `vendor-adapter-api` | *(configured in identity server)* | `quote.access` |
| Admin API | *(your M2M client)* | *(your secret)* | `rating-engine.admin` |

```
POST https://localhost:7000/connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=client_credentials
&client_id=vendor-adapter-api
&client_secret=YOUR_SECRET
&scope=quote.access
```

Include the token on every request:

```
Authorization: Bearer eyJhbGciOiJSUzI1NiIs...
X-Tenant-Id: tenant-acme
```

> **Note**: `X-Tenant-Id` is always required in addition to the Bearer token. It identifies which tenant's database to use.

---

## 3. Core Concepts

### Product Manifest

A product manifest declares a **versioned product** and the exact coverage versions it contains.

```json
{
  "productCode": "HO-PRIMARY",
  "version": "2026.02",
  "effectiveStart": "2026-02-01",
  "coverages": [
    { "coverageCode": "DWELLING",  "version": "2026.02" },
    { "coverageCode": "LIABILITY", "version": "2026.01" }
  ]
}
```

Each product version pins specific coverage versions — allowing you to update a coverage independently of other coverages in the same product.

### Coverage Config

A coverage config defines:
- The **state** it applies to — the primary routing dimension alongside product and coverage
- Which **perils** it rates (e.g., `FIRE`, `HURRICANE`, `GRP1`)
- The **pipeline** — an ordered list of steps to compute the premium for each peril

```json
{
  "productCode": "HO-PRIMARY",
  "state": "NJ",
  "coverageCode": "DWELLING",
  "version": "2026.02",
  "effectiveStart": "2026-02-01",
  "perils": ["FIRE", "HURRICANE"],
  "pipeline": [ /* steps */ ]
}
```

Use `"state": "*"` for a pipeline that applies to all states as a fallback. The engine always prefers an exact state match over a wildcard.

The pipeline runs **once per peril**. If a coverage has perils `["FIRE", "HURRICANE"]`, the full pipeline executes twice — once with `$peril = "FIRE"` and once with `$peril = "HURRICANE"`. The coverage premium is the sum across all perils.

**Pipeline resolution** is performed by the engine automatically. Given `productCode + RateState + coverageCode + RateEffectiveDate`, the engine finds the coverage config with the latest `effectiveStart` on or before the effective date. The caller never specifies a version string.

### Risk Bag

The risk bag is a flat `Dictionary<string, string>` sent by the caller in the quote request. It contains all property-level and coverage-level attributes needed for rating. Pipeline steps reference these values using `$risk.<Key>` path expressions.

```json
{
  "State": "NJ",
  "Zone": "Z1",
  "Occupancy": "OWNER",
  "ConstructionType": "FRAME",
  "ProtectionClass": "3",
  "CoverageA": "250000"
}
```

Coverage-level parameters passed in the quote request (such as `InsuredValue`, `Deductible`) are merged into the risk bag with coverage params taking precedence on key collision.

---

## 4. Admin API Reference

All Admin API endpoints require:
- `Authorization: Bearer <token>` (scope: `rating-engine.admin`)
- `X-Tenant-Id: <tenant-id>`

---

### 4.1 Products

#### List Products

```
GET /admin/products
```

Returns all products in the tenant.

**Response**
```json
[
  {
    "id": 1,
    "productCode": "HO-PRIMARY",
    "version": "2026.02",
    "effStart": "2026-02-01",
    "expireAt": null,
    "createdAt": "2026-02-15T10:00:00Z",
    "createdBy": "admin@carrier.com"
  }
]
```

---

#### Get Product Detail

```
GET /admin/products/{productCode}/{version}
```

Returns full product detail including the coverage list.

**Response**
```json
{
  "id": 1,
  "productCode": "HO-PRIMARY",
  "version": "2026.02",
  "effStart": "2026-02-01",
  "expireAt": null,
  "createdAt": "2026-02-15T10:00:00Z",
  "createdBy": "admin@carrier.com",
  "modifiedAt": null,
  "modifiedBy": null,
  "coverages": [
    { "id": 1, "coverageCode": "DWELLING",  "coverageVersion": "2026.02", "sortOrder": 1 },
    { "id": 2, "coverageCode": "LIABILITY", "coverageVersion": "2026.01", "sortOrder": 2 }
  ]
}
```

---

#### Create Product

```
POST /admin/products
X-User-Id: admin@carrier.com   (optional — captured as createdBy)
```

**Request Body**
```json
{
  "productCode": "HO-PRIMARY",
  "version": "2026.02",
  "effStart": "2026-02-01",
  "expireAt": null,
  "coverages": [
    { "coverageCode": "DWELLING",  "coverageVersion": "2026.02" },
    { "coverageCode": "LIABILITY", "coverageVersion": "2026.01" }
  ]
}
```

**Response** `201 Created`
```json
{ "id": 1 }
```

---

#### Update Product

```
PUT /admin/products/{id}
X-User-Id: admin@carrier.com
```

**Request Body**
```json
{
  "effStart": "2026-03-01",
  "expireAt": null,
  "coverages": [
    { "coverageCode": "DWELLING",     "coverageVersion": "2026.03" },
    { "coverageCode": "LIABILITY",    "coverageVersion": "2026.01" },
    { "coverageCode": "OTHERSTRUCTURE", "coverageVersion": "2026.02" }
  ]
}
```

---

#### Expire Product

```
POST /admin/products/{id}/expire
```

**Request Body**
```json
{ "expireAt": "2026-12-31" }
```

Sets the expiration date without deleting. The product will not be returned for rating requests with an effective date after `expireAt`.

---

#### Delete Product

```
DELETE /admin/products/{id}
```

Hard deletes the product record.

---

### 4.2 Coverages

#### List Coverages

```
GET /admin/coverages
GET /admin/coverages?productCode=HO-PRIMARY    (filtered)
```

**Response**
```json
[
  {
    "id": 1,
    "productCode": "HO-PRIMARY",
    "state": "NJ",
    "coverageCode": "DWELLING",
    "version": "2026.02",
    "effStart": "2026-02-01",
    "expireAt": null,
    "createdAt": "2026-02-15T10:00:00Z",
    "createdBy": "admin@carrier.com"
  }
]
```

---

#### Get Coverage Detail

```
GET /admin/coverages/{productCode}/{coverageCode}/{version}
```

Returns the full coverage including perils and pipeline.

**Response**
```json
{
  "id": 1,
  "productCode": "HO-PRIMARY",
  "state": "NJ",
  "coverageCode": "DWELLING",
  "version": "2026.02",
  "effStart": "2026-02-01",
  "expireAt": null,
  "createdAt": "2026-02-15T10:00:00Z",
  "createdBy": "admin@carrier.com",
  "modifiedAt": null,
  "modifiedBy": null,
  "perils": ["FIRE", "HURRICANE"],
  "pipeline": [
    {
      "id": "S1",
      "name": "Base Loss Cost",
      "operation": "lookup",
      "rateTable": "BaseLossCost",
      "keys": { "Peril": "$peril", "OccupancyType": "$risk.Occupancy", "Zone": "$risk.Zone" },
      "math": { "type": "set" }
    }
  ]
}
```

---

#### Create Coverage

```
POST /admin/coverages
X-User-Id: admin@carrier.com
```

**Request Body**
```json
{
  "productCode": "HO-PRIMARY",
  "state": "NJ",
  "coverageCode": "DWELLING",
  "version": "2026.02",
  "effStart": "2026-02-01",
  "expireAt": null,
  "perils": ["FIRE", "HURRICANE"],
  "pipeline": [
    {
      "id": "S1",
      "name": "Base Loss Cost",
      "operation": "lookup",
      "rateTable": "BaseLossCost",
      "keys": { "Peril": "$peril", "OccupancyType": "$risk.Occupancy", "Zone": "$risk.Zone" },
      "math": { "type": "set" }
    },
    {
      "id": "ROUND_FINAL",
      "name": "Round Premium",
      "operation": "round",
      "round": { "precision": 2, "mode": "AwayFromZero" }
    }
  ]
}
```

> Use `"state": "*"` for a state-agnostic pipeline that applies as a fallback when no exact state match exists.

**Response** `201 Created`
```json
{ "id": 1 }
```

---

#### Update Coverage

```
PUT /admin/coverages/{id}
X-User-Id: admin@carrier.com
```

**Request Body**
```json
{
  "effStart": "2026-03-01",
  "expireAt": null,
  "perils": ["FIRE", "HURRICANE", "WIND"],
  "pipeline": [ /* updated steps */ ]
}
```

---

#### Expire / Delete Coverage

```
POST /admin/coverages/{id}/expire    body: { "expireAt": "2026-12-31" }
DELETE /admin/coverages/{id}
```

---

### 4.3 Rate Tables

Rate tables are scoped to a specific **Coverage Config**. A table's name only needs to be unique within its coverage config — the same name (e.g., `BaseLossCost`) can exist in every coverage config without collision. This means you can name your tables intuitively without worrying about global uniqueness.

All rate table endpoints are nested under `/admin/coverages/{coverageId}/...`. You need the numeric `id` of the coverage config returned by the coverage create or list endpoints.

> **File-system layout**: When using the file-system storage provider, rate table JSON files live in per-coverage subdirectories:
> `data/rates/{productCode}.{state}.{coverageCode}.{version}/TableName.json`

#### List Rate Tables

```
GET /admin/coverages/{coverageId}/rate-tables
```

**Response**
```json
[
  {
    "id": 1,
    "coverageConfigId": 1,
    "name": "BaseLossCost",
    "description": "Base loss costs by peril, occupancy and zone",
    "lookupType": "exact",
    "interpolationKeyCol": null,
    "effStart": "2026-01-01",
    "expireAt": null,
    "createdAt": "2026-02-01T00:00:00Z",
    "createdBy": "admin@carrier.com"
  }
]
```

---

#### Get Rate Table Detail

```
GET /admin/coverages/{coverageId}/rate-tables/{name}
```

Returns the table metadata and its column definitions.

**Response**
```json
{
  "id": 1,
  "coverageConfigId": 1,
  "name": "BaseLossCost",
  "description": "Base loss costs by peril, occupancy and zone",
  "lookupType": "exact",
  "interpolationKeyCol": null,
  "effStart": "2026-01-01",
  "expireAt": null,
  "createdAt": "2026-02-01T00:00:00Z",
  "createdBy": "admin@carrier.com",
  "columnDefs": [
    { "id": 1, "columnName": "Key1", "displayLabel": "Peril",         "dataType": "string",  "sortOrder": 1, "isRequired": true },
    { "id": 2, "columnName": "Key2", "displayLabel": "Occupancy Type","dataType": "string",  "sortOrder": 2, "isRequired": true },
    { "id": 3, "columnName": "Key3", "displayLabel": "Zone",          "dataType": "string",  "sortOrder": 3, "isRequired": true },
    { "id": 4, "columnName": "Factor","displayLabel": "Base Rate",    "dataType": "decimal", "sortOrder": 4, "isRequired": true }
  ]
}
```

---

#### Create Rate Table

```
POST /admin/coverages/{coverageId}/rate-tables
X-User-Id: admin@carrier.com
```

**Request Body**

The `lookupType` field controls which lookup algorithm is used:

| `lookupType` | Description |
|---|---|
| `exact` | All keys must match exactly (or wildcard `*`) |
| `interpolate` | One key dimension is numeric; value is linearly interpolated between breakpoints |
| `range` | One key dimension is compared against `RangeFrom`/`RangeTo` bounds |

`coverageConfigId` in the body must match the `{coverageId}` in the URL.

```json
{
  "coverageConfigId": 1,
  "name": "BaseLossCost",
  "description": "Base loss costs by peril, occupancy and zone",
  "lookupType": "exact",
  "interpolationKeyCol": null,
  "effStart": "2026-01-01",
  "expireAt": null,
  "columnDefs": [
    { "columnName": "Key1",   "displayLabel": "Peril",         "dataType": "string",  "sortOrder": 1, "isRequired": true },
    { "columnName": "Key2",   "displayLabel": "Occupancy Type","dataType": "string",  "sortOrder": 2, "isRequired": true },
    { "columnName": "Key3",   "displayLabel": "Zone",          "dataType": "string",  "sortOrder": 3, "isRequired": true },
    { "columnName": "Factor", "displayLabel": "Base Rate",     "dataType": "decimal", "sortOrder": 4, "isRequired": true }
  ]
}
```

**For an interpolation table** (e.g. amount-of-insurance factors), set `lookupType: "interpolate"` and `interpolationKeyCol` to the column name whose values are numeric breakpoints:

```json
{
  "coverageConfigId": 1,
  "name": "AOIFactor",
  "description": "Amount of insurance additive factor",
  "lookupType": "interpolate",
  "interpolationKeyCol": "Key2",
  "effStart": "2026-01-01",
  "expireAt": null,
  "columnDefs": [
    { "columnName": "Key1",     "displayLabel": "Peril",           "dataType": "string",  "sortOrder": 1, "isRequired": true },
    { "columnName": "Key2",     "displayLabel": "Coverage Amount",  "dataType": "decimal", "sortOrder": 2, "isRequired": true },
    { "columnName": "Additive", "displayLabel": "Additive Factor",  "dataType": "decimal", "sortOrder": 3, "isRequired": true }
  ]
}
```

**Response** `201 Created`
```json
{ "id": 2 }
```

---

#### Update Rate Table

```
PUT /admin/coverages/{coverageId}/rate-tables/{id}
```

Only metadata fields can be updated (not the name). Column definitions are updated separately.

**Request Body**
```json
{
  "description": "Updated description",
  "lookupType": "exact",
  "interpolationKeyCol": null,
  "expireAt": "2027-01-01"
}
```

---

#### Delete Rate Table

```
DELETE /admin/coverages/{coverageId}/rate-tables/{id}
```

---

### 4.4 Rate Table Rows

Rows are the actual data in a rate table. Each row has up to 5 key columns, a factor (multiplicative) or additive value, and an effective date range.

#### List Rows

```
GET /admin/coverages/{coverageId}/rate-tables/{name}/rows
GET /admin/coverages/{coverageId}/rate-tables/{name}/rows?effectiveDate=2026-06-01
```

**Response**
```json
[
  {
    "id": 1001,
    "key1": "FIRE",
    "key2": "OWNER",
    "key3": "Z1",
    "key4": null,
    "key5": null,
    "rangeFrom": null,
    "rangeTo": null,
    "factor": 120.0,
    "additive": null,
    "additionalUnit": null,
    "additionalRate": null,
    "effStart": "2026-01-01",
    "expireAt": null
  }
]
```

---

#### Add a Row

```
POST /admin/coverages/{coverageId}/rate-tables/{name}/rows
```

**Request Body**
```json
{
  "key1": "FIRE",
  "key2": "OWNER",
  "key3": "Z1",
  "factor": 120.0,
  "effStart": "2026-01-01",
  "expireAt": null
}
```

> Use `factor` for multiplicative values and `additive` for additive values. You should not set both on the same row. For tiered rates above the highest interpolation breakpoint, also set `additionalRate` and `additionalUnit`.

**Response** `201 Created`
```json
{ "id": 1001 }
```

---

#### Bulk Insert Rows

Use this endpoint when loading rate tables in bulk (e.g., after a rate filing).

```
POST /admin/coverages/{coverageId}/rate-tables/{name}/rows/bulk
```

**Request Body**
```json
{
  "rows": [
    { "key1": "FIRE",      "key2": "OWNER",  "key3": "Z1", "factor": 120.0, "effStart": "2026-01-01" },
    { "key1": "FIRE",      "key2": "TENANT", "key3": "Z1", "factor": 150.0, "effStart": "2026-01-01" },
    { "key1": "HURRICANE", "key2": "OWNER",  "key3": "Z1", "factor": 200.0, "effStart": "2026-01-01" }
  ]
}
```

**Response**
```json
{ "inserted": 3 }
```

---

#### Update a Row

```
PUT /admin/coverages/{coverageId}/rate-tables/{name}/rows/{rowId}
```

**Request Body** — same shape as create.

---

#### Expire a Row

```
POST /admin/coverages/{coverageId}/rate-tables/{name}/rows/{rowId}/expire
```

**Request Body**
```json
{ "expireAt": "2026-12-31" }
```

Expired rows are still stored but excluded from lookups with an effective date after `expireAt`. This supports rate history without deleting data.

---

#### Delete a Row

```
DELETE /admin/coverages/{coverageId}/rate-tables/{name}/rows/{rowId}
```

---

### 4.5 Pipeline Steps

Pipeline steps are the individual instructions that make up a coverage's rating algorithm. They are executed in order for every peril.

#### List Steps

```
GET /admin/coverages/{coverageId}/pipeline/steps
```

Returns the ordered list of `StepConfig` objects.

---

#### Add a Step

```
POST /admin/coverages/{coverageId}/pipeline/steps
```

**Request Body**

`insertAfterOrder` is optional. If omitted, the step is appended at the end.

```json
{
  "step": {
    "id": "S5_PROTCLASS",
    "name": "Protection Class Factor",
    "operation": "lookup",
    "rateTable": "ProtClassFactor",
    "keys": {
      "Peril": "$peril",
      "ProtClass": "$risk.ProtectionClass"
    },
    "math": { "type": "mul" }
  },
  "insertAfterOrder": 4
}
```

**Response**
```json
{ "dbId": 12, "stepId": "S5_PROTCLASS" }
```

---

#### Update a Step

```
PUT /admin/coverages/{coverageId}/pipeline/steps/{stepId}
```

**Request Body** — a full `StepConfig` object (same as in create).

---

#### Delete a Step

```
DELETE /admin/coverages/{coverageId}/pipeline/steps/{stepId}
```

---

#### Reorder Steps

```
PUT /admin/coverages/{coverageId}/pipeline/reorder
```

**Request Body** — the ordered array of all step IDs in the new order:
```json
{
  "orderedStepIds": ["S1_BASE", "S2_AOI", "ROUND_BASE", "S3_LCM", "S4_TERRFACTOR", "ROUND_FINAL"]
}
```

---

### 4.6 Column Definitions

Column definitions describe the key layout of a rate table. They are informational metadata used by admin tooling and for documentation purposes.

#### List Column Definitions

```
GET /admin/coverages/{coverageId}/rate-tables/{tableName}/column-defs
```

---

#### Replace Column Definitions

```
PUT /admin/coverages/{coverageId}/rate-tables/{tableName}/column-defs
```

Replaces **all** column definitions (full replace, not a merge).

**Request Body**
```json
[
  { "columnName": "Key1",   "displayLabel": "Peril",        "dataType": "string",  "sortOrder": 1, "isRequired": true },
  { "columnName": "Key2",   "displayLabel": "State",        "dataType": "string",  "sortOrder": 2, "isRequired": true },
  { "columnName": "Factor", "displayLabel": "Territory LCM","dataType": "decimal", "sortOrder": 3, "isRequired": true }
]
```

---

#### Update a Single Column Definition

```
PUT /admin/coverages/{coverageId}/rate-tables/{tableName}/column-defs/{id}
```

---

#### Delete a Column Definition

```
DELETE /admin/coverages/{coverageId}/rate-tables/{tableName}/column-defs/{id}
```

---

## 5. Rating Algorithm Deep Dive

The rating engine is a **sequential premium transformer**. Every step in the pipeline receives the current premium and either multiplies, adds, subtracts, sets, or rounds it — or computes a new value and stores it in the risk bag.

### 5.1 Step Types

There are three step operations: `lookup`, `compute`, and `round`.

---

#### Operation: `lookup`

Retrieves a factor from a rate table and applies a math operation to the current premium.

**Required fields:**

| Field | Description |
|---|---|
| `operation` | `"lookup"` |
| `rateTable` | Name of the rate table to query |
| `keys` | Map of key column names to value path expressions |
| `math.type` | Math operation to apply: `set`, `mul`, `add`, `sub` |

**Optional fields:**

| Field | Description |
|---|---|
| `interpolate.key` | Key name to use as numeric interpolation dimension |
| `rangeKey.key` | Risk bag field whose value must fall within `[RangeFrom, RangeTo]` |
| `when` | Conditional guard — step only executes if the condition is true |

**Math types:**

| `math.type` | Formula | Use case |
|---|---|---|
| `set` | `premium = factor` | Base rate — sets the starting premium |
| `mul` | `premium = premium × factor` | Multiplicative modifiers (territory, protection class, etc.) |
| `add` | `premium = premium + factor` | Additive surcharges |
| `sub` | `premium = premium - factor` | Discounts, reductions |

**Example — set base rate:**
```json
{
  "id": "S1_BASE",
  "name": "Base Loss Cost",
  "operation": "lookup",
  "rateTable": "BaseLossCost",
  "keys": {
    "Peril":        "$peril",
    "OccupancyType":"$risk.Occupancy",
    "Zone":         "$risk.Zone"
  },
  "math": { "type": "set" }
}
```

**Example — multiply by territory factor:**
```json
{
  "id": "S3_TERRITORY",
  "name": "Territory Factor",
  "operation": "lookup",
  "rateTable": "TerritoryFactor",
  "keys": {
    "State": "$risk.State",
    "Territory": "$risk.Territory"
  },
  "math": { "type": "mul" }
}
```

---

#### Operation: `compute`

Evaluates an arithmetic expression and stores the result in the risk bag. Optionally replaces the running premium.

**Required fields:**

| Field | Description |
|---|---|
| `operation` | `"compute"` |
| `compute.expr` | Arithmetic expression (see below) |
| `compute.storeAs` | Key under which to store the result in the risk bag |

**Optional fields:**

| Field | Default | Description |
|---|---|---|
| `compute.applyToPremium` | `false` | If `true`, sets the running premium to the computed value |

**Expression syntax:**

Tokens are separated by single spaces. Evaluation is strictly left-to-right (no operator precedence).

| Token type | Syntax | Example |
|---|---|---|
| Current premium | `$premium` | `$premium * 1.05` |
| Risk bag value | `$risk.<Key>` | `$risk.InsuredValue` |
| Decimal literal | Any number | `100`, `0.05` |
| Operators | `+`, `-`, `*`, `/` | — |

**Example — compute final premium from rate × insured value:**
```json
{
  "id": "S12_FINAL",
  "name": "Apply Building Limit (per $100)",
  "operation": "compute",
  "compute": {
    "expr": "$premium * $risk.InsuredValue / 100",
    "storeAs": "FinalPremium",
    "applyToPremium": true
  }
}
```

Meaning: `premium = (current premium) × (insured value) / 100`

**Example — derive a value for use by a later step:**
```json
{
  "id": "CALC_EXCESS",
  "name": "Calculate Excess Amount",
  "operation": "compute",
  "compute": {
    "expr": "$risk.InsuredValue - $risk.Deductible",
    "storeAs": "ExcessAmount",
    "applyToPremium": false
  }
}
```

This stores `ExcessAmount` in the risk bag. A later step can reference it as `$risk.ExcessAmount`.

---

#### Operation: `round`

Rounds the current premium to a specified precision.

**Required fields:**

| Field | Description |
|---|---|
| `operation` | `"round"` |
| `round.precision` | Number of decimal places (default: `2`) |
| `round.mode` | `"AwayFromZero"` (default) or `"ToEven"` |

**Example:**
```json
{
  "id": "ROUND_FINAL",
  "name": "Round Final Premium",
  "operation": "round",
  "round": { "precision": 2, "mode": "AwayFromZero" }
}
```

> Place round steps at natural breakpoints in your pipeline — after the base rate section and again at the end — to prevent rounding drift accumulating across many multiplicative steps.

---

### 5.2 Key Resolution

Key values in `lookup` steps are specified as **path expressions** that resolve at runtime:

| Path expression | Resolves to |
|---|---|
| `$peril` | The current peril being rated (e.g., `"FIRE"`) |
| `$risk.<Key>` | Value from the risk bag, e.g., `$risk.State` → `"NJ"` |
| `"*"` (literal) | Wildcard — matches any row key value of `"*"` |

**Example:**
```json
"keys": {
  "Peril":     "$peril",
  "State":     "$risk.State",
  "Territory": "$risk.Territory"
}
```

At runtime with `$peril = "FIRE"`, `$risk.State = "NJ"`, `$risk.Territory = "T4"`, this looks up the row where `Key1="FIRE"`, `Key2="NJ"`, `Key3="T4"`.

**Wildcard rows:** A rate table row with a key value of `"*"` matches any request value. More specific matches take precedence over wildcards.

```json
// Wildcard row — matches any peril
{ "Key1": "*", "Key2": "OWNER", "Factor": 1.0 }
```

**Computed risk bag values** produced by earlier `compute` steps are also available via `$risk.<Key>`:
```json
"keys": { "ExcessBand": "$risk.ExcessBand" }
```

---

### 5.3 Conditional Execution (When Guards)

Every step (lookup, compute, or round) supports an optional `when` clause. When present, the step executes only if the condition evaluates to `true`. This allows a single pipeline to handle multiple perils or risk variations without separate coverages.

**Fields:**

| Field | Condition |
|---|---|
| `path` | Path expression to the value to test |
| `equals` | String equality (case-insensitive) |
| `notEquals` | String inequality |
| `isTrue` | Expects `"true"` or `"false"` from the path |
| `greaterThan` | Numeric greater-than |
| `lessThan` | Numeric less-than |
| `greaterThanOrEqual` | Numeric >= |
| `lessThanOrEqual` | Numeric <= |
| `in` | Comma-separated list — value must be in the set |
| `notIn` | Comma-separated list — value must not be in the set |

**Examples:**

Run only for a specific peril:
```json
"when": { "path": "$peril", "equals": "GRP1" }
```

Run for multiple perils:
```json
"when": { "path": "$peril", "in": "GRP2,SPL" }
```

Run only when a boolean flag is set:
```json
"when": { "path": "$risk.HasPoolLiability", "isTrue": "true" }
```

Run when a field is not empty:
```json
"when": { "path": "$risk.WindHailDedCode", "notEquals": "" }
```

Run when insured value exceeds a threshold:
```json
"when": { "path": "$risk.InsuredValue", "greaterThan": "500000" }
```

---

### 5.4 Lookup Modes

When a `lookup` step needs to find a rate table row, three matching strategies are available:

---

#### Mode 1: Exact Match (default)

All keys must match exactly (or the row key must be `"*"`). Use for categorical dimensions like state, occupancy type, construction class.

No extra configuration — this is the default when neither `interpolate` nor `rangeKey` is set.

**Rate table example:**
```json
[
  { "Key1": "FIRE", "Key2": "NJ", "Factor": 1.25, "EffStart": "2026-01-01" },
  { "Key1": "FIRE", "Key2": "*",  "Factor": 1.00, "EffStart": "2026-01-01" }
]
```

The row with `Key2 = "*"` acts as a fallback for any state not explicitly listed.

---

#### Mode 2: Numeric Interpolation

One key dimension holds numeric breakpoints. For a requested value between two breakpoints, the factor is linearly interpolated. For values above the highest breakpoint, an optional tiered rate can be applied.

Set `interpolate.key` to the name of the key that is numeric:

```json
{
  "id": "S2_AOI",
  "name": "Amount of Insurance Factor",
  "operation": "lookup",
  "rateTable": "AOIFactor",
  "keys": {
    "Peril":    "$peril",
    "Coverage": "$risk.CoverageA"
  },
  "math": { "type": "add" },
  "interpolate": { "key": "Coverage" }
}
```

**Rate table for interpolation:**
```json
[
  { "Key1": "FIRE", "Key2": "100000", "Additive": 15.00, "EffStart": "2026-01-01" },
  { "Key1": "FIRE", "Key2": "200000", "Additive": 22.50, "EffStart": "2026-01-01" },
  { "Key1": "FIRE", "Key2": "300000", "Additive": 30.00, "AdditionalRate": 1.5, "AdditionalUnit": 10000, "EffStart": "2026-01-01" }
]
```

**Interpolation behavior:**

| Requested value | Result |
|---|---|
| `100000` | `15.00` (exact breakpoint) |
| `150000` | `18.75` (linear: `15 + 0.5 × (22.5 - 15)`) |
| `200000` | `22.50` (exact breakpoint) |
| `250000` | `26.25` (linear: `22.5 + 0.5 × (30 - 22.5)`) |
| `350000` | `37.50` (above max: `30 + (50000/10000) × 1.5`) |
| `50000`  | `15.00` (below min: clamped to lowest breakpoint) |

The `AdditionalRate` and `AdditionalUnit` fields on the highest breakpoint row define the tiered rate applied above the ceiling: `factor = maxFactor + (excess / unit) × additionalRate`.

---

#### Mode 3: Range-Based Lookup

The row is selected by testing whether a numeric risk value falls within the row's `[RangeFrom, RangeTo]` bounds (both inclusive). Use for deductible tiers, limit bands, or any stepped scale.

Set `rangeKey.key` to the risk bag field to test:

```json
{
  "id": "S7_DEDFACTOR",
  "name": "Deductible Factor",
  "operation": "lookup",
  "rateTable": "DeductibleFactor",
  "keys": { "DedCode": "$risk.DeductibleCode" },
  "rangeKey": { "key": "InsuredValue" },
  "math": { "type": "mul" }
}
```

**Rate table for range lookup:**
```json
[
  { "Key1": "DED1000", "RangeFrom":      0, "RangeTo":  499999, "Factor": 0.95, "EffStart": "2026-01-01" },
  { "Key1": "DED1000", "RangeFrom": 500000, "RangeTo":  999999, "Factor": 0.92, "EffStart": "2026-01-01" },
  { "Key1": "DED1000", "RangeFrom":1000000, "RangeTo": 9999999, "Factor": 0.88, "EffStart": "2026-01-01" }
]
```

For a risk with `InsuredValue = 750000` and `DeductibleCode = "DED1000"`, this matches the second row (500000 ≤ 750000 ≤ 999999) and applies a factor of `0.92`.

The `keys` dict still uses exact matching — only the `rangeKey` field is tested as a range.

---

## 6. End-to-End Configuration Walkthrough

This section walks through configuring a complete simple product from scratch.

**Scenario:** Homeowners Dwelling coverage, rated by peril (FIRE and HURRICANE). Pipeline:
1. Look up base loss cost by peril, occupancy, and zone
2. Add amount-of-insurance adjustment (interpolated by coverage amount)
3. Round after base section
4. Multiply by loss cost multiplier (by state)
5. Multiply by protection class factor
6. Compute final premium as rate × insured value / 100
7. Round final

> **Rate table scoping**: Because rate tables are scoped to a coverage config, you must create the coverage first to obtain its `id`, then create the rate tables under it. The pipeline can simply refer to tables by short names like `BaseLossCost` — no need to namespace them globally.

---

### Step 1 — Create the Coverage

```
POST /admin/coverages
```
```json
{
  "productCode": "HO-PRIMARY",
  "state": "NJ",
  "coverageCode": "DWELLING",
  "version": "2026.02",
  "effStart": "2026-02-01",
  "perils": ["FIRE", "HURRICANE"],
  "pipeline": [
    {
      "id": "S1_BASE",
      "name": "Base Loss Cost",
      "operation": "lookup",
      "rateTable": "BaseLossCost",
      "keys": { "Peril": "$peril", "OccupancyType": "$risk.Occupancy", "Zone": "$risk.Zone" },
      "math": { "type": "set" }
    },
    {
      "id": "S2_AOI",
      "name": "Amount of Insurance Adjustment",
      "operation": "lookup",
      "rateTable": "AOIFactor",
      "keys": { "Peril": "$peril", "CoverageA": "$risk.CoverageA" },
      "math": { "type": "add" },
      "interpolate": { "key": "CoverageA" }
    },
    {
      "id": "ROUND_BASE",
      "name": "Round Base Section",
      "operation": "round",
      "round": { "precision": 6, "mode": "AwayFromZero" }
    },
    {
      "id": "S3_LCM",
      "name": "Loss Cost Multiplier",
      "operation": "lookup",
      "rateTable": "LCM",
      "keys": { "State": "$risk.State" },
      "math": { "type": "mul" }
    },
    {
      "id": "S4_PROTCLASS",
      "name": "Protection Class Factor",
      "operation": "lookup",
      "rateTable": "ProtClassFactor",
      "keys": { "Peril": "$peril", "ProtClass": "$risk.ProtectionClass" },
      "math": { "type": "mul" }
    },
    {
      "id": "S5_FINALPREM",
      "name": "Apply Insured Value (per $100)",
      "operation": "compute",
      "compute": {
        "expr": "$premium * $risk.InsuredValue / 100",
        "storeAs": "FinalPremium",
        "applyToPremium": true
      }
    },
    {
      "id": "ROUND_FINAL",
      "name": "Round Final Premium",
      "operation": "round",
      "round": { "precision": 2, "mode": "AwayFromZero" }
    }
  ]
}
```

**Response** `201 Created` — note the `id` returned (e.g., `1`):
```json
{ "id": 1 }
```

---

### Step 2 — Create the Rate Tables

Rate tables are created under the coverage config using the `id` from Step 1. Table names are short and unambiguous because they are scoped to this coverage.

**Table: `BaseLossCost`**

```
POST /admin/coverages/1/rate-tables
```
```json
{
  "coverageConfigId": 1,
  "name": "BaseLossCost",
  "description": "Homeowners base loss costs per $100 of insured value",
  "lookupType": "exact",
  "effStart": "2026-01-01",
  "columnDefs": [
    { "columnName": "Key1",   "displayLabel": "Peril",         "dataType": "string",  "sortOrder": 1, "isRequired": true },
    { "columnName": "Key2",   "displayLabel": "Occupancy Type","dataType": "string",  "sortOrder": 2, "isRequired": true },
    { "columnName": "Key3",   "displayLabel": "Zone",          "dataType": "string",  "sortOrder": 3, "isRequired": true },
    { "columnName": "Factor", "displayLabel": "Base Rate",     "dataType": "decimal", "sortOrder": 4, "isRequired": true }
  ]
}
```

**Load rows in bulk:**

```
POST /admin/coverages/1/rate-tables/BaseLossCost/rows/bulk
```
```json
{
  "rows": [
    { "key1": "FIRE",      "key2": "OWNER",  "key3": "Z1", "factor": 0.42, "effStart": "2026-01-01" },
    { "key1": "FIRE",      "key2": "OWNER",  "key3": "Z2", "factor": 0.55, "effStart": "2026-01-01" },
    { "key1": "FIRE",      "key2": "TENANT", "key3": "Z1", "factor": 0.52, "effStart": "2026-01-01" },
    { "key1": "HURRICANE", "key2": "OWNER",  "key3": "Z1", "factor": 0.68, "effStart": "2026-01-01" },
    { "key1": "HURRICANE", "key2": "OWNER",  "key3": "Z2", "factor": 0.85, "effStart": "2026-01-01" },
    { "key1": "HURRICANE", "key2": "TENANT", "key3": "Z1", "factor": 0.75, "effStart": "2026-01-01" }
  ]
}
```

**Table: `AOIFactor`** (interpolated)

```
POST /admin/coverages/1/rate-tables
```
```json
{
  "coverageConfigId": 1,
  "name": "AOIFactor",
  "description": "Amount of insurance additive factor",
  "lookupType": "interpolate",
  "interpolationKeyCol": "Key2",
  "effStart": "2026-01-01",
  "columnDefs": [
    { "columnName": "Key1",     "displayLabel": "Peril",          "dataType": "string",  "sortOrder": 1, "isRequired": true },
    { "columnName": "Key2",     "displayLabel": "Coverage Amount", "dataType": "decimal", "sortOrder": 2, "isRequired": true },
    { "columnName": "Additive", "displayLabel": "AOI Factor",      "dataType": "decimal", "sortOrder": 3, "isRequired": true }
  ]
}
```

```
POST /admin/coverages/1/rate-tables/AOIFactor/rows/bulk
```
```json
{
  "rows": [
    { "key1": "FIRE",      "key2": "100000", "additive": 0.05, "effStart": "2026-01-01" },
    { "key1": "FIRE",      "key2": "250000", "additive": 0.08, "effStart": "2026-01-01" },
    { "key1": "FIRE",      "key2": "500000", "additive": 0.12, "additionalRate": 0.000008, "additionalUnit": 1000, "effStart": "2026-01-01" },
    { "key1": "HURRICANE", "key2": "100000", "additive": 0.06, "effStart": "2026-01-01" },
    { "key1": "HURRICANE", "key2": "250000", "additive": 0.09, "effStart": "2026-01-01" },
    { "key1": "HURRICANE", "key2": "500000", "additive": 0.14, "additionalRate": 0.000010, "additionalUnit": 1000, "effStart": "2026-01-01" }
  ]
}
```

**Table: `LCM`** — loss cost multiplier by state

```
POST /admin/coverages/1/rate-tables/LCM/rows/bulk
```
```json
{
  "rows": [
    { "key1": "NJ", "factor": 1.25, "effStart": "2026-01-01" },
    { "key1": "NY", "factor": 1.40, "effStart": "2026-01-01" },
    { "key1": "*",  "factor": 1.00, "effStart": "2026-01-01" }
  ]
}
```

**Table: `ProtClassFactor`** — protection class multiplier

```
POST /admin/coverages/1/rate-tables/ProtClassFactor/rows/bulk
```
```json
{
  "rows": [
    { "key1": "FIRE", "key2": "1",  "factor": 0.85, "effStart": "2026-01-01" },
    { "key1": "FIRE", "key2": "3",  "factor": 0.95, "effStart": "2026-01-01" },
    { "key1": "FIRE", "key2": "5",  "factor": 1.00, "effStart": "2026-01-01" },
    { "key1": "FIRE", "key2": "8",  "factor": 1.15, "effStart": "2026-01-01" },
    { "key1": "FIRE", "key2": "*",  "factor": 1.00, "effStart": "2026-01-01" },
    { "key1": "HURRICANE", "key2": "*", "factor": 1.00, "effStart": "2026-01-01" }
  ]
}
```

---

### Step 3 — Create the Product

```
POST /admin/products
{
  "productCode": "HO-PRIMARY",
  "version": "2026.02",
  "effStart": "2026-02-01",
  "coverages": [
    { "coverageCode": "DWELLING", "coverageVersion": "2026.02" }
  ]
}
```

---

### Step 4 — Submit a Quote

```
POST /quote/rate
Authorization: Bearer <token>
X-Tenant-Id: tenant-acme

{
  "productCode":       "HO-PRIMARY",
  "rateState":         "NJ",
  "coverageCode":      "DWELLING",
  "rateEffectiveDate": "2026-06-01",
  "risk": {
    "Zone":            "Z1",
    "Occupancy":       "OWNER",
    "ProtectionClass": "3",
    "InsuredValue":    "250000",
    "CoverageA":       "250000"
  }
}
```

> `rateState` and `rateEffectiveDate` are routing parameters used by the engine to resolve the correct pipeline. They are separate from the `risk` dictionary, which contains only rating factors.

---

## 7. Quote API Reference

All Quote API endpoints require:
- `Authorization: Bearer <token>` (scope: `quote.access`)
- `X-Tenant-Id: <tenant-id>`

### Routing Parameters

Every quote request contains two **engine-level routing parameters** that are distinct from the risk dictionary:

| Parameter | Type | Description |
|---|---|---|
| `rateState` | `string` | Two-letter state code (e.g., `"NJ"`). Used to select the state-specific pipeline. |
| `rateEffectiveDate` | `date` | ISO date (e.g., `"2026-06-01"`). Used to resolve the active product manifest and coverage config version. |

The engine resolves the correct pipeline automatically — callers never specify a version string.

---

### Rate a Single Coverage

```
POST /quote/rate
```

**Request Body:**
```json
{
  "productCode":       "HO-PRIMARY",
  "rateState":         "NJ",
  "coverageCode":      "DWELLING",
  "rateEffectiveDate": "2026-06-01",
  "risk": {
    "Zone":            "Z1",
    "Occupancy":       "OWNER",
    "ProtectionClass": "3",
    "InsuredValue":    "250000",
    "CoverageA":       "250000"
  }
}
```

**Response:**
```json
{
  "coveragePremium": 584.37,
  "perils": [
    {
      "peril": "FIRE",
      "premium": 328.19,
      "trace": [
        { "stepId": "S1_BASE",     "stepName": "Base Loss Cost",         "rateTable": "BaseLossCost",     "keys": { "Peril": "FIRE", "OccupancyType": "OWNER", "Zone": "Z1" }, "factor": 0.42, "before": 0.00,    "after": 0.42,   "note": "set" },
        { "stepId": "S2_AOI",      "stepName": "AOI Adjustment",         "rateTable": "AOIFactor",        "keys": { "Peril": "FIRE", "CoverageA": "250000" },                  "factor": 0.08, "before": 0.42,    "after": 0.50,   "note": null },
        { "stepId": "ROUND_BASE",  "stepName": "Round Base Section",     "rateTable": null,               "keys": null,                                                         "factor": null, "before": 0.50,    "after": 0.50,   "note": "round(6,AwayFromZero)" },
        { "stepId": "S3_LCM",      "stepName": "Loss Cost Multiplier",   "rateTable": "LCM",              "keys": { "State": "NJ" },                                            "factor": 1.25, "before": 0.50,    "after": 0.625,  "note": null },
        { "stepId": "S4_PROTCLASS","stepName": "Protection Class Factor","rateTable": "ProtClassFactor",  "keys": { "Peril": "FIRE", "ProtClass": "3" },                        "factor": 0.95, "before": 0.625,   "after": 0.5938, "note": null },
        { "stepId": "S5_FINALPREM","stepName": "Apply Insured Value",    "rateTable": null,               "keys": null,                                                         "factor": null, "before": 0.5938,  "after": 328.19, "note": null },
        { "stepId": "ROUND_FINAL", "stepName": "Round Final Premium",    "rateTable": null,               "keys": null,                                                         "factor": null, "before": 328.19,  "after": 328.19, "note": "round(2,AwayFromZero)" }
      ]
    },
    {
      "peril": "HURRICANE",
      "premium": 256.18,
      "trace": [ /* ... */ ]
    }
  ]
}
```

The `trace` array is your full audit trail — each step records what factor was retrieved, what the premium was before and after, and which rate table keys were used.

---

### Rate Policy-Level Segments

Used when a policy has multiple time segments (e.g., mid-term changes). Each segment is rated independently and pro-rated by the fraction of policy days it represents.

```
POST /quote/rate-policy-segments
```

**Request Body:**
```json
{
  "productCode": "HO-PRIMARY",
  "rateState":   "NJ",
  "policyFrom":  "2026-01-01",
  "policyTo":    "2027-01-01",
  "segments": [
    {
      "from":              "2026-01-01",
      "to":                "2026-07-01",
      "rateEffectiveDate": "2026-01-01",
      "property": {
        "Zone": "Z1", "Occupancy": "OWNER",
        "ProtectionClass": "3", "InsuredValue": "200000", "CoverageA": "200000"
      },
      "coverages": [
        { "id": "DWELLING", "name": "Dwelling", "params": {} }
      ]
    },
    {
      "from":              "2026-07-01",
      "to":                "2027-01-01",
      "rateEffectiveDate": "2026-07-01",
      "property": {
        "Zone": "Z1", "Occupancy": "OWNER",
        "ProtectionClass": "3", "InsuredValue": "250000", "CoverageA": "250000"
      },
      "coverages": [
        { "id": "DWELLING", "name": "Dwelling", "params": {} }
      ]
    }
  ]
}
```

**Response:**
```json
{
  "policyTotal": 572.14,
  "coverages": [
    {
      "id": "DWELLING",
      "name": "Dwelling",
      "coverageTotal": 572.14,
      "segments": [
        {
          "from": "2026-01-01",
          "to":   "2026-07-01",
          "prorationFactor": 0.4959,
          "segmentPremium": 263.11,
          "perils": [ /* trace */ ]
        },
        {
          "from": "2026-07-01",
          "to":   "2027-01-01",
          "prorationFactor": 0.5041,
          "segmentPremium": 309.03,
          "perils": [ /* trace */ ]
        }
      ]
    }
  ]
}
```

The pro-ration factor is `segmentDays / totalPolicyDays`. Segment premiums are rounded to 2 decimal places.

---

### Rate Coverage-Level Segments

Similar to policy segments, but segments are defined per coverage rather than across all coverages in a segment. Useful when coverages have different change dates.

```
POST /quote/rate-coverage-segments
```

**Request Body shape** (same routing parameters at the top level):
```json
{
  "productCode": "HO-PRIMARY",
  "rateState":   "NJ",
  "policyFrom":  "2026-01-01",
  "policyTo":    "2027-01-01",
  "coverages": [
    {
      "id":   "DWELLING",
      "name": "Dwelling",
      "segments": [
        {
          "from":              "2026-01-01",
          "to":                "2026-07-01",
          "rateEffectiveDate": "2026-01-01",
          "property":          { "Zone": "Z1", "Occupancy": "OWNER", "InsuredValue": "200000", "CoverageA": "200000" },
          "params":            {}
        }
      ]
    }
  ]
}
```

---

## 8. Rate Table Design Patterns

### Pattern 1: Simple Factor Table

One to three categorical keys, one `Factor` column. Use for most lookup tables.

```
Key1 (Peril)  | Key2 (State) | Factor
--------------+--------------+--------
FIRE          | NJ           | 1.25
FIRE          | NY           | 1.40
FIRE          | *            | 1.00
HURRICANE     | NJ           | 1.80
```

---

### Pattern 2: Wildcard Fallback

Add a catch-all row with `Key = "*"` so lookups never fail when an exact match is absent.

```
Key1          | Key2         | Factor
FRAME         | PPC1         | 0.85
FRAME         | PPC3         | 0.95
FRAME         | *            | 1.00      ← wildcard: any PPC not explicitly listed
MASONRY       | PPC1         | 0.80
MASONRY       | *            | 1.00
*             | *            | 1.00      ← catch-all fallback
```

---

### Pattern 3: Interpolation Table

Numeric breakpoints in the interpolation key column. Always include `AdditionalRate` and `AdditionalUnit` on the highest breakpoint row to handle values above the ceiling.

```
Key1      | Key2 (InsuredValue) | Additive | AdditionalRate | AdditionalUnit
----------+--------------------+----------+----------------+---------------
FIRE      | 100000             | 0.05     |                |
FIRE      | 250000             | 0.08     |                |
FIRE      | 500000             | 0.12     | 0.000008       | 1000
```

Values below 100000 get factor 0.05 (clamped). Values above 500000 get `0.12 + (excess / 1000) × 0.000008`.

---

### Pattern 4: Range Table

Use `RangeFrom` / `RangeTo` for tiered scales. Every possible input value must be covered by a row's range.

```
Key1      | RangeFrom | RangeTo   | Factor
----------+-----------+-----------+-------
DED1000   |         0 |   499999  | 0.95
DED1000   |    500000 |   999999  | 0.92
DED1000   |   1000000 |  99999999 | 0.88
```

---

### Pattern 5: Effective Date Rows

To update a factor on a specific date without removing history, add a new row with a later `EffStart`. The engine always picks the most recent effective row on or before the quote's effective date.

```
Key1  | Key2 | Factor | EffStart
------+------+--------+------------
FIRE  | NJ   | 1.25   | 2026-01-01   ← used for quotes before 2026-07-01
FIRE  | NJ   | 1.30   | 2026-07-01   ← used for quotes on/after 2026-07-01
```

---

## 9. Worked Premium Calculation Examples

### Example 1: Simple Two-Peril Homeowners

**Inputs:**
- Product: `HO-PRIMARY`
- Rate State: `NJ`
- Coverage: `DWELLING`
- Rate Effective Date: `2026-06-01`
- Risk: `Zone=Z1`, `Occupancy=OWNER`, `ProtectionClass=3`, `InsuredValue=250000`, `CoverageA=250000`

**Pipeline Execution — FIRE Peril:**

| Step | Operation | Factor | Before | After | Notes |
|---|---|---|---|---|---|
| S1_BASE | set | **0.42** | 0.00 | **0.42** | BaseLossCost[FIRE, OWNER, Z1] |
| S2_AOI | add | **0.08** | 0.42 | **0.50** | AOIFactor interpolated at CoverageA=250000 (between 100000→0.05 and 500000→0.12, t=0.6 → 0.05+0.6×(0.12-0.05)=0.092; rounded to 0.08 in this example) |
| ROUND_BASE | round | — | 0.50 | **0.50** | 6 decimal places |
| S3_LCM | mul | **1.25** | 0.50 | **0.625** | LCM[NJ] |
| S4_PROTCLASS | mul | **0.95** | 0.625 | **0.5938** | ProtClassFactor[FIRE, PPC3] |
| S5_FINALPREM | compute | — | 0.5938 | **1484.38** | `0.5938 × 250000 / 100` |
| ROUND_FINAL | round | — | 1484.38 | **1484.38** | 2 decimal places |

**FIRE Premium: $1,484.38**

---

**Pipeline Execution — HURRICANE Peril:**

| Step | Operation | Factor | Before | After | Notes |
|---|---|---|---|---|---|
| S1_BASE | set | **0.68** | 0.00 | **0.68** | BaseLossCost[HURRICANE, OWNER, Z1] |
| S2_AOI | add | **0.09** | 0.68 | **0.77** | AOIFactor[HURRICANE, 250000] |
| ROUND_BASE | round | — | 0.77 | **0.77** | |
| S3_LCM | mul | **1.25** | 0.77 | **0.9625** | LCM[NJ] |
| S4_PROTCLASS | mul | **1.00** | 0.9625 | **0.9625** | No HURRICANE PPC factor → wildcard=1.00 |
| S5_FINALPREM | compute | — | 0.9625 | **2406.25** | `0.9625 × 250000 / 100` |
| ROUND_FINAL | round | — | 2406.25 | **2406.25** | |

**HURRICANE Premium: $2,406.25**

**Total Coverage Premium: $1,484.38 + $2,406.25 = $3,890.63**

---

### Example 2: Conditional Pipeline (Multi-Peril with When Guards)

**Coverage:** `BUILDINGCVG` with perils `GRP1`, `GRP2`, `SPL`

For peril `GRP1`:
- S1_GRP1_BASERATE executes (`when.equals = "GRP1"` ✓)
- S1_GRP2_BASERATE skipped (`when.equals = "GRP2"` ✗)
- S1_SPL_BASERATE skipped (`when.equals = "SPL"` ✗)
- S5_GRP1_COINSFACTOR executes (`when.equals = "GRP1"` ✓)
- S5_GRP2SPL_COINSFACTOR skipped (`when.in = "GRP2,SPL"` ✗)

For peril `GRP2`:
- S1_GRP1_BASERATE skipped
- S1_GRP2_BASERATE executes
- S5_GRP1_COINSFACTOR skipped
- S5_GRP2SPL_COINSFACTOR executes (`"GRP2"` is in `"GRP2,SPL"` ✓)

This allows one pipeline to cover multiple perils with different calculation paths, while sharing common steps like LCM and rounding.

---

### Example 3: Interpolation with Tiered Rate Above Ceiling

**Table: AOIFactor**
```
FIRE, 100000, Additive=15.00
FIRE, 200000, Additive=22.50
FIRE, 300000, Additive=30.00, AdditionalRate=1.5, AdditionalUnit=10000
```

**Requested CoverageA = 350000 (above max breakpoint of 300000)**

Excess = 350000 − 300000 = 50000
Tiers = 50000 / 10000 = 5
Result = 30.00 + 5 × 1.5 = **37.50**

**Requested CoverageA = 175000 (between 100000 and 200000)**

t = (175000 − 100000) / (200000 − 100000) = 0.75
Result = 15.00 + 0.75 × (22.50 − 15.00) = 15.00 + 5.625 = **20.625**

---

### Example 4: Range-Based Deductible Factor

**Table: DeductibleFactor**
```
Key1=DED1000, RangeFrom=0,       RangeTo=499999,  Factor=0.95
Key1=DED1000, RangeFrom=500000,  RangeTo=999999,  Factor=0.92
Key1=DED1000, RangeFrom=1000000, RangeTo=9999999, Factor=0.88
```

**Step configuration:**
```json
{
  "id": "S7_DEDFACTOR",
  "operation": "lookup",
  "rateTable": "DeductibleFactor",
  "keys": { "DedCode": "$risk.DeductibleCode" },
  "rangeKey": { "key": "InsuredValue" },
  "math": { "type": "mul" }
}
```

For `InsuredValue=750000` and `DeductibleCode=DED1000`:
- Key1 matches `DED1000` ✓
- 500000 ≤ 750000 ≤ 999999 ✓ → Factor = **0.92**
- `premium = premium × 0.92`

---

---

## 10. Commercial Multi-LOB Rating

The `POST /quote/rate-commercial` endpoint supports commercial products where a single policy submission spans multiple **Lines of Business (LOBs)**, each containing one or more rated **risks** at different hierarchy levels (building, location, policy).

### 10.1 Key Concepts

| Concept | Description |
|---|---|
| **LOB** | A line of business within the policy (e.g., PROP, GL, LIQLIAB, AUTO) |
| **Risk** | A rated unit within a LOB — a building, location, or policy-level risk |
| **RiskLevel** | The hierarchy tier of the risk: `"BUILDING"`, `"LOCATION"`, or `"POLICY"` |
| **RatingType** | Per-coverage: `"NORMAL"` (single pass) or `"SCHEDLEVEL"` (one pass per schedule item) |

These two dimensions are **orthogonal** — any risk at any level can have any coverage, and any coverage can use either rating type. For example, a POLICY-level Inland Marine coverage can still use `SCHEDLEVEL` to rate individual scheduled items.

### 10.2 Product Manifest with LOBs

Commercial products declare their coverages under named LOBs in the product manifest:

```json
{
  "productCode": "BOP",
  "version": "2026.02",
  "effectiveStart": "2026-02-01",
  "coverages": [],
  "lobs": [
    {
      "lobCode": "PROP",
      "coverages": [
        { "coverageCode": "BLDG",   "version": "2026.02" },
        { "coverageCode": "BPP",    "version": "2026.02" },
        { "coverageCode": "IM",     "version": "2026.02" }
      ]
    },
    {
      "lobCode": "GL",
      "coverages": [
        { "coverageCode": "GL-OCC", "version": "2026.02" },
        { "coverageCode": "GL-AGG", "version": "2026.02" }
      ]
    }
  ]
}
```

Personal lines products continue to use the flat `coverages` array — no changes required to existing configurations.

### 10.3 Risk Bag Merge Chain

The engine constructs a merged risk bag for every rated unit using this precedence order (later entries override earlier):

```
PolicyRisk  →  LobRisk  →  Risk.Attributes  →  CoverageParams  →  ScheduleFields
```

- **PolicyRisk** — attributes shared across all LOBs (e.g., state, policy form)
- **LobRisk** — attributes shared across all risks in a LOB (e.g., `OccupancyType` for PROP)
- **Risk.Attributes** — the specific risk's own fields (e.g., `Construction`, `YearBuilt` for a building)
- **CoverageParams** — coverage-specific parameters (e.g., `CoverageLimit`)
- **ScheduleFields** — individual item fields for `SCHEDLEVEL` coverages (e.g., `ItemValue`)

This means a coverage param can override a risk attribute, and a schedule field can override everything — giving fine-grained control at each level.

### 10.4 Request Structure

```
POST /quote/rate-commercial
Authorization: Bearer <token>
X-Tenant-Id: <tenant>
Content-Type: application/json
```

```json
{
  "productCode": "BOP",
  "rateState": "NJ",
  "rateEffectiveDate": "2026-03-01",
  "policyRisk": {
    "PolicyForm": "BOP-STD",
    "State": "NJ"
  },
  "lobs": [
    {
      "lobCode": "PROP",
      "lobRisk": { "OccupancyType": "Office" },
      "risks": [
        {
          "riskId": "B1",
          "riskLevel": "BUILDING",
          "locationId": "L1",
          "attributes": {
            "Construction": "Frame",
            "YearBuilt": "1995",
            "Sprinklered": "True"
          },
          "coverages": [
            {
              "id": "BLDG",
              "name": "Building",
              "params": { "CoverageLimit": "2000000" }
            },
            {
              "id": "BPP",
              "name": "Business Personal Property",
              "params": { "CoverageLimit": "500000" }
            }
          ]
        },
        {
          "riskId": "POL",
          "riskLevel": "POLICY",
          "attributes": {},
          "coverages": [
            {
              "id": "IM",
              "name": "Inland Marine",
              "ratingType": "SCHEDLEVEL",
              "params": { "ClassCode": "IM-EQUIP" },
              "schedules": [
                { "ScheduleId": "I1", "ItemType": "Equipment", "ItemValue": "50000", "Description": "Crane" },
                { "ScheduleId": "I2", "ItemType": "Equipment", "ItemValue": "25000", "Description": "Generator" }
              ]
            }
          ]
        }
      ]
    },
    {
      "lobCode": "GL",
      "lobRisk": {},
      "risks": [
        {
          "riskId": "L1",
          "riskLevel": "LOCATION",
          "attributes": { "ClassCode": "41650", "Exposure": "150000" },
          "coverages": [
            { "id": "GL-OCC", "name": "GL Occurrence", "params": { "OccurrenceLimit": "1000000" } },
            { "id": "GL-AGG", "name": "GL Aggregate",  "params": { "AggregateLimit": "2000000" } }
          ]
        }
      ]
    }
  ]
}
```

### 10.5 Response Structure

Premiums are rolled up: schedule items → coverage → risk → LOB → policy.

```json
{
  "policyTotal": 18500.00,
  "lobs": [
    {
      "lobCode": "PROP",
      "lobTotal": 14200.00,
      "risks": [
        {
          "riskId": "B1",
          "riskLevel": "BUILDING",
          "locationId": "L1",
          "riskTotal": 7800.00,
          "coverages": [
            {
              "id": "BLDG",
              "name": "Building",
              "premium": 6200.00,
              "perils": [
                { "peril": "FIRE",  "premium": 3100.00, "trace": [...] },
                { "peril": "WIND",  "premium": 2100.00, "trace": [...] },
                { "peril": "THEFT", "premium": 1000.00, "trace": [...] }
              ]
            },
            {
              "id": "BPP",
              "name": "Business Personal Property",
              "premium": 1600.00,
              "perils": [...]
            }
          ]
        },
        {
          "riskId": "POL",
          "riskLevel": "POLICY",
          "locationId": null,
          "riskTotal": 6400.00,
          "coverages": [
            {
              "id": "IM",
              "name": "Inland Marine",
              "premium": 6400.00,
              "schedules": [
                { "scheduleId": "I1", "premium": 4200.00, "perils": [...] },
                { "scheduleId": "I2", "premium": 2200.00, "perils": [...] }
              ]
            }
          ]
        }
      ]
    },
    {
      "lobCode": "GL",
      "lobTotal": 4300.00,
      "risks": [
        {
          "riskId": "L1",
          "riskLevel": "LOCATION",
          "locationId": null,
          "riskTotal": 4300.00,
          "coverages": [
            { "id": "GL-OCC", "name": "GL Occurrence", "premium": 2800.00, "perils": [...] },
            { "id": "GL-AGG", "name": "GL Aggregate",  "premium": 1500.00, "perils": [...] }
          ]
        }
      ]
    }
  ]
}
```

### 10.6 Notes for Pipeline Configuration

- **Coverage configs are unchanged** — the same `CoverageConfig` + pipeline used by personal lines works for commercial. The pipeline sees a flat `RiskBag` regardless of how it was assembled.
- **`RatingType = "SCHEDLEVEL"` on commercial coverages** works identically to the personal lines endpoints — one pipeline run per schedule entry, results summed for the coverage.
- **`$risk.RiskLevel`** is available as a risk bag key (populated from the risk's `RiskLevel` field) if a step needs to guard on hierarchy tier using a `When` condition.
- **`$risk.LocationId`** is likewise available in the bag so pipelines can reference parent-location context.
- **Admin API** — coverage configuration for commercial products is identical to personal lines. Create each commercial coverage (`BLDG`, `BPP`, `IM`, etc.) as a separate `CoverageConfig` entry via the Admin API. The LOB grouping only lives in the product manifest, not in the coverage configs.

### 10.7 SQL Schema (Database Storage)

Run `sql/02_Add_ProductLob.sql` to add the `ProductLob` table and `CoverageRef.LobId` column. Existing personal lines products and their coverage refs are unaffected (their `LobId` remains `NULL`).

---

*For questions about extending this rating engine or configuring additional step types, contact your platform team.*
