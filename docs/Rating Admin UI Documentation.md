# Rating Engine — Admin UI Design

---

## 1. Configuration Hierarchy

```
Product
  └─ LOB
      └─ Coverage Catalog
          └─ State Coverage
              ├─ Perils
              └─ Rating Steps  (reference Rate Tables, don't own them)

Rate Tables  ← separate top-level entity under a Coverage
```

Rate Tables are scoped to a Coverage (State Coverage Config) but are **not** nested inside
Rating Steps. They are a sibling entity — a step selects a rate table by reference.
This keeps tables reusable across multiple steps and independently versionable via
effective dates, without being tied to any single step's lifecycle.

---

## 2. Admin Navigation

| Section | Audience |
|---|---|
| Products | Business |
| Coverages | Business |
| Risk Field Mapping | Developer |
| Lookups & Keys | Business |
| Rating Steps | Business |
| Rate Tables | Business |
| Validation & Testing | Business / Developer |

---

## 3. Core Configuration Entities

### 3.1 Product

**Fields**

| Field | Notes |
|---|---|
| Product Name | |
| Version | e.g. 2026.02 |
| Effective From | Date |
| Effective To | Optional — blank means still active |

**Child Tabs**

- **Lines of Business** — LOBs offered under this product
- **States Supported** — declares which states this product is filed for; prevents creation
  of a state coverage config for any state not listed here

---

### 3.2 Line of Business (LOB)

Each product contains one or more LOBs. The LOB defines the natural exposure structure
for coverages beneath it.

**Fields**

| Field | Example values |
|---|---|
| LOB Name | Property, General Liability, Auto |
| Exposure Attributes | The risk fields relevant to this LOB (see examples below) |
| Allowed Aggregation Scopes | Guardrail — see §3.2.1 |

**Exposure Attribute Examples**

| LOB | Attribute | Unit |
|---|---|---|
| Property | Location TIV, Building TIV | Currency |
| General Liability | Payroll, Sales, Units, Area | Currency / Count |
| Auto | Vehicle Symbol, Territory | Code |

#### 3.2.1 LOB Aggregation Scope (Guardrail)

The LOB declares which aggregation levels are **permitted** for any coverage beneath it.
This reflects the natural exposure structure of the LOB and acts as a hard constraint.

| LOB | Permitted Scopes |
|---|---|
| Property | Per Building, Per Location, Per LOB |
| General Liability | Per Business Class, Per LOB |
| Auto | Per Vehicle, Per LOB |

> **LOB Aggregation Scope = what is allowed.** It does not dictate what must be used.

Coverage Aggregation Rules (§3.4.1) must select from within these permitted scopes.
If a coverage rule references a scope not permitted by its LOB, the system rejects it on save.

---

### 3.3 Risk Field Mapping — Developer Only

Developers map raw JSON request paths to logical field names that business users see
throughout the system. Business users never interact with this screen.

| Logical Name | JSON Path | Data Type | LOB |
|---|---|---|---|
| Building TIV | $.policy.locations[*].buildings[*].tiv | Number | Property |
| State | $.policy.state | String | All |
| Construction Type | $.policy.locations[*].buildings[*].constructionType | String | Property |
| Payroll | $.policy.lob.classes[*].payroll | Number | GL |

Once mapped here, the logical name (e.g. "Building TIV") appears everywhere else in the
admin UI — in step conditions, lookup key sources, aggregate field pickers, and rate table
key bindings. No JSON path is ever shown to a business user.

---

### 3.4 Lookups & Keys — Business Layer

A distinct layer from Risk Field Mapping. Where Risk Field Mapping answers
*"where does this value come from in the request?"*, Lookups & Keys answers
*"what are the valid values and how are composite values derived?"*

**3.4.1 Lookup Dimensions (Enumerations)**

Define the allowed values for categorical fields used in rate table keys and conditions.
Business users pick from dropdowns rather than typing free text.

| Dimension | Allowed Values |
|---|---|
| Construction Type | Frame, Masonry, Masonry Veneer, Fire Resistive, Modified Fire Resistive |
| Occupancy Class | Residential, Commercial, Industrial |
| Business Class | Retail, Manufacturing, Office, Restaurant |
| Protection Class | 1 – 10 |

