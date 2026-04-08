
using System.Globalization;

namespace RatingEngine.Core;

public abstract class LookupStepBase : IRatingStep
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Category { get; init; }
    public required string RateTable { get; init; }
    public required Func<RateContext, Dictionary<string,string>> KeySelector { get; init; }
    public Func<RateContext, bool> Predicate { get; init; } = _ => true;
    /// <summary>When set, this key is the numeric interpolation dimension rather than an exact match key.</summary>
    public string? InterpolateKey { get; init; }
    /// <summary>When set, the value at this risk key is matched against each row's RangeFrom/RangeTo.</summary>
    public (string Key, Func<RateContext, decimal> Resolver)? RangeKey { get; init; }

    protected (decimal before, decimal after, decimal factor, Dictionary<string,string> keys) Apply(
        RateContext ctx, IRateLookup lookup, Func<decimal, decimal, decimal> op)
    {
        var before = ctx.Premium;
        var keys = KeySelector(ctx);
        decimal factor;
        if (RangeKey.HasValue)
        {
            var rangeValue = RangeKey.Value.Resolver(ctx);
            factor = lookup.GetRangeKeyFactor(RateTable, keys, RangeKey.Value.Key, rangeValue, ctx.EffectiveDate);
        }
        else if (InterpolateKey is not null)
        {
            factor = lookup.GetInterpolatedFactor(RateTable, keys, InterpolateKey, ctx.EffectiveDate);
        }
        else
        {
            factor = lookup.GetFactor(RateTable, keys, ctx.EffectiveDate);
        }
        var after = op(before, factor);
        return (before, after, factor, keys);
    }

    public abstract RateResult Execute(RateContext ctx, IRateLookup lookup);
    public bool ShouldExecute(RateContext ctx) => Predicate(ctx);
}

public sealed class LookupMultiplyStep : LookupStepBase
{
    public override RateResult Execute(RateContext ctx, IRateLookup lookup)
    {
        var (before, after, factor, keys) = Apply(ctx, lookup, (b, f) => b * f);
        return new RateResult(after, new RatingTrace(Id, Name, RateTable, keys, factor, before, after, null, Category)
        {
            Formula = $"{before} * {factor}"
        });
    }
}

public sealed class LookupAddStep : LookupStepBase
{
    public override RateResult Execute(RateContext ctx, IRateLookup lookup)
    {
        var (before, after, factor, keys) = Apply(ctx, lookup, (b, f) => b + f);
        return new RateResult(after, new RatingTrace(Id, Name, RateTable, keys, factor, before, after, null, Category)
        {
            Formula = $"{before} + {factor}"
        });
    }
}

public sealed class LookupSubtractStep : LookupStepBase
{
    public override RateResult Execute(RateContext ctx, IRateLookup lookup)
    {
        var (before, after, factor, keys) = Apply(ctx, lookup, (b, f) => b - f);
        return new RateResult(after, new RatingTrace(Id, Name, RateTable, keys, factor, before, after, null, Category)
        {
            Formula = $"{before} - {factor}"
        });
    }
}

public sealed class SetFromLookupStep : LookupStepBase
{
    public override RateResult Execute(RateContext ctx, IRateLookup lookup)
    {
        var (before, after, factor, keys) = Apply(ctx, lookup, (_, f) => f);
        return new RateResult(after, new RatingTrace(Id, Name, RateTable, keys, factor, before, after, "set", Category)
        {
            Formula = $"{factor}"
        });
    }
}

/// <summary>
/// Evaluates a simple arithmetic expression against the risk bag and stores
/// the result back into the bag under StoreAs so later steps can read it
/// via $risk.&lt;StoreAs&gt;.
///
/// Expression syntax: operands separated by a single operator with spaces.
///   Operand: $risk.&lt;Key&gt;  |  decimal literal
///   Operator: +  -  *  /   (evaluated left-to-right; space-delimited tokens)
///
/// Examples:
///   "$risk.CoverageA * 0.1"           → 10 % of CoverageA
///   "$risk.CoverageA + $risk.CoverageB"
///   "$risk.DwellingLimit * 0.2 + 500"
/// </summary>
public sealed class ComputeStep : IRatingStep
{
    public required string Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Category { get; init; }
    public required string Expr { get; init; }
    public required string StoreAs { get; init; }
    public bool ApplyToPremium { get; init; } = false;
    public Func<RateContext, bool> Predicate { get; init; } = _ => true;

    public bool ShouldExecute(RateContext ctx) => Predicate(ctx);

    public RateResult Execute(RateContext ctx, IRateLookup _)
    {
        var before = ctx.Premium;
        var metadata = new Dictionary<string, string>();
        var value = Evaluate(ctx, Expr, metadata);
        ctx.Risk[StoreAs] = value.ToString(CultureInfo.InvariantCulture);
        var after = ApplyToPremium ? value : ctx.Premium;
        return new RateResult(
            after,
            new RatingTrace(Id, Name, null, null, ApplyToPremium ? value : null, before, after, $"compute:{StoreAs}={value}", Category)
            {
                Metadata = metadata,
                Formula = Expr
            });
    }

    // ── expression evaluator ─────────────────────────────────────────────────
    // Tokenises by whitespace and evaluates left-to-right so the expression
    // format is: operand [op operand]*
    // e.g. "$risk.CoverageA * 0.1 + 500"  →  (CoverageA × 0.1) + 500
    private static decimal Evaluate(RateContext ctx, string expr, Dictionary<string, string> metadata)
    {
        var tokens = expr.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0) return 0m;

