namespace RatingEngine.Data.Admin;

public interface IRateTableAdminRepository
{
    Task<IReadOnlyList<RateTableSummary>> ListAsync(int coverageConfigId, CancellationToken cancellationToken = default);
    Task<RateTableDetail?> GetAsync(int coverageConfigId, string name, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(CreateRateTableRequest request, string? actor = null, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(int id, UpdateRateTableRequest request, string? actor = null, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RateTableRowDetail>> GetRowsAsync(int coverageConfigId, string tableName, DateOnly? effectiveDate = null, CancellationToken cancellationToken = default);
    Task<long> AddRowAsync(int coverageConfigId, string tableName, CreateRateTableRowRequest request, CancellationToken cancellationToken = default);
    Task<bool> UpdateRowAsync(int coverageConfigId, string tableName, long rowId, CreateRateTableRowRequest request, CancellationToken cancellationToken = default);
    Task<bool> ExpireRowAsync(long rowId, DateOnly expireAt, CancellationToken cancellationToken = default);
    Task<bool> DeleteRowAsync(long rowId, CancellationToken cancellationToken = default);
    Task<int> BulkInsertRowsAsync(int coverageConfigId, string tableName, IReadOnlyList<CreateRateTableRowRequest> rows, CancellationToken cancellationToken = default);
}
