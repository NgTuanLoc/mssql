namespace EfCoreLessons.Lab;

public record ColumnInfo(string Column, string Type, int? MaxLength, bool Nullable);

// Reads the schema EF actually created, for the code-first lessons' checks. Runs through Reference:
// same transaction, not counted in Sql.
public static class Schema
{
    public static Task<List<string>> TablesAsync(this Reference reference)
        => reference.Rows<string>($"SELECT name AS Value FROM sys.tables ORDER BY name");

    public static Task<List<ColumnInfo>> ColumnsAsync(this Reference reference, string table)
        => reference.Rows<ColumnInfo>($"""
            SELECT COLUMN_NAME AS [Column], DATA_TYPE AS Type, CHARACTER_MAXIMUM_LENGTH AS MaxLength,
                   CAST(CASE IS_NULLABLE WHEN 'YES' THEN 1 ELSE 0 END AS bit) AS Nullable
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_NAME = {table}
            ORDER BY ORDINAL_POSITION
            """);

    // The primary key's columns, in key order.
    public static Task<List<string>> PrimaryKeyAsync(this Reference reference, string table)
        => reference.Rows<string>($"""
            SELECT c.name AS Value
            FROM sys.indexes i
            JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            WHERE i.object_id = OBJECT_ID({table}) AND i.is_primary_key = 1
            ORDER BY ic.key_ordinal
            """);

    // For each foreign key on `table`: "Column -> ReferencedTable.ReferencedColumn ON DELETE action".
    public static Task<List<string>> ForeignKeysAsync(this Reference reference, string table)
        => reference.Rows<string>($"""
            SELECT CONCAT(c.name COLLATE DATABASE_DEFAULT, ' -> ', OBJECT_NAME(fk.referenced_object_id) COLLATE DATABASE_DEFAULT,
                          '.', rc.name COLLATE DATABASE_DEFAULT,
                          ' ON DELETE ', fk.delete_referential_action_desc COLLATE DATABASE_DEFAULT) AS Value
            FROM sys.foreign_keys fk
            JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
            JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
            JOIN sys.columns rc ON rc.object_id = fkc.referenced_object_id AND rc.column_id = fkc.referenced_column_id
            WHERE fk.parent_object_id = OBJECT_ID({table})
            ORDER BY c.name
            """);
}
