namespace RatingEngine.Data.Admin;

public interface IRiskFieldRepository
{
    /// <summary>Returns global (ProductCode IS NULL) + product-specific fields for the given product.</summary>
    Task<IReadOnlyList<RiskField>> ListAsync(string? productCode = null, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(CreateRiskFieldRequest request, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(int id, UpdateRiskFieldRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
