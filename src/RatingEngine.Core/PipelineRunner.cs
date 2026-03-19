
namespace RatingEngine.Core;

public sealed class PipelineRunner
{
    private readonly IReadOnlyList<IRatingStep> _steps;
    private readonly IRateLookup _lookup;

    public PipelineRunner(IReadOnlyList<IRatingStep> steps, IRateLookup lookup)
    {
        _steps = steps;
        _lookup = lookup;
    }

    public (decimal premium, List<RatingTrace> trace) Run(RateContext ctx)
    {
        var trace = new List<RatingTrace>();
        var p = ctx.Premium;
        foreach (var step in _steps)
        {
            if (!step.ShouldExecute(ctx with { Premium = p })) continue;
            var result = step.Execute(ctx with { Premium = p }, _lookup);
            p = result.NewPremium;
            trace.Add(result.Trace);
        }
        return (p, trace);
    }
}
