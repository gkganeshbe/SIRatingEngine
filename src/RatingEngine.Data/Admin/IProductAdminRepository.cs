namespace RatingEngine.Data.Admin;

public interface IProductAdminRepository
{
    Task<IReadOnlyList<ProductSummary>> ListAsync();
    Task<ProductDetail?> GetAsync(string productCode, string version);
    Task<int> CreateAsync(CreateProductRequest request, string? actor = null);
    Task<bool> UpdateAsync(int id, UpdateProductRequest request, string? actor = null);
    Task<bool> ExpireAsync(int id, DateOnly expireAt, string? actor = null);
    Task<bool> DeleteAsync(int id);
}
