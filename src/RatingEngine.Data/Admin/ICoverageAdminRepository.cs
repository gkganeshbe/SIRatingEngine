namespace RatingEngine.Data.Admin;

public interface ICoverageAdminRepository
{
    Task<IReadOnlyList<CoverageSummary>> ListAsync(int? coverageRefId = null, int? productManifestId = null, CancellationToken cancellationToken = default);
    Task<CoverageDetail?> GetAsync(int id, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(CreateCoverageRequest request, string? actor = null, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(int id, UpdateCoverageRequest request, string? actor = null, CancellationToken cancellationToken = default);
    Task<bool> ExpireAsync(int id, DateOnly expireAt, string? actor = null, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
