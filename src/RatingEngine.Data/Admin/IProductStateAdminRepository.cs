namespace RatingEngine.Data.Admin;

public interface IProductStateAdminRepository
{
    Task<IReadOnlyList<ProductStateDetail>> ListAsync(int manifestId, CancellationToken cancellationToken = default);
    Task<int> AddAsync(int manifestId, AddProductStateRequest req, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
