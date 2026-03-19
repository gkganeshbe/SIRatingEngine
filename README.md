
# Property Insurance Rating Engine – Starter Repo (.NET 8)

A configurable, peril-based rating engine designed from your worksheet. It supports multi-step pipelines, multi-key rate lookups, effective dating, conditional execution, and rounding at **section boundaries** via explicit `round` steps. Includes:

- **RatingEngine.Api** – Minimal API to compute premiums and return full trace
- **RatingEngine.Core** – Engine abstractions, pipeline runner, steps, in-memory lookup
- **RateImporter** – Console tool to ingest Excel rate sheets into JSON or SQL Server
- **RatingEngine.Tests** – xUnit tests and golden-master examples
- **Config & Data** – Sample product/pipeline JSON + sample rates (perils) in JSON
- **SQL** – Example schema scripts for SQL Server

> This starter is shaped by the uploaded worksheet’s step sequence and lookups (e.g., Base Loss Cost, Amount of Insurance, LCM, Peril Subzone, Protection Class).citeturn1search1

---

## Quick start

> Requires .NET 8 SDK, SQL Server (optional), and PowerShell/Bash.

```bash
# restore & build
dotnet build

# run API
dotnet run --project src/RatingEngine.Api/RatingEngine.Api.csproj

# sample request
curl -s \
  -H "Content-Type: application/json" \
  -d @data/sample-request.json \
  http://localhost:5088/quote/2026.02/rate | jq
```

The API returns: coverage premium, peril premiums, and a **step-by-step trace**.

---

## Project layout

```
src/
  RatingEngine.Api/              # Minimal API
  RatingEngine.Core/             # Engine, steps, in-memory lookup
  RatingEngine.Config/products/  # Product & pipeline JSON

tools/
  RateImporter/                  # Excel -> JSON/SQL importer

tests/
  RatingEngine.Tests/            # xUnit tests

data/
  rates/                         # Sample rate tables (JSON)
  sample-request.json            # Sample rating request

sql/
  01_Create_RateTables.sql       # Example SQL Server schema
```

---

## Rounding at Section Boundaries

Set `operation: "round"` steps wherever a section ends (e.g., after LCM, after peril subzone). Example in the product config (`HO-PRIMARY.2026.02.json`):

```json
{ "id": "ROUND_BASE", "operation": "round", "precision": 2, "mode": "AwayFromZero" }
```

This ensures you only round where intended—**not at every step**. The worksheet’s sections (e.g., Base Loss → LCM → ... → Peril Premium) map to these checkpoints.citeturn1search1

---

## Importing rates from Excel

Use the **RateImporter** tool:

```bash
# Convert Excel into JSON files under data/rates
 dotnet run --project tools/RateImporter/RateImporter.csproj \
   --excel ./Rating\ Example.xlsx \
   --out ./data/rates \
   --product HO-PRIMARY --version 2026.02 \
   --to json

# Or insert into SQL Server
 dotnet run --project tools/RateImporter/RateImporter.csproj \
   --excel ./Rating\ Example.xlsx \
   --connection "Server=localhost;Database=Rating;Trusted_Connection=True;TrustServerCertificate=True" \
   --to sql
```

Importer expects sheets named after rate tables (e.g., `BaseLossCost`, `AmountOfInsuranceFactor`, `LCM`, `PerilSubzoneFactor`, `ProtectionClassFactor`). You can map arbitrary sheet names via a `--map` JSON file. The tables correspond to the worksheet steps (S1, S2, S4, S6, S8, etc.).citeturn1search1

---

## SQL Server schema

See `sql/01_Create_RateTables.sql` for strongly-typed tables plus a generic fallback. Use **effective dating** and **jurisdiction** filters in queries.

---

## Testing

Run tests:

```bash
dotnet test
```

Tests include a **golden-master** that fixes the expected premium & trace for a sample risk and perils from the sample rate files.

---

## Next steps

- Wire your actual Excel exports to the importer maps
- Flesh out remaining worksheet steps (S9–S31, S36) by adding rate tables and step entries in the product JSON
- Swap `InMemoryRateLookup` with a cached SQL lookup once your tables are filled

If you want, I can extend the pipeline config to all remaining steps from your sheet and add more tests.
