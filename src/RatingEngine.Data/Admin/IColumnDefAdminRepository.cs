namespace RatingEngine.Data.Admin;

public interface IColumnDefAdminRepository
{
    Task<IReadOnlyList<ColumnDefDetail>> ListAsync(string tableName);
    /// <summary>Replaces all column definitions for the table.</summary>
    Task ReplaceAsync(string tableName, IReadOnlyList<ColumnDefRequest> columnDefs);
    Task<bool> UpdateAsync(int columnDefId, ColumnDefRequest request);
    Task<bool> DeleteAsync(int columnDefId);
}
