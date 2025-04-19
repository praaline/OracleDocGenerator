public class ConstraintInfo
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = ""; // e.g., PRIMARY KEY, FOREIGN KEY, UNIQUE
    public List<string> Columns { get; set; } = new();
    public string? ReferenceTable { get; set; }
    public List<string>? ReferenceColumns { get; set; }
}