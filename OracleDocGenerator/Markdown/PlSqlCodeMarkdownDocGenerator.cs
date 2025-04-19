using System.Text;
using System.Text.Json;

public class PlSqlCodeMarkdownDocGenerator
{
    public void Generate(object catalog)
    {
        var root = JsonSerializer.SerializeToElement(catalog);
        Directory.CreateDirectory("docs");

        var packages = root.GetProperty("Packages");

        foreach (var pkg in packages.EnumerateArray())
        {
            var packageName = pkg.GetProperty("Name").GetString();
            var sb = new StringBuilder();

            sb.AppendLine($"# Package `{packageName}`\n");
            sb.AppendLine("## Contents\n");

            var index = new StringBuilder();

            var allItems = new List<JsonElement>();
            if (pkg.TryGetProperty("Procedures", out var procedures))
                allItems.AddRange(procedures.EnumerateArray());
            if (pkg.TryGetProperty("Functions", out var functions))
                allItems.AddRange(functions.EnumerateArray());

            foreach (var item in allItems)
            {
                var name = item.GetProperty("Name").GetString() ?? string.Empty;
                index.AppendLine($"- [{name}](#{name.ToLowerInvariant()})");

                sb.AppendLine($"### {name}\n");

                sb.AppendLine("#### Parameters\n");
                sb.AppendLine("| Name | Type |\n|------|------|");
                foreach (var param in item.GetProperty("Parameters").EnumerateArray())
                {
                    var pname = param.GetProperty("Name").GetString();
                    var type = param.GetProperty("Type").GetString();
                    sb.AppendLine($"| {pname} | {type} |");
                }

                sb.AppendLine("\n#### Dependencies\n");
                sb.AppendLine("| Name | Type | Usage |");
                sb.AppendLine("|------|------|--------|");
                foreach (var dep in item.GetProperty("Dependencies").EnumerateArray())
                {
                    var dname = dep.GetProperty("Name").GetString();
                    var dtype = dep.GetProperty("Type").GetString();
                    var usage = dep.GetProperty("Usage").GetString();
                    sb.AppendLine($"| {dname} | {dtype} | {usage} |");
                }
                sb.AppendLine();
            }

            File.WriteAllText(Path.Combine("docs", $"{packageName}.md"), index.ToString() + "\n---\n\n" + sb.ToString());
        }
    }
}
