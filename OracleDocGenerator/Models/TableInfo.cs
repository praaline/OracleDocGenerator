public class TableInfo
{
    public string Name { get; set; } = "";
    public string? Comment { get; set; }
    public string? Schema { get; set; }

    public List<ColumnInfo> Columns { get; set; } = new();
    public List<ConstraintInfo> Constraints { get; set; } = new();
    public List<IndexInfo> Indexes { get; set; } = new();
}