**3.4.2 Derived Keys (Aggregations)**

Define composite values that are calculated from the request before rating begins.
These become available as inputs to rating steps and conditions without needing a
separate compute step in every pipeline.

| Derived Key | Readable Name | Expression |
|---|---|---|
| LOB_TIV | LOB Total Insured Value | SUM of Building TIV across all locations |
| Location_Count | Number of Locations | COUNT of locations |
| GL_Total_Payroll | Total GL Payroll | SUM of Payroll across all business classes |

> Business users select "LOB Total Insured Value" from a dropdown; the system resolves
> the underlying aggregation. No formulas are exposed.

---

### 3.5 Coverage Catalog

Defines the portfolio of coverage types available under a product and LOB,
independent of state or effective date.

**Fields**

| Field | Notes |
|---|---|
| Coverage Name | e.g. Building, Contents, Earthquake |
| Effective From / To | Catalog-level validity |
| LOB | Which LOB this coverage belongs to |
| Aggregation Rule | See §3.5.1 — selected from LOB's permitted scopes |
| Perils | List of peril codes applicable to this coverage |

#### 3.5.1 Coverage Aggregation Rule

Specifies how this coverage performs its rating and premium roll-up.
Must be one of the scopes permitted by the parent LOB (§3.2.1).

| Coverage | Aggregation Rule | Meaning |
|---|---|---|
| Building Coverage | Per Building | Rate and premium calculated separately per building; totals summed |
| Earthquake | Per LOB | Rate applied once to total LOB TIV; single premium produced |
| Terrorism | Per Policy | Applied as a percentage of total policy premium across all LOBs |

> **Coverage Aggregation Rule = what is selected within the LOB's permitted scopes.**
> LOB scope always takes authority. The Coverage rule specifies the actual actuarial method.

**Coverage with Perils — Roll-up Options**

When a coverage has multiple perils, the aggregation rule also specifies how peril
premiums combine:

- **Sum** — total premium = sum of all peril premiums (e.g. Fire + Wind)
- **Maximum** — total premium = highest single peril premium
- **Independently Capped** — each peril has its own limit before combining

> Which perils are included on a given policy is a quoting workflow decision, not a rating
> configuration decision. The rating engine runs its pipeline for whatever perils it receives.

---

### 3.6 State Coverage Config

Links a Coverage Catalog entry to a specific state filing with its own effective dates,
peril list, and rating pipeline. Inherits coverage properties from the Catalog entry;
overrides are limited to what legitimately varies by state.

**Fields**

| Field | Notes |
|---|---|
| Coverage | Selected from Catalog |
| State | Must be declared in the Product's States Supported list |
| Effective From / To | State-specific filing dates |
| Perils | Inherited from Catalog; may be restricted for this state |
| Rating Sequence | Ordered list of Rating Steps |

---

## 4. Rating Steps

### 4.1 Visual Layout

Steps are displayed as an ordered flow, not a flat list:

```
Base Rate  →  Territory Factor  →  Experience Modifier  →  Deductible Credit  →  Final Premium
```

Steps can be reordered by drag and drop.

---

### 4.2 Step Types

| Type | Purpose | Example |
|---|---|---|
| **Lookup** | Read a factor or rate from a rate table | Look up Base Rate by Construction Type and Territory |
| **Compute** | Establish a primary value from an expression | Base Premium = Building TIV × Base Rate |
| **Adjustment** | Modify an already-established value | Adjusted Premium = Prior Premium × Experience Modifier; Premium = Prior Premium − 10%; Premium = Prior Premium + $250 |
| **Rounding** | Round the running premium | Round to nearest dollar |

**Compute vs. Adjustment — the distinction**

- **Compute** produces a value from scratch. It is typically the step that establishes the
  base premium for a coverage or sub-component.
- **Adjustment** takes the current running premium as its implicit starting point and
  applies a modification. The business user selects the modification type (multiply by,
  add, subtract, cap at, floor at) without writing a formula.

This distinction matters for readability: a business user scanning a pipeline can
immediately see which steps build the premium and which steps adjust it.

