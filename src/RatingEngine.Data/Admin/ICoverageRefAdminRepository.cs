namespace RatingEngine.Data.Admin;

public interface ICoverageRefAdminRepository
{
    /// <summary>Adds a coverage type to a product's catalog.</summary>
    Task<int> AddAsync(int productManifestId, AddCoverageRefRequest req, CancellationToken cancellationToken = default);
    /// <summary>Removes a coverage type from a product's catalog (cascades to all its CoverageConfig rows).</summary>
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
