using RatingEngine.Core;

namespace RatingEngine.Data.Admin;

public interface IPipelineStepAdminRepository
{
    Task<IReadOnlyList<StepConfig>> ListStepsAsync(int coverageConfigId);
    Task<int> AddStepAsync(int coverageConfigId, StepConfig step, int? insertAfterOrder = null);
    Task<bool> UpdateStepAsync(int coverageConfigId, string stepId, StepConfig step);
    Task<bool> DeleteStepAsync(int coverageConfigId, string stepId);
    /// <summary>
    /// Reorders steps to match the supplied ordered list of StepIds.
    /// Any step not in the list is appended at the end in its original relative order.
    /// </summary>
    Task ReorderStepsAsync(int coverageConfigId, IReadOnlyList<string> orderedStepIds);
}
