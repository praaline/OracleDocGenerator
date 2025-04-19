using System.Diagnostics;
using System.Text;
using System.Text.Json;

public class DependencyGraphGenerator
{
    public void Generate(string catalogPath, string outputDotFile)
    {
        var json = File.ReadAllText(catalogPath);
        var catalog = JsonSerializer.Deserialize<DatabaseCatalog>(json);

        if (catalog == null) return;

        using var writer = new StreamWriter(outputDotFile);
        writer.WriteLine("digraph G {");

        foreach (var package in catalog.Packages)
        {
            foreach (var routine in package.Procedures.Concat(package.Functions))
            {
                var fullName = $"{package.Name}.{routine.Name}";
                foreach (var dep in routine.Dependencies)
                {
                    writer.WriteLine($"\t\"{fullName}\" -> \"{dep.Name}\";");
                }
            }
        }

        writer.WriteLine("}");
    }

    public void RenderDotGraph(string dotFilePath, string outputImagePath)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dot",
                Arguments = $"-Tpng \"{dotFilePath}\" -o \"{outputImagePath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        process.WaitForExit();
    }

    public string GenerateMermaidGraph(DatabaseCatalog catalog, string currentSchema, bool crossSchemaOnly = false)
    {
        var sb = new StringBuilder();
        sb.AppendLine("graph TD");

        foreach (var package in catalog.Packages)
        {
            foreach (var routine in package.Procedures.Concat(package.Functions))
            {
                string routineLabel = $"{package.Name}.{routine.Name}";

                foreach (var dep in routine.Dependencies)
                {
                    bool isCrossSchema = !string.Equals(dep.Schema, currentSchema, StringComparison.OrdinalIgnoreCase);
                    if (!crossSchemaOnly || isCrossSchema)
                    {
                        string target = isCrossSchema ? $"{dep.Schema}.{dep.Name}" : $"{dep.Name}";
                        string edgeLabel = string.IsNullOrEmpty(dep.Usage) ? dep.Type : dep.Usage;
                        string styleClass = GetNodeClass(dep.Type);

                        sb.AppendLine($"    \"{routineLabel}\" -->|{edgeLabel}| \"{target}\":::{styleClass}");
                    }
                }
            }
        }

        sb.AppendLine("classDef TABLE fill:#ffefef,stroke:#ffaaaa;");
        sb.AppendLine("classDef VIEW fill:#e0f7fa,stroke:#4dd0e1;");
        sb.AppendLine("classDef FUNCTION fill:#e8f5e9,stroke:#81c784;");
        sb.AppendLine("classDef PROCEDURE fill:#fff3e0,stroke:#ffb74d;");
        sb.AppendLine("classDef UNKNOWN fill:#eeeeee,stroke:#aaaaaa;");

        return sb.ToString();
    }

    public void GenerateMermaid(string catalogPath, string outputFile, string currentSchema, bool crossSchemaOnly = false)
    {
        var json = File.ReadAllText(catalogPath);
        var catalog = JsonSerializer.Deserialize<DatabaseCatalog>(json);
        if (catalog is null) return;
        var graph = GenerateMermaidGraph(catalog, currentSchema, crossSchemaOnly);
        File.WriteAllText(outputFile, graph);
    }

    private string GetNodeClass(string type)
    {
        return type.ToUpper() switch
        {
            "TABLE" => "TABLE",
            "VIEW" => "VIEW",
            "FUNCTION" => "FUNCTION",
            "PROCEDURE" => "PROCEDURE",
            _ => "UNKNOWN"
        };
    }
}
