namespace RatingEngine.Data.Admin;

public interface IColumnDefAdminRepository
{
    Task<IReadOnlyList<ColumnDefDetail>> ListAsync(string tableName, CancellationToken cancellationToken = default);
    /// <summary>Replaces all column definitions for the table.</summary>
    Task ReplaceAsync(string tableName, IReadOnlyList<ColumnDefRequest> columnDefs, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(int columnDefId, ColumnDefRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int columnDefId, CancellationToken cancellationToken = default);
}