        var result = ResolveOperand(ctx, tokens[0], metadata);
        var i = 1;
        while (i + 1 < tokens.Length)
        {
            var op    = tokens[i];
            var right = ResolveOperand(ctx, tokens[i + 1], metadata);
            result = op switch
            {
                "+" => result + right,
                "-" => result - right,
                "*" => result * right,
                "/" => right == 0m ? 0m : result / right,
                _   => result
            };
            i += 2;
        }
        return result;
    }

    private static decimal ResolveOperand(RateContext ctx, string operand, Dictionary<string, string> metadata)
    {
        if (string.Equals(operand, "$premium", StringComparison.OrdinalIgnoreCase))
        {
            metadata["$premium"] = ctx.Premium.ToString(CultureInfo.InvariantCulture);
            return ctx.Premium;
        }

        if (operand.StartsWith("$risk.", StringComparison.OrdinalIgnoreCase))
        {
            var key = operand[6..];
            if (ctx.Risk.TryGetValue(key, out var v) &&
                decimal.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
            {
                metadata[operand] = v;
                return d;
            }
            metadata[operand] = "0";
            return 0m;
        }

        if (operand.StartsWith("$coverage.", StringComparison.OrdinalIgnoreCase))
        {
            var key = operand[10..];
            if (ctx.Risk.TryGetValue(key, out var v) &&
                decimal.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
            {
                metadata[operand] = v;
                return d;
            }
            metadata[operand] = "0";
            return 0m;
        }
        return decimal.TryParse(operand, NumberStyles.Any, CultureInfo.InvariantCulture, out var lit)
            ? lit
            : 0m;
    }
}

/// <summary>
/// No-op IRateLookup for policy adjustment pipelines that consist entirely of compute/round steps
/// and do not need rate table data. Returns 1.0 for all lookups so that any accidental
/// lookup step is a no-op multiply rather than a hard crash.
/// </summary>
public sealed class NullRateLookup : IRateLookup
{
    public static readonly NullRateLookup Instance = new();
    private NullRateLookup() { }
    public decimal GetFactor(string rateTable, IReadOnlyDictionary<string, string> keys, DateOnly effDate) => 1m;
    public decimal GetInterpolatedFactor(string rateTable, IReadOnlyDictionary<string, string> keys, string interpolationKey, DateOnly effDate) => 1m;
    public decimal GetRangeKeyFactor(string rateTable, IReadOnlyDictionary<string, string> keys, string rangeKey, decimal rangeValue, DateOnly effDate) => 1m;
}

public sealed class RoundStep : IRatingStep
{
    public required string Id { get; init; }
    public string Name { get; init; } = "Round";
    public string? Category { get; init; }
    public int Precision { get; init; } = 2;
    public MidpointRounding Mode { get; init; } = MidpointRounding.AwayFromZero;
    public bool ShouldExecute(RateContext ctx) => true;
    public RateResult Execute(RateContext ctx, IRateLookup _)
    {
        var before = ctx.Premium;
        var after = Math.Round(before, Precision, Mode);
        return new RateResult(after, new RatingTrace(Id, Name, null, null, null, before, after, $"round({Precision},{Mode})", Category)
        {
            Formula = $"Round({before}, {Precision})"
        });
    }
}

/// <summary>
/// Adjustment step that applies a constant (fixed) value to the running premium.
/// The math operation (mul/add/sub/set) determines how the constant is applied.
/// </summary>
public abstract class ConstantAdjustmentStepBase : IRatingStep
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Category { get; init; }
    public required decimal Constant { get; init; }
    public Func<RateContext, bool> Predicate { get; init; } = _ => true;
    public bool ShouldExecute(RateContext ctx) => Predicate(ctx);
    public abstract RateResult Execute(RateContext ctx, IRateLookup lookup);
}

public sealed class ConstantMultiplyStep : ConstantAdjustmentStepBase
{
    public override RateResult Execute(RateContext ctx, IRateLookup _)
    {
        var before = ctx.Premium;
        var after = before * Constant;
        return new RateResult(after, new RatingTrace(Id, Name, null, null, Constant, before, after, "mul-constant", Category)
        {
            Formula = $"{before} * {Constant}"
        });
    }
}

public sealed class ConstantAddStep : ConstantAdjustmentStepBase
{
    public override RateResult Execute(RateContext ctx, IRateLookup _)
    {
        var before = ctx.Premium;
        var after = before + Constant;
        return new RateResult(after, new RatingTrace(Id, Name, null, null, Constant, before, after, "add-constant", Category)
        {
            Formula = $"{before} + {Constant}"
        });
    }
}

public sealed class ConstantSubtractStep : ConstantAdjustmentStepBase
{
    public override RateResult Execute(RateContext ctx, IRateLookup _)
    {
        var before = ctx.Premium;
        var after = before - Constant;
        return new RateResult(after, new RatingTrace(Id, Name, null, null, Constant, before, after, "sub-constant", Category)
        {
            Formula = $"{before} - {Constant}"
        });
    }
}

public sealed class ConstantSetStep : ConstantAdjustmentStepBase
{
    public override RateResult Execute(RateContext ctx, IRateLookup _)
    {
        var before = ctx.Premium;
        return new RateResult(Constant, new RatingTrace(Id, Name, null, null, Constant, before, Constant, "set-constant", Category)
        {
            Formula = $"{Constant}"
        });
    }
}
