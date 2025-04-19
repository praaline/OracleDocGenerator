// DTOs
public class DatabaseCatalog
{
    public List<PackageInfo> Packages { get; set; } = new();
}

public class PackageInfo
{
    public string Name { get; set; } = string.Empty;
    public List<PlsqlRoutine> Procedures { get; set; } = new();
    public List<PlsqlRoutine> Functions { get; set; } = new();
}

public class PlsqlRoutine
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<PlsqlParameter> Parameters { get; set; } = new();
    public List<PlsqlDependency> Dependencies { get; set; } = new();
}

public class PlsqlParameter
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public class PlsqlDependency
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Schema { get; set; } = string.Empty;
    public string Usage { get; set; } = string.Empty;
}
