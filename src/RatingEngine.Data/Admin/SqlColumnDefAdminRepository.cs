using Dapper;

namespace RatingEngine.Data.Admin;

public sealed class SqlColumnDefAdminRepository : IColumnDefAdminRepository
{
    private readonly DbConnectionFactory _db;
    public SqlColumnDefAdminRepository(DbConnectionFactory db) => _db = db;

    public async Task<IReadOnlyList<ColumnDefDetail>> ListAsync(string tableName)
    {
        const string sql = """
            SELECT d.Id, d.ColumnName, d.DisplayLabel, d.DataType, d.SortOrder, d.IsRequired
            FROM RateTableColumnDef d
            JOIN RateTable t ON t.Id = d.RateTableId
            WHERE t.Name = @TableName
            ORDER BY d.SortOrder
            """;

        using var conn = _db.Create();
        return (await conn.QueryAsync<ColumnDefDetail>(sql, new { TableName = tableName })).AsList();
    }

    public async Task ReplaceAsync(string tableName, IReadOnlyList<ColumnDefRequest> columnDefs)
    {
        const string tableIdSql = "SELECT Id FROM RateTable WHERE Name = @Name";
        const string deleteSql  = "DELETE FROM RateTableColumnDef WHERE RateTableId = @TableId";
        const string insertSql  = """
            INSERT INTO RateTableColumnDef
                (RateTableId, ColumnName, DisplayLabel, DataType, SortOrder, IsRequired)
            VALUES (@RateTableId, @ColumnName, @DisplayLabel, @DataType, @SortOrder, @IsRequired)
            """;

        using var conn = _db.Create();
        conn.Open();

        var tableId = await conn.ExecuteScalarAsync<int?>(tableIdSql, new { Name = tableName })
            ?? throw new InvalidOperationException($"Rate table '{tableName}' not found.");

        using var tx = conn.BeginTransaction();

        await conn.ExecuteAsync(deleteSql, new { TableId = tableId }, tx);

        foreach (var col in columnDefs)
            await conn.ExecuteAsync(insertSql, new
            {
                RateTableId = tableId,
                col.ColumnName, col.DisplayLabel, col.DataType, col.SortOrder, col.IsRequired
            }, tx);

        tx.Commit();
    }

    public async Task<bool> UpdateAsync(int columnDefId, ColumnDefRequest req)
    {
        const string sql = """
            UPDATE RateTableColumnDef
            SET ColumnName = @ColumnName, DisplayLabel = @DisplayLabel,
                DataType = @DataType, SortOrder = @SortOrder, IsRequired = @IsRequired
            WHERE Id = @Id
            """;

        using var conn = _db.Create();
        return await conn.ExecuteAsync(sql, new
        {
            Id = columnDefId,
            req.ColumnName, req.DisplayLabel, req.DataType, req.SortOrder, req.IsRequired
        }) > 0;
    }

    public async Task<bool> DeleteAsync(int columnDefId)
    {
        using var conn = _db.Create();
        return await conn.ExecuteAsync(
            "DELETE FROM RateTableColumnDef WHERE Id = @Id", new { Id = columnDefId }) > 0;
    }
}
