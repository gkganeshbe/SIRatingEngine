namespace RatingEngine.Data.Admin;

public interface IPolicyAdjustmentAdminRepository
{
    Task<IReadOnlyList<PolicyAdjustmentDetail>> ListAsync(int productManifestId, CancellationToken cancellationToken = default);
    Task<PolicyAdjustmentDetail?> GetAsync(int id, CancellationToken cancellationToken = default);
    Task<int>  CreateAsync(int productManifestId, CreatePolicyAdjustmentRequest req, string? actor = null, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(int id, UpdatePolicyAdjustmentRequest req, string? actor = null, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
