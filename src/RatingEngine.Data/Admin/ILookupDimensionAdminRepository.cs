namespace RatingEngine.Data.Admin;

public interface ILookupDimensionAdminRepository
{
    Task<IReadOnlyList<LookupDimensionSummary>> ListAsync(int? productManifestId = null, CancellationToken cancellationToken = default);
    Task<LookupDimensionDetail?> GetAsync(int id, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(int? productManifestId, CreateLookupDimensionRequest req, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(int id, UpdateLookupDimensionRequest req, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<int> AddValueAsync(int dimensionId, CreateLookupValueRequest req, CancellationToken cancellationToken = default);
    Task<bool> DeleteValueAsync(int valueId, CancellationToken cancellationToken = default);
}
