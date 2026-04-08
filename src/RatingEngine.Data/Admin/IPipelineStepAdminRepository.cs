using RatingEngine.Core;

namespace RatingEngine.Data.Admin;

public interface IPipelineStepAdminRepository
{
    Task<IReadOnlyList<StepConfig>> ListStepsAsync(int coverageConfigId, CancellationToken cancellationToken = default);
    Task<int> AddStepAsync(int coverageConfigId, StepConfig step, int? insertAfterOrder = null, CancellationToken cancellationToken = default);
    Task<bool> UpdateStepAsync(int coverageConfigId, string stepId, StepConfig step, CancellationToken cancellationToken = default);
    Task<bool> DeleteStepAsync(int coverageConfigId, string stepId, CancellationToken cancellationToken = default);
    /// <summary>
    /// Reorders steps to match the supplied ordered list of StepIds.
    /// Any step not in the list is appended at the end in its original relative order.
    /// </summary>
    Task ReorderStepsAsync(int coverageConfigId, IReadOnlyList<string> orderedStepIds, CancellationToken cancellationToken = default);
}
