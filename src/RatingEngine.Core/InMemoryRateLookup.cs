
using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;

namespace RatingEngine.Core;

public sealed class InMemoryRateLookup : IRateLookup
{
    private readonly ConcurrentDictionary<string, List<RateRow>> _tables = new();

    private InMemoryRateLookup() { }

    public static InMemoryRateLookup FromDirectory(string dir)
    {
        var inst = new InMemoryRateLookup();
        if (!Directory.Exists(dir)) return inst;
        foreach (var file in Directory.GetFiles(dir, "*.json"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var rows = JsonSerializer.Deserialize<List<RateRow>>(File.ReadAllText(file), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            inst._tables[name] = rows;
        }
        return inst;
    }

    public decimal GetFactor(string rateTable, IReadOnlyDictionary<string,string> keys, DateOnly effDate)
    {
        if (!_tables.TryGetValue(rateTable, out var rows))
            throw new KeyNotFoundException($"Rate table {rateTable} not loaded");

        // Prefer exact matches over wildcard rows; within equal specificity prefer latest effective date.
        var match = rows
            .Where(r => r.IsEffective(effDate))
            .Where(r => r.Matches(keys))
            .OrderBy(r => r.WildcardCount(keys))       // fewer wildcards = more specific = first
            .ThenByDescending(r => r.EffStart)
            .FirstOrDefault();

        if (match is null)
            throw new KeyNotFoundException($"No rate row in {rateTable} for keys: {string.Join(",", keys.Select(k=>$"{k.Key}={k.Value}"))}");

        return match.Factor;
    }

    public decimal GetRangeKeyFactor(string rateTable, IReadOnlyDictionary<string,string> keys, string rangeKey, decimal rangeValue, DateOnly effDate)
    {
        if (!_tables.TryGetValue(rateTable, out var rows))
            throw new KeyNotFoundException($"Rate table {rateTable} not loaded");

        var match = rows
            .Where(r => r.IsEffective(effDate))
            .Where(r => r.Matches(keys))
            .Where(r => r.RangeFrom.HasValue && r.RangeTo.HasValue
                        && rangeValue >= r.RangeFrom.Value
                        && rangeValue <= r.RangeTo.Value)
            .OrderBy(r => r.WildcardCount(keys))
            .ThenByDescending(r => r.EffStart)
            .FirstOrDefault();

        if (match is null)
            throw new KeyNotFoundException(
                $"No rate row in {rateTable} for keys: {string.Join(",", keys.Select(k => $"{k.Key}={k.Value}"))} rangeKey={rangeKey}={rangeValue}");

        return match.Factor;
    }

    public decimal GetInterpolatedFactor(string rateTable, IReadOnlyDictionary<string,string> keys, string interpolationKey, DateOnly effDate)
    {
        if (!_tables.TryGetValue(rateTable, out var rows))
            throw new KeyNotFoundException($"Rate table {rateTable} not loaded");

        var keyList = keys.ToList();
        var interpIdx = keyList.FindIndex(kv => string.Equals(kv.Key, interpolationKey, StringComparison.OrdinalIgnoreCase));
        if (interpIdx < 0)
            throw new InvalidOperationException($"Interpolation key '{interpolationKey}' not found in step keys");

        if (!decimal.TryParse(keyList[interpIdx].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var interpValue))
            throw new InvalidOperationException($"Interpolation key '{interpolationKey}' value '{keyList[interpIdx].Value}' is not numeric");

        var candidates = rows
            .Where(r => r.IsEffective(effDate))
            .Where(r => r.MatchesWithInterpolation(keys, interpIdx))
            .OrderBy(r => r.WildcardCountWithInterpolation(keys, interpIdx)) // specificity
            .ThenByDescending(r => r.EffStart)
            .Select(r =>
            {
                var bpStr = r.GetKeyByIndex(interpIdx);
                var ok = decimal.TryParse(bpStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var bp);
                return (ok, bp, factor: r.Factor, r.AdditionalRate, r.AdditionalUnit);
            })
            .Where(x => x.ok)
            .GroupBy(x => x.bp)
            .Select(g => g.First())
            .OrderBy(x => x.bp)
            .ToList();

        if (candidates.Count == 0)
            throw new KeyNotFoundException($"No rate rows in {rateTable} for keys: {string.Join(",", keys.Select(k=>$"{k.Key}={k.Value}"))}");

        if (interpValue <= candidates[0].bp) return candidates[0].factor;

        if (interpValue >= candidates[^1].bp)
        {
            var top = candidates[^1];
            if (top.AdditionalRate.HasValue && top.AdditionalUnit is > 0)
            {
                var excess = interpValue - top.bp;
                return top.factor + (excess / top.AdditionalUnit.Value) * top.AdditionalRate.Value;
            }
            return top.factor;
        }

        var lo = candidates.Last(x => x.bp <= interpValue);
        var hi = candidates.First(x => x.bp > interpValue);
        var t = (interpValue - lo.bp) / (hi.bp - lo.bp);
        return lo.factor + t * (hi.factor - lo.factor);
    }
}

public sealed record RateRow
{
    public string? Key1 { get; init; }
    public string? Key2 { get; init; }
    public string? Key3 { get; init; }
    public string? Key4 { get; init; }
    public string? Key5 { get; init; }
    public decimal Factor { get; init; }
    public decimal? AdditionalRate { get; init; }
    public decimal? AdditionalUnit { get; init; }
    public DateOnly EffStart { get; init; }
    public DateOnly? EffEnd { get; init; }
    /// <summary>Lower bound (inclusive) for range-based lookups.</summary>
    public decimal? RangeFrom { get; init; }
    /// <summary>Upper bound (inclusive) for range-based lookups.</summary>
    public decimal? RangeTo { get; init; }
}

internal static class RateRowExtensions
{
    // A row key of "*" matches any request value (wildcard fallback).
    private static bool KeyMatches(string? rowKey, string requestValue) =>
        rowKey == "*" || string.Equals(rowKey, requestValue, StringComparison.OrdinalIgnoreCase);

    public static bool Matches(this RateRow row, IReadOnlyDictionary<string,string> keys)
    {
        // var cols = new[] { row.Key1, row.Key2, row.Key3, row.Key4, row.Key5 };
        // int i = 0;
        // foreach (var kv in keys)
        // {
        //     if (!KeyMatches(cols[i++], kv.Value)) return false;
        // }
        // Explicit lookup fixes the dictionary iteration order vulnerability
        if (keys.TryGetValue("Key1", out var k1) && !KeyMatches(row.Key1, k1)) return false;
        if (keys.TryGetValue("Key2", out var k2) && !KeyMatches(row.Key2, k2)) return false;
        if (keys.TryGetValue("Key3", out var k3) && !KeyMatches(row.Key3, k3)) return false;
        if (keys.TryGetValue("Key4", out var k4) && !KeyMatches(row.Key4, k4)) return false;
        if (keys.TryGetValue("Key5", out var k5) && !KeyMatches(row.Key5, k5)) return false;
        return true;
    }

    public static bool MatchesWithInterpolation(this RateRow row, IReadOnlyDictionary<string,string> keys, int interpIdx)
    {
        var cols = new[] { row.Key1, row.Key2, row.Key3, row.Key4, row.Key5 };
        int i = 0;
        foreach (var kv in keys)
        {
            if (i == interpIdx) { i++; continue; }
            if (!KeyMatches(cols[i], kv.Value)) return false;
            i++;
        }
        return true;
    }

    /// <summary>Returns the number of wildcard columns in this row for the given key set.
    /// Used to rank rows so exact matches take precedence over wildcard fallbacks.</summary>
    public static int WildcardCount(this RateRow row, IReadOnlyDictionary<string,string> keys)
    {
        var cols = new[] { row.Key1, row.Key2, row.Key3, row.Key4, row.Key5 };
        int count = 0, i = 0;
        foreach (var _ in keys) { if (cols[i++] == "*") count++; }
        return count;
    }

    public static int WildcardCountWithInterpolation(this RateRow row, IReadOnlyDictionary<string,string> keys, int interpIdx)
    {
        var cols = new[] { row.Key1, row.Key2, row.Key3, row.Key4, row.Key5 };
        int count = 0, i = 0;
        foreach (var _ in keys) { if (i != interpIdx && cols[i] == "*") count++; i++; }
        return count;
    }

    public static string? GetKeyByIndex(this RateRow row, int index) => index switch
    {
        0 => row.Key1,
        1 => row.Key2,
        2 => row.Key3,
        3 => row.Key4,
        4 => row.Key5,
        _ => null
    };

    public static bool IsEffective(this RateRow row, DateOnly eff)
    {
        var end = row.EffEnd ?? new DateOnly(9999,12,31);
        return row.EffStart <= eff && eff <= end;
    }
}
