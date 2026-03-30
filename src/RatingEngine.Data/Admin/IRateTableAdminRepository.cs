namespace RatingEngine.Data.Admin;

public interface IRateTableAdminRepository
{
    Task<IReadOnlyList<RateTableSummary>> ListAsync(int coverageConfigId);
    Task<RateTableDetail?> GetAsync(int coverageConfigId, string name);
    Task<int> CreateAsync(CreateRateTableRequest request, string? actor = null);
    Task<bool> UpdateAsync(int id, UpdateRateTableRequest request, string? actor = null);
    Task<bool> DeleteAsync(int id);

    Task<IReadOnlyList<RateTableRowDetail>> GetRowsAsync(int coverageConfigId, string tableName, DateOnly? effectiveDate = null);
    Task<long> AddRowAsync(int coverageConfigId, string tableName, CreateRateTableRowRequest request);
    Task<bool> UpdateRowAsync(long rowId, CreateRateTableRowRequest request);
    Task<bool> ExpireRowAsync(long rowId, DateOnly expireAt);
    Task<bool> DeleteRowAsync(long rowId);
    Task<int> BulkInsertRowsAsync(int coverageConfigId, string tableName, IReadOnlyList<CreateRateTableRowRequest> rows);
}
