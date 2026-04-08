namespace RatingEngine.Data.Admin;

public interface IProductAdminRepository
{
    Task<IReadOnlyList<ProductSummary>> ListAsync(CancellationToken cancellationToken = default);
    Task<ProductDetail?> GetAsync(string productCode, string version, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(CreateProductRequest request, string? actor = null, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(int id, UpdateProductRequest request, string? actor = null, CancellationToken cancellationToken = default);
    Task<bool> ExpireAsync(int id, DateOnly expireAt, string? actor = null, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
