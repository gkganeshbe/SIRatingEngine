namespace RatingEngine.Data.Admin;

public interface IDerivedKeyAdminRepository
{
    Task<IReadOnlyList<DerivedKeyDetail>> ListAsync(int? productManifestId = null, CancellationToken cancellationToken = default);
    Task<DerivedKeyDetail?> GetAsync(int id, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(int? productManifestId, CreateDerivedKeyRequest req, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(int id, UpdateDerivedKeyRequest req, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
