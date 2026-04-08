using Dapper;

namespace RatingEngine.Data.Admin;

public sealed class SqlColumnDefAdminRepository : IColumnDefAdminRepository
{
    private readonly DbConnectionFactory _db;
    public SqlColumnDefAdminRepository(DbConnectionFactory db) => _db = db;

    public async Task<IReadOnlyList<ColumnDefDetail>> ListAsync(string tableName, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT d.Id, d.ColumnName, d.DisplayLabel, d.DataType, d.SortOrder, d.IsRequired
            FROM RateTableColumnDef d
            JOIN RateTable t ON t.Id = d.RateTableId
            WHERE t.Name = @TableName
            ORDER BY d.SortOrder
            """;

        using var conn = _db.Create();
        return (await conn.QueryAsync<ColumnDefDetail>(new CommandDefinition(
            sql,
            new { TableName = tableName },
            cancellationToken: cancellationToken))).AsList();
    }

    public async Task ReplaceAsync(string tableName, IReadOnlyList<ColumnDefRequest> columnDefs, CancellationToken cancellationToken = default)
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

        var tableId = await conn.ExecuteScalarAsync<int?>(new CommandDefinition(
            tableIdSql,
            new { Name = tableName },
            cancellationToken: cancellationToken))
            ?? throw new InvalidOperationException($"Rate table '{tableName}' not found.");

        using var tx = conn.BeginTransaction();

        await conn.ExecuteAsync(new CommandDefinition(
            deleteSql,
            new { TableId = tableId },
            transaction: tx,
            cancellationToken: cancellationToken));

        foreach (var col in columnDefs)
            await conn.ExecuteAsync(new CommandDefinition(
                insertSql,
                new
                {
                    RateTableId = tableId,
                    col.ColumnName, col.DisplayLabel, col.DataType, col.SortOrder, col.IsRequired
                },
                transaction: tx,
                cancellationToken: cancellationToken));

        tx.Commit();
    }

    public async Task<bool> UpdateAsync(int columnDefId, ColumnDefRequest req, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE RateTableColumnDef
            SET ColumnName = @ColumnName, DisplayLabel = @DisplayLabel,
                DataType = @DataType, SortOrder = @SortOrder, IsRequired = @IsRequired
            WHERE Id = @Id
            """;

        using var conn = _db.Create();
        return await conn.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                Id = columnDefId,
                req.ColumnName, req.DisplayLabel, req.DataType, req.SortOrder, req.IsRequired
            },
            cancellationToken: cancellationToken)) > 0;
    }

    public async Task<bool> DeleteAsync(int columnDefId, CancellationToken cancellationToken = default)
    {
        using var conn = _db.Create();
        return await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM RateTableColumnDef WHERE Id = @Id",
            new { Id = columnDefId },
            cancellationToken: cancellationToken)) > 0;
    }
}
