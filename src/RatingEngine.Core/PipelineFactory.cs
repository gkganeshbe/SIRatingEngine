
using System.Globalization;
using System.Text.Json;

namespace RatingEngine.Core;

public sealed class JsonPipelineFactory : IPipelineFactory
{
    public IReadOnlyList<IRatingStep> Build(IReadOnlyList<StepConfig> steps)
    {
        var list = new List<IRatingStep>();
        foreach (var sc in steps)
        {
            var when = BuildPredicate(sc.When);
            switch (sc.Operation.ToLowerInvariant())
            {
                case "lookup":
                    {
                        var rt = sc.RateTable ?? throw new InvalidOperationException($"Step {sc.Id} missing rateTable");
                        var keys = BuildKeySelector(sc.Keys ?? new());
                        var mathType = sc.Math?.Type?.ToLowerInvariant() ?? "mul";
                        var interpKey = sc.Interpolate?.Key;
                        (string Key, Func<RateContext, decimal> Resolver)? rangeKey = null;
                        if (sc.RangeKey is not null)
                        {
                            var rkName = sc.RangeKey.Key;
                            rangeKey = (rkName, ctx =>
                            {
                                var raw = ResolvePath(ctx, $"$risk.{rkName}");
                                return decimal.TryParse(raw, System.Globalization.NumberStyles.Any,
                                    System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0m;
                            });
                        }
                        IRatingStep step = mathType switch
                        {
                            "mul" => new LookupMultiplyStep  { Id = sc.Id, Name = sc.Name, RateTable = rt, KeySelector = keys, Predicate = when, InterpolateKey = interpKey, RangeKey = rangeKey },
                            "add" => new LookupAddStep       { Id = sc.Id, Name = sc.Name, RateTable = rt, KeySelector = keys, Predicate = when, InterpolateKey = interpKey, RangeKey = rangeKey },
                            "sub" => new LookupSubtractStep  { Id = sc.Id, Name = sc.Name, RateTable = rt, KeySelector = keys, Predicate = when, InterpolateKey = interpKey, RangeKey = rangeKey },
                            "set" => new SetFromLookupStep   { Id = sc.Id, Name = sc.Name, RateTable = rt, KeySelector = keys, Predicate = when, InterpolateKey = interpKey, RangeKey = rangeKey },
                            _ => throw new NotSupportedException($"Unsupported math type {sc.Math?.Type}")
                        };
                        list.Add(step);
                        break;
                    }
                case "compute":
                    {
                        var cfg = sc.Compute ?? throw new InvalidOperationException($"Step {sc.Id} with operation=compute is missing a 'compute' config block");
                        list.Add(new ComputeStep { Id = sc.Id, Name = sc.Name, Expr = cfg.Expr, StoreAs = cfg.StoreAs, ApplyToPremium = cfg.ApplyToPremium, Predicate = when });
                        break;
                    }
                case "round":
                    list.Add(new RoundStep { Id = sc.Id, Name = sc.Name, Precision = sc.Round?.Precision ?? 2, Mode = ParseMode(sc.Round?.Mode) });
                    break;
                default:
                    throw new NotSupportedException($"Unsupported operation {sc.Operation}");
            }
        }
        return list;
    }

    private static Func<RateContext, Dictionary<string,string>> BuildKeySelector(Dictionary<string,string> map)
    {
        return ctx => map.ToDictionary(kv => kv.Key, kv => ResolvePath(ctx, kv.Value));
    }

    private static Func<RateContext, bool> BuildPredicate(WhenConfig? when)
    {
        if (when is null || string.IsNullOrWhiteSpace(when.Path)) return _ => true;

        return ctx =>
        {
            var raw = ResolvePath(ctx, when.Path);

            // ── equality / boolean ────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(when.EqualsTo))
                return string.Equals(raw, when.EqualsTo, StringComparison.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(when.NotEquals))
                return !string.Equals(raw, when.NotEquals, StringComparison.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(when.IsTrue))
                return bool.TryParse(raw, out var b) && b;

            // ── numeric comparisons ───────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(when.GreaterThan))
                return TryCompare(raw, when.GreaterThan, out var c) && c > 0;

            if (!string.IsNullOrWhiteSpace(when.LessThan))
                return TryCompare(raw, when.LessThan, out var c) && c < 0;

            if (!string.IsNullOrWhiteSpace(when.GreaterThanOrEqual))
                return TryCompare(raw, when.GreaterThanOrEqual, out var c) && c >= 0;

            if (!string.IsNullOrWhiteSpace(when.LessThanOrEqual))
                return TryCompare(raw, when.LessThanOrEqual, out var c) && c <= 0;

            // ── set membership ────────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(when.In))
            {
                var set = ParseSet(when.In);
                return set.Contains(raw);
            }

            if (!string.IsNullOrWhiteSpace(when.NotIn))
            {
                var set = ParseSet(when.NotIn);
                return !set.Contains(raw);
            }

            return true;
        };
    }

    // ── path resolution ───────────────────────────────────────────────────────
    // Supports: $peril  |  $risk.<Key>  |  literal (returned as-is)
    internal static string ResolvePath(RateContext ctx, string path)
    {
        if (path == "$peril") return ctx.Peril;

        if (path.StartsWith("$risk.", StringComparison.OrdinalIgnoreCase))
        {
            var key = path[6..];
            return ctx.Risk.TryGetValue(key, out var v) ? v : string.Empty;
        }

        if (path.StartsWith("$coverage.", StringComparison.OrdinalIgnoreCase))
        {
            var key = path[10..];
            return ctx.Risk.TryGetValue(key, out var v) ? v : string.Empty;
        }

        // Literal value — strip leading $ if present (e.g. "$FIRE" → "FIRE")
        return path.TrimStart('$');
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static bool TryCompare(string rawValue, string threshold, out int comparison)
    {
        comparison = 0;
        if (!decimal.TryParse(rawValue,   NumberStyles.Any, CultureInfo.InvariantCulture, out var lhs)) return false;
        if (!decimal.TryParse(threshold,  NumberStyles.Any, CultureInfo.InvariantCulture, out var rhs)) return false;
        comparison = lhs.CompareTo(rhs);
        return true;
    }

    private static HashSet<string> ParseSet(string csv) =>
        new(csv.Split(',').Select(s => s.Trim()), StringComparer.OrdinalIgnoreCase);

    private static MidpointRounding ParseMode(string? mode) =>
        string.Equals(mode, "ToEven", StringComparison.OrdinalIgnoreCase)
            ? MidpointRounding.ToEven
            : MidpointRounding.AwayFromZero;
}

public sealed class FileProductManifestRepository : IProductManifestRepository
{
    private readonly string _configDir;
    public FileProductManifestRepository(string configDir) => _configDir = configDir;

    public Task<ProductManifest?> GetAsync(string productCode, string version)
    {
        var productsDir = Path.Combine(_configDir, "products");
        var file = Directory.GetFiles(productsDir, $"{productCode}.{version}.json", SearchOption.AllDirectories).FirstOrDefault();
        if (file == null) return Task.FromResult<ProductManifest?>(null);
        var cfg = JsonSerializer.Deserialize<ProductManifest>(File.ReadAllText(file), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return Task.FromResult(cfg);
    }
}

public sealed class FileCoverageConfigRepository : ICoverageConfigRepository
{
    private readonly string _configDir;
    public FileCoverageConfigRepository(string configDir) => _configDir = configDir;

    public Task<CoverageConfig?> GetAsync(string productCode, string coverageCode, string version)
    {
        var coveragesDir = Path.Combine(_configDir, "coverages");
        var file = Directory.GetFiles(coveragesDir, $"{productCode}.{coverageCode}.{version}.json", SearchOption.AllDirectories).FirstOrDefault();
        if (file == null) return Task.FromResult<CoverageConfig?>(null);
        var cfg = JsonSerializer.Deserialize<CoverageConfig>(File.ReadAllText(file), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return Task.FromResult(cfg);
    }
}
