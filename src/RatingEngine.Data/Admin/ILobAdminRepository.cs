namespace RatingEngine.Data.Admin;

public interface ILobAdminRepository
{
    Task<int>  AddAsync(int productManifestId, AddLobRequest req, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
