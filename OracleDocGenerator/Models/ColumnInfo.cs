public class ColumnInfo
{
    public string Name { get; set; } = "";
    public string DataType { get; set; } = "";
    public int? DataLength { get; set; }
    public int? DataPrecision { get; set; }
    public int? DataScale { get; set; }
    public bool IsNullable { get; set; }
    public string? DefaultValue { get; set; }
    public bool IsPrimaryKey { get; set; }
    public bool IsForeignKey { get; set; }
    public string? ForeignKeyReference { get; set; }
    public string? Comment { get; set; }
   
    // For distinct value analysis
    public int? DistinctValueCount { get; set; }
    public List<string>? SampleDistinctValues { get; set; }
    public bool DistinctValuesSampled { get; set; }
}
