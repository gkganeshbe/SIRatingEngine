using Dapper;
using Microsoft.Extensions.Caching.Memory;
using RatingEngine.Core;

namespace RatingEngine.Data;

/// <summary>
/// IRateLookup backed by SQL Server, scoped to one CoverageConfig row.
/// All rate table lookups are filtered to RateTable.CoverageConfigId = _coverageConfigId,
/// so table names only need to be unique within a coverage config.
/// Cache key: rt:{tenantId}:{configId}:{tableName}
/// </summary>
public sealed class DbRateLookup : IRateLookup
{
    private readonly DbConnectionFactory _db;
    private readonly IMemoryCache _cache;
    private readonly ITenantContext _tenant;
    private readonly int _coverageConfigId;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    public DbRateLookup(DbConnectionFactory db, IMemoryCache cache, ITenantContext tenant, int coverageConfigId)
    {
        _db = db;
        _cache = cache;
        _tenant = tenant;
        _coverageConfigId = coverageConfigId;
    }

    public decimal GetFactor(string rateTable, IReadOnlyDictionary<string, string> keys, DateOnly effDate)
    {
        var rows = LoadRows(rateTable);

        var match = rows
            .Where(r => r.IsEffective(effDate))
            .Where(r => r.Matches(keys))
            .OrderBy(r => r.WildcardCount(keys))
            .ThenByDescending(r => r.EffStart)
            .FirstOrDefault();

        if (match is null)
            throw new KeyNotFoundException(
                $"No rate row in {rateTable} for keys: {string.Join(",", keys.Select(k => $"{k.Key}={k.Value}"))}");

        return match.Factor ?? match.Additive ?? 0m;
    }

    public decimal GetRangeKeyFactor(string rateTable, IReadOnlyDictionary<string, string> keys, string rangeKey, decimal rangeValue, DateOnly effDate)
    {
        var rows = LoadRows(rateTable);

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

        return match.Factor ?? match.Additive ?? 0m;
    }

    public decimal GetInterpolatedFactor(string rateTable, IReadOnlyDictionary<string, string> keys, string interpolationKey, DateOnly effDate)
    {
        var rows = LoadRows(rateTable);

        var keyList = keys.ToList();
        var interpIdx = keyList.FindIndex(kv => string.Equals(kv.Key, interpolationKey, StringComparison.OrdinalIgnoreCase));
        if (interpIdx < 0)
            throw new InvalidOperationException($"Interpolation key '{interpolationKey}' not found in step keys");

        if (!decimal.TryParse(keyList[interpIdx].Value, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var interpValue))
            throw new InvalidOperationException(
                $"Interpolation key '{interpolationKey}' value '{keyList[interpIdx].Value}' is not numeric");

        var candidates = rows
            .Where(r => r.IsEffective(effDate))
            .Where(r => r.MatchesWithInterpolation(keys, interpIdx))
            .OrderBy(r => r.WildcardCountWithInterpolation(keys, interpIdx))
            .ThenByDescending(r => r.EffStart)
            .Select(r =>
            {
                var bpStr = r.GetKeyByIndex(interpIdx);
                var ok = decimal.TryParse(bpStr, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var bp);
                return (ok, bp, factor: r.Factor ?? r.Additive ?? 0m, r.AdditionalRate, r.AdditionalUnit);
            })
            .Where(x => x.ok)
            .GroupBy(x => x.bp)
            .Select(g => g.First())
            .OrderBy(x => x.bp)
            .ToList();

        if (candidates.Count == 0)
            throw new KeyNotFoundException(
                $"No rate rows in {rateTable} for keys: {string.Join(",", keys.Select(k => $"{k.Key}={k.Value}"))}");

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

    private List<RateRow> LoadRows(string tableName)
    {
        var cacheKey = $"rt:{_tenant.TenantId}:{_coverageConfigId}:{tableName}";
        return _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return FetchRows(tableName);
        })!;
    }

    private List<RateRow> FetchRows(string tableName)
    {
        const string sql = """
            SELECT r.Key1, r.Key2, r.Key3, r.Key4, r.Key5,
                   r.RangeFrom, r.RangeTo,
                   r.Factor, r.Additive, r.AdditionalUnit, r.AdditionalRate,
                   r.EffStart, r.ExpireAt AS EffEnd
            FROM RateTableRow r
            INNER JOIN RateTable t ON t.Id = r.RateTableId
            WHERE t.Name = @Name
              AND t.CoverageConfigId = @ConfigId
            """;

        using var conn = _db.Create();
        return conn.Query<RateRow>(sql, new { Name = tableName, ConfigId = _coverageConfigId }).AsList();
    }
}

/// <summary>
/// IRateLookupFactory backed by SQL Server.
/// Scopes each IRateLookup to the resolved CoverageConfig.DbId.
/// Must be registered as scoped (depends on scoped DbConnectionFactory and ITenantContext).
/// </summary>
public sealed class DbRateLookupFactory : IRateLookupFactory
{
    private readonly DbConnectionFactory _db;
    private readonly IMemoryCache _cache;
    private readonly ITenantContext _tenant;

    public DbRateLookupFactory(DbConnectionFactory db, IMemoryCache cache, ITenantContext tenant)
    {
        _db = db;
        _cache = cache;
        _tenant = tenant;
    }

    public IRateLookup CreateForCoverage(CoverageConfig coverage)
    {
        if (!coverage.DbId.HasValue)
            throw new InvalidOperationException(
                $"CoverageConfig '{coverage.CoverageCode}' has no DbId — cannot create a DB-scoped rate lookup.");
        return new DbRateLookup(_db, _cache, _tenant, coverage.DbId.Value);
    }
}