---

### 4.3 Step Definition

#### Identity

| Field | Notes |
|---|---|
| Step Name | Friendly label shown in the flow (e.g. "Territory Factor") |
| Step Code | Auto-generated from the Step Name; editable if needed |
| Step Type | Lookup / Compute / Adjustment / Rounding |
| Execution Order | Set by drag and drop |

#### Calculation Type (radio buttons)

| Option | Meaning |
|---|---|
| Set as base value | Result becomes the new running premium |
| Multiply by | Running premium × factor |
| Add flat amount | Running premium + amount |
| Subtract flat amount | Running premium − amount |
| Apply minimum | Running premium cannot go below this value |
| Apply maximum | Running premium cannot exceed this value |

#### Operation Source

| Source | Meaning |
|---|---|
| Rate Table | Factor or amount read from a configured rate table |
| Constant Value | A fixed number entered directly |
| Derived from prior step | Reads the named output of an earlier step |

#### Operation Scope (cross-coverage dependencies)

Each step declares where its input premium comes from:

| Scope | Meaning |
|---|---|
| Current Coverage | Uses the running premium within this coverage's pipeline |
| Other Coverage — Final Premium | Reads the completed premium of another coverage |
| Other Coverage — Step Output | Reads a named intermediate result from another coverage |
| LOB Aggregate | Uses the LOB-level aggregated value (e.g. LOB Total Insured Value) |
| Policy Aggregate | Uses a policy-wide value (e.g. total policy premium across all LOBs) |

> **Business example:** Equipment Breakdown premium = 10% of Property Final Premium
>
> Step: Adjustment | Source: Other Coverage — Final Premium (Property) | Multiply by 0.10

#### Named Step Output (Intermediate Results)

If a step produces a value that other steps or coverages need to reference, it can be
given a named output:

| Field | Example |
|---|---|
| Output Alias | PROP_BASE_PREM |
| Readable Name | Property Base Premium |

This named result is then available in the Operation Scope picker of any subsequent step
or any dependent coverage — without exposing any JSON path or variable syntax.

#### When Condition (optional)

A step can be made conditional. The condition builder works entirely with logical field
names from the Risk Field registry and Lookup dimensions:

```
Apply when:
  State is one of  CA, TX, FL
  AND  Construction Type  equals  Frame
OR
  LOB Total Insured Value  is greater than  1,000,000
```

Operators available: equals / is not / is greater than / is less than / ≥ / ≤ /
is one of / is not one of / exists / does not exist

#### Rounding (when Step Type = Rounding)

| Option | |
|---|---|
| Nearest dollar | |
| Nearest $10 | |
| Nearest $100 | |
| Custom precision | Specify decimal places |
| Rounding method | Standard (away from zero) / Banker's (to even) |

---

## 5. Rate Tables

### 5.1 Design Principles

- Rate Tables are **first-class entities**, defined independently of any rating step.
- A step references a rate table; it does not own one.
- Tables are reusable across multiple steps and independently versionable via
  effective dates.
- One table = one business purpose. Avoid multi-purpose mega-tables.

**Recommended workflow**

1. Define Lookup Dimensions (§3.4.1)
2. Create Rate Table — bind its key columns to Lookup Dimensions
3. Enter rate rows
4. Create Rating Step — select the rate table and bind its keys to input fields

Creating a new table from within the step form is allowed as a shortcut, but the
table is always saved as a first-class entity.

---

### 5.2 Rate Table Structure

#### Metadata

| Field | Notes |
|---|---|
| Table Name | e.g. Property Base Rate — CA |
| Description | Business purpose |
| Intended for Coverage | Documentation — which coverage(s) this table is designed for |
| Effective From / To | Date range during which this table is active |

#### Key Columns

| Column role | Configuration |
|---|---|
| Lookup Keys | Exact match — bound to a Lookup Dimension (gives dropdown validation) or a Risk Field |
| Range Keys | Numeric From / To bounds (e.g. TIV Range) |
| Interpolation | Flag one numeric key column as the interpolation dimension |

#### Value Column

