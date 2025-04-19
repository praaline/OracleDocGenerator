public class IndexInfo
{
    public string Name { get; set; } = "";
    public bool IsUnique { get; set; }
    public List<string> Columns { get; set; } = new();
}