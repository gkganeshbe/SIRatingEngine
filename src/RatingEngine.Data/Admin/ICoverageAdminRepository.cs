namespace RatingEngine.Data.Admin;

public interface ICoverageAdminRepository
{
    Task<IReadOnlyList<CoverageSummary>> ListAsync(string? productCode = null);
    Task<CoverageDetail?> GetAsync(string productCode, string coverageCode, string version);
    Task<int> CreateAsync(CreateCoverageRequest request, string? actor = null);
    Task<bool> UpdateAsync(int id, UpdateCoverageRequest request, string? actor = null);
    Task<bool> ExpireAsync(int id, DateOnly expireAt, string? actor = null);
    Task<bool> DeleteAsync(int id);
}