| Value Type | Math operation implied | Example |
|---|---|---|
| Rate | Set as base (per-unit rate) | 3.50 per $100 TIV |
| Factor | Multiply | 1.15 territory factor |
| Flat Amount | Add or Subtract | $250 minimum premium adjustment |
| Multiplier | Multiply (synonym for Factor, semantic label only) | 0.90 experience modifier |

The Value Type is metadata — it informs the default math operation when the table is
selected in a step, and it labels the value column in the rate table UI.

#### Rate Preview

The rate table UI provides a sample-input tester: enter values for each key column
and the system highlights the matching row and returns the factor, before any step
is configured.

---

## 6. Conditions & Rule Guards

Covered in §4.3 (When Condition). Additional system-level guardrails:

| Guard | Description |
|---|---|
| Required step validation | Warn if a coverage has no step that sets a base value |
| Circular dependency detection | Detect when Coverage A depends on Coverage B and B depends on A |
| Missing rate detection | Warn if a rate table has no row matching a given input combination |
| LOB scope violation | Reject a Coverage Aggregation Rule not permitted by its LOB |
| Undeclared state | Reject a State Coverage Config for a state not in the Product's States Supported list |

---

## 7. Versioning

Versioning is managed through **effective date ranges** (Effective From / Effective To)
on every configuration entity. This applies to:

- Products
- Coverage Catalog entries
- State Coverage Configs
- Rate Tables (and individual rate table rows)

**Key behaviours**

- A record with no Effective To date is considered currently active.
- Multiple versions of the same entity (same product/coverage/table name) can coexist
  with non-overlapping date ranges, supporting future-dated rate changes loaded in advance.
- The system selects the version whose date range contains the policy's rate effective date
  at rating time.
- Editing a currently active record's rates should be done by expiring the existing rows
  and adding new rows with a future Effective From, not by overwriting in place.

---

## 8. Validation & Testing

### Testing Sandbox

Provide a "Test Rating" screen where a business user can:

1. Select a Product, LOB, State, and Coverage
2. Enter sample policy values (using the logical field names, not JSON)
3. Run the rating pipeline against those inputs
4. See step-by-step output:

```
Step 1 — Base Rate (Lookup)         →  $1,200.00
Step 2 — Territory Factor (1.10)    →  $1,320.00
Step 3 — Experience Modifier (0.95) →  $1,254.00
Step 4 — Rounding                   →  $1,254.00
```

Each step shows: the value read from the rate table or expression, the math applied,
and the running premium after that step.

---

## 9. Documentation & Audit

Every configuration entity supports:

| Field | Purpose |
|---|---|
| Business Description | Plain-language explanation of what this entity does |
| Change Reason | Why this change was made |
| Reference | Ticket / filing / document reference |

---

## 10. Permissions

| Role | Capabilities |
|---|---|
| Viewer | Read-only access to all configuration |
| Editor | Create and edit Draft configurations |
| Approver | Activate configurations for a future effective date |
| Deployer | Publish configurations to production |

---

## 11. Recommended Visualisations

- **Rating flow diagram** per coverage — visual step-by-step pipeline with step type colour coding
- **Drag-and-drop step ordering** in the pipeline editor
- **Auto-generated plain English summary** derived from the actual step configuration:

  > *"For Building coverage in NJ, the premium is calculated per building by multiplying
  > the Building TIV by the base rate from the Property Base Rate table, adjusted by the
  > territory factor, then rounded to the nearest dollar."*

- **Effective date timeline** — shows which version of each rate table and coverage config
  is active today, and which become active on future dates

---

## Summary: Design Principles

| Principle | How it is applied |
|---|---|
| Business language everywhere | No JSON paths, no variable names, no code syntax visible to business users |
| Technical complexity hidden | Risk Field Mapping is developer-only; business users see logical names |
| Intent-driven UI | Business user expresses intent ("multiply by territory factor"); system resolves the mechanics |
| Strong validation | LOB scope guards, circular dependency detection, missing rate warnings |
| Clear hierarchy of authority | LOB Aggregation Scope constrains; Coverage Aggregation Rule selects within those constraints |
| Reusability | Rate Tables and Lookup Dimensions are shared entities, not embedded in steps |
