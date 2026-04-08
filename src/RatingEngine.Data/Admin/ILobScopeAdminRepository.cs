namespace RatingEngine.Data.Admin;

public interface ILobScopeAdminRepository
{
    Task<IReadOnlyList<LobScopeDetail>> ListAsync(int lobId, CancellationToken cancellationToken = default);
    Task<int> AddAsync(int lobId, AddLobScopeRequest req, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> UpdateCoverageRefAsync(int coverageRefId, UpdateCoverageRefRequest req, CancellationToken cancellationToken = default);
}
