
using RatingEngine.Core;
using Xunit;

namespace RatingEngine.Tests;

public class PipelineTests
{
    // ── AOI interpolation ────────────────────────────────────────────────────

    [Theory]
    [InlineData(100000, 15.0)]   // at lower breakpoint — exact
    [InlineData(200000, 22.5)]   // at middle breakpoint — exact
    [InlineData(300000, 30.0)]   // at upper breakpoint — exact (no excess)
    [InlineData(150000, 18.75)]  // midpoint 100k–200k: 15 + 0.5*(22.5-15) = 18.75
    [InlineData(250000, 26.25)]  // midpoint 200k–300k: 22.5 + 0.5*(30-22.5) = 26.25
    [InlineData(50000,  15.0)]   // below range — clamped to lowest
    [InlineData(400000, 45.0)]   // above range — extrapolated: 30 + (100k/10k)*1.5 = 30+15 = 45
    [InlineData(500000, 60.0)]   // above range — extrapolated: 30 + (200k/10k)*1.5 = 30+30 = 60
    public void AOI_Interpolation_Returns_Correct_Factor(decimal coverageA, decimal expectedFactor)
    {
        var lookup = InMemoryRateLookup.FromDirectory(
            Path.Combine(FindRepoRoot(), "data", "rates", "HO-PRIMARY.NJ.PRIMARY.2026.02"));

        var keys = new Dictionary<string, string>
        {
            ["Peril"]    = "FIRE",
            ["CoverageA"] = coverageA.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        var factor = lookup.GetInterpolatedFactor("AmountOfInsuranceFactor", keys, "CoverageA", new DateOnly(2026, 2, 15));

        Assert.Equal(expectedFactor, factor);
    }

    // ── Premium regression ───────────────────────────────────────────────────

    [Fact]
    public void Computes_Premium_With_Rounding_Checkpoints()
    {
        var coveragePath = Path.Combine(FindRepoRoot(), "src", "RatingEngine.Config", "coverages", "HO-PRIMARY.NJ.PRIMARY.2026.02.json");
        var coverage = System.Text.Json.JsonSerializer.Deserialize<CoverageConfig>(File.ReadAllText(coveragePath)!, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var factory = new JsonPipelineFactory();
        var steps = factory.Build(coverage!.Pipeline);
        var lookup = InMemoryRateLookup.FromDirectory(Path.Combine(FindRepoRoot(), "data", "rates", "HO-PRIMARY.NJ.PRIMARY.2026.02"));

        var risk = BuildBaseRisk();
        var ctx = new RateContext("HO-PRIMARY", "2026.02", new DateOnly(2026,2,15), "NJ", risk, "FIRE", 0m);
        var (premium, trace) = new PipelineRunner(steps, lookup).Run(ctx);

        Assert.Equal(187.03m, premium);
        Assert.True(trace.Count > 0);
    }

    // ── Segment pro-ration ───────────────────────────────────────────────────

    [Fact]
    public void Segment_SingleFullYear_EqualsAnnualPremium()
    {
        var (coverage, lookup) = LoadPrimary();

        var policyFrom = new DateOnly(2026, 1, 1);
        var policyTo   = new DateOnly(2027, 1, 1);
        var totalDays  = policyTo.DayNumber - policyFrom.DayNumber;

        decimal annualTotal = 0m;
        foreach (var peril in coverage.Perils)
        {
            var ctx = new RateContext("HO-PRIMARY", "2026.02", new DateOnly(2026, 2, 15), "NJ",
                BuildBaseRisk(), peril, 0m);
            var (premium, _) = new PipelineRunner(new JsonPipelineFactory().Build(coverage.Pipeline), lookup).Run(ctx);
            annualTotal += premium;
        }

        var prorationFactor = (decimal)(policyTo.DayNumber - policyFrom.DayNumber) / totalDays;
        Assert.Equal(1.0m, prorationFactor);

        decimal segmentTotal = 0m;
        foreach (var peril in coverage.Perils)
        {
            var ctx = new RateContext("HO-PRIMARY", "2026.02", new DateOnly(2026, 2, 15), "NJ",
                BuildBaseRisk(), peril, 0m);
            var (premium, _) = new PipelineRunner(new JsonPipelineFactory().Build(coverage.Pipeline), lookup).Run(ctx);
            segmentTotal += Math.Round(premium * prorationFactor, 2, MidpointRounding.AwayFromZero);
        }

        Assert.Equal(annualTotal, segmentTotal);
    }

    [Fact]
    public void Segment_TwoHalves_SumWithinPennyOfAnnual()
    {
        var (coverage, lookup) = LoadPrimary();

        var policyFrom = new DateOnly(2026, 1, 1);
        var midPoint   = new DateOnly(2026, 7, 1);
        var policyTo   = new DateOnly(2027, 1, 1);
        var totalDays  = policyTo.DayNumber - policyFrom.DayNumber;

        decimal annualTotal = 0m;
        foreach (var peril in coverage.Perils)
        {
            var ctx = new RateContext("HO-PRIMARY", "2026.02", new DateOnly(2026, 2, 15), "NJ",
                BuildBaseRisk(), peril, 0m);
            var (premium, _) = new PipelineRunner(new JsonPipelineFactory().Build(coverage.Pipeline), lookup).Run(ctx);
            annualTotal += premium;
        }

        var segments = new[] { (From: policyFrom, To: midPoint), (From: midPoint, To: policyTo) };
        decimal segmentsTotal = 0m;
        foreach (var seg in segments)
        {
            var factor = (decimal)(seg.To.DayNumber - seg.From.DayNumber) / totalDays;
            foreach (var peril in coverage.Perils)
            {
                var ctx = new RateContext("HO-PRIMARY", "2026.02", new DateOnly(2026, 2, 15), "NJ",
                    BuildBaseRisk(), peril, 0m);
                var (premium, _) = new PipelineRunner(new JsonPipelineFactory().Build(coverage.Pipeline), lookup).Run(ctx);
                segmentsTotal += Math.Round(premium * factor, 2, MidpointRounding.AwayFromZero);
            }
        }

        Assert.InRange(segmentsTotal, annualTotal - 0.01m * coverage.Perils.Count, annualTotal + 0.01m * coverage.Perils.Count);
    }

    // ── RiskBag.Merge ────────────────────────────────────────────────────────

    [Fact]
    public void RiskBag_Merge_CombinesPropertyAndCoverageParams()
    {
        var property = new Dictionary<string, string>
        {
            ["State"]        = "NJ",
            ["Zone"]         = "Z1",
            ["Construction"] = "FRM",
            ["HasPool"]      = "True",
            ["PoolCount"]    = "1",
        };
        var coverageParams = new Dictionary<string, string>
        {
            ["CoverageA"]      = "400000",
            ["AmountBand"]     = ">250k",
            ["DeductiblePct"]  = "0.02",
            ["DeductibleFlat"] = "2500",
        };

        var bag = RiskBag.Merge(property, coverageParams);

        Assert.Equal("NJ",      bag["State"]);
        Assert.Equal("Z1",      bag["Zone"]);
        Assert.Equal("FRM",     bag["Construction"]);
        Assert.Equal("True",    bag["HasPool"]);
        Assert.Equal("400000",  bag["CoverageA"]);
        Assert.Equal(">250k",   bag["AmountBand"]);
        Assert.Equal("0.02",    bag["DeductiblePct"]);
        Assert.Equal("2500",    bag["DeductibleFlat"]);
    }

    [Fact]
    public void RiskBag_Merge_CoverageParams_Override_Property()
    {
        var property       = new Dictionary<string, string> { ["CoverageA"] = "100000" };
        var coverageParams = new Dictionary<string, string> { ["CoverageA"] = "999999" };

        var bag = RiskBag.Merge(property, coverageParams);
        Assert.Equal("999999", bag["CoverageA"]);
    }

    // ── WhenConfig – numeric comparisons ────────────────────────────────────

    [Theory]
    [InlineData("5", "3", true)]
    [InlineData("3", "5", false)]
    [InlineData("3", "3", false)]
    public void When_GreaterThan_Gates_Step_Correctly(string riskValue, string threshold, bool shouldExecute)
    {
        var ctx = MakeCtx("ClaimFreeYears", riskValue);
        var when = new WhenConfig { Path = "$risk.ClaimFreeYears", GreaterThan = threshold };
        Assert.Equal(shouldExecute, EvalWhen(when, ctx));
    }

    [Theory]
    [InlineData("2", "5", true)]
    [InlineData("5", "2", false)]
    [InlineData("5", "5", false)]
    public void When_LessThan_Gates_Step_Correctly(string riskValue, string threshold, bool shouldExecute)
    {
        var ctx = MakeCtx("ClaimFreeYears", riskValue);
        var when = new WhenConfig { Path = "$risk.ClaimFreeYears", LessThan = threshold };
        Assert.Equal(shouldExecute, EvalWhen(when, ctx));
    }

    [Theory]
    [InlineData("5", "5", true)]
    [InlineData("6", "5", true)]
    [InlineData("4", "5", false)]
    public void When_GreaterThanOrEqual_Gates_Step_Correctly(string riskValue, string threshold, bool shouldExecute)
    {
        var ctx = MakeCtx("ClaimFreeYears", riskValue);
        var when = new WhenConfig { Path = "$risk.ClaimFreeYears", GreaterThanOrEqual = threshold };
        Assert.Equal(shouldExecute, EvalWhen(when, ctx));
    }

    // ── WhenConfig – In / NotIn ──────────────────────────────────────────────

    [Fact]
    public void When_In_Executes_When_Value_Is_In_List()
    {
        var ctx = MakeCtx("State", "NJ");
        Assert.True(EvalWhen(new WhenConfig { Path = "$risk.State", In = "NJ, NY, CT" }, ctx));
    }

    [Fact]
    public void When_In_Skips_When_Value_Not_In_List()
    {
        var ctx = MakeCtx("State", "FL");
        Assert.False(EvalWhen(new WhenConfig { Path = "$risk.State", In = "NJ, NY, CT" }, ctx));
    }

    [Fact]
    public void When_NotIn_Skips_When_Value_Is_In_List()
    {
        var ctx = MakeCtx("State", "NJ");
        Assert.False(EvalWhen(new WhenConfig { Path = "$risk.State", NotIn = "NJ, NY, CT" }, ctx));
    }

    // ── ComputeStep ──────────────────────────────────────────────────────────

    [Fact]
    public void ComputeStep_Stores_Derived_Value_In_Risk_Bag()
    {
        var risk = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["CoverageA"] = "400000" };
        var ctx  = new RateContext("P", "1", new DateOnly(2026, 1, 1), "NJ", risk, "FIRE", 0m);
        var step = new ComputeStep { Id = "c1", Name = "Derive CovB", Expr = "$risk.CoverageA * 0.1", StoreAs = "CoverageB" };

        step.Execute(ctx, null!);

        Assert.True(ctx.Risk.TryGetValue("CoverageB", out var val));
        Assert.Equal(40000m, decimal.Parse(val!, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ComputeStep_Multi_Operator_Left_To_Right()
    {
        // ($risk.CoverageA * 0.2) + 500 = 80000 + 500 = 80500
        var risk = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["CoverageA"] = "400000" };
        var ctx  = new RateContext("P", "1", new DateOnly(2026, 1, 1), "NJ", risk, "FIRE", 0m);
        var step = new ComputeStep { Id = "c2", Name = "Derive CovD", Expr = "$risk.CoverageA * 0.2 + 500", StoreAs = "CoverageD" };

        step.Execute(ctx, null!);

        Assert.Equal(80500m, decimal.Parse(ctx.Risk["CoverageD"], System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ComputeStep_Does_Not_Change_Premium()
    {
        var risk = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["CoverageA"] = "300000" };
        var ctx  = new RateContext("P", "1", new DateOnly(2026, 1, 1), "NJ", risk, "FIRE", 250m);
        var step = new ComputeStep { Id = "c3", Name = "noop compute", Expr = "$risk.CoverageA * 0.1", StoreAs = "CoverageB" };

        var result = step.Execute(ctx, null!);
        Assert.Equal(250m, result.NewPremium);
    }

    // ── Wildcard rate table lookup ───────────────────────────────────────────
    // Verified through GetFactor: a table with a wildcard row and an exact row
    // must return the exact row's factor when the key matches exactly, and the
    // wildcard factor when no exact row exists.

    [Fact]
    public void GetFactor_Returns_Exact_Match_Over_Wildcard()
    {
        // Build a lookup with two rows in a synthetic table:
        //   State=NJ  → Factor 2.0   (exact)
        //   State=*   → Factor 1.5   (wildcard fallback)
        var lookup = BuildLookupWithWildcard();

        var njFactor = lookup.GetFactor("WildcardTest",
            new Dictionary<string, string> { ["State"] = "NJ" }, new DateOnly(2026, 1, 1));
        Assert.Equal(2.0m, njFactor);
    }

    [Fact]
    public void GetFactor_Falls_Back_To_Wildcard_When_No_Exact_Match()
    {
        var lookup = BuildLookupWithWildcard();

        var caFactor = lookup.GetFactor("WildcardTest",
            new Dictionary<string, string> { ["State"] = "CA" }, new DateOnly(2026, 1, 1));
        Assert.Equal(1.5m, caFactor);
    }

    // Builds an InMemoryRateLookup from a temp JSON file so we can inject custom rows.
    private static InMemoryRateLookup BuildLookupWithWildcard()
    {
        var json = """
            [
              { "Key1": "NJ", "Factor": 2.0, "EffStart": "2020-01-01" },
              { "Key1": "*",  "Factor": 1.5, "EffStart": "2020-01-01" }
            ]
            """;
        var dir = Path.Combine(Path.GetTempPath(), $"rate-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "WildcardTest.json");
        File.WriteAllText(file, json);
        var lookup = InMemoryRateLookup.FromDirectory(dir);
        Directory.Delete(dir, recursive: true);
        return lookup;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static (CoverageConfig coverage, InMemoryRateLookup lookup) LoadPrimary()
    {
        var coveragePath = Path.Combine(FindRepoRoot(), "src", "RatingEngine.Config", "coverages", "HO-PRIMARY.NJ.PRIMARY.2026.02.json");
        var coverage = System.Text.Json.JsonSerializer.Deserialize<CoverageConfig>(
            File.ReadAllText(coveragePath)!,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var lookup = InMemoryRateLookup.FromDirectory(Path.Combine(FindRepoRoot(), "data", "rates", "HO-PRIMARY.NJ.PRIMARY.2026.02"));
        return (coverage, lookup);
    }

    private static Dictionary<string, string> BuildBaseRisk() =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Occupancy"]        = "OWNER",
            ["Zone"]             = "Z1",
            ["SubZone"]          = "SZ1",
            ["PolicyForm"]       = "HO3",
            ["State"]            = "NJ",
            ["Zip"]              = "08525",
            ["ProtectionClass"]  = "3",
            ["Construction"]     = "FRM",
            ["ExternalCladding"] = "STD",
            ["Roof"]             = "SHINGLE",
            ["CoverageA"]        = "300000",
            ["AmountBand"]       = ">250k",
            ["HasPool"]          = "False",
            ["PoolCount"]        = "0",
            ["HasTrampoline"]    = "False",
            ["TrampolineCount"]  = "0",
            ["HasCanines"]       = "False",
            ["CanineCount"]      = "0",
            ["Seasonal"]         = "False",
            ["SeasonalMonths"]   = "0",
            ["ClaimFreeYears"]   = "5",
            ["NoOfFamilies"]     = "1",
            ["StructureType"]    = "SFD",
            ["DeductiblePct"]    = "1.0",
            ["DeductibleFlat"]   = "1000",
        };

    private static RateContext MakeCtx(string key, string value)
    {
        var risk = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [key] = value };
        return new RateContext("P", "1", new DateOnly(2026, 1, 1), "NJ", risk, "FIRE", 0m);
    }

    // Build a trivial compute step with the given WhenConfig and call ShouldExecute.
    private static bool EvalWhen(WhenConfig when, RateContext ctx)
    {
        var stepCfg = new StepConfig
        {
            Id        = "test",
            Operation = "compute",
            When      = when,
            Compute   = new ComputeConfig { Expr = "0", StoreAs = "_test" }
        };
        var steps = new JsonPipelineFactory().Build(new[] { stepCfg });
        return steps[0].ShouldExecute(ctx);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "src")))
            dir = dir.Parent;
        if (dir == null) throw new Exception("Repository root (containing 'src') not found");
        return dir.FullName;
    }
}
