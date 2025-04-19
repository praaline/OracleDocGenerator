using System.Text;

public class MarkdownDocGeneratorForTables
{
    public void Generate(List<TableInfo> tables, string outputDir)
    {
        Directory.CreateDirectory(outputDir);

        foreach (var table in tables)
        {
            var markdown = GenerateTableMarkdown(table);
            var filePath = Path.Combine(outputDir, $"{table.Name}.md");
            File.WriteAllText(filePath, markdown);
        }
    }

    public string GenerateTableMarkdown(TableInfo table)
    {
        var sb = new StringBuilder();

        // Optional: Frontmatter for static site generators
        sb.AppendLine($"---");
        sb.AppendLine($"title: Table {table.Name}");
        sb.AppendLine($"schema: {table.Schema ?? "N/A"}");
        sb.AppendLine($"generated: {DateTime.UtcNow:yyyy-MM-dd}");
        sb.AppendLine($"---\n");

        sb.AppendLine($"# Table `{table.Schema}.{table.Name}`");

        if (!string.IsNullOrWhiteSpace(table.Comment))
            sb.AppendLine($"\n> {EscapeMarkdown(table.Comment)}\n");

        sb.AppendLine(GenerateSummary(table));
        sb.AppendLine(GenerateColumnsTable(table));
        sb.AppendLine(GenerateConstraintsTable(table));
        sb.AppendLine(GenerateIndexesTable(table));

        return sb.ToString();
    }

    private string GenerateSummary(TableInfo table)
    {
        var pkCount = table.Columns.Count(c => c.IsPrimaryKey);
        var fkCount = table.Columns.Count(c => c.IsForeignKey);
        var indexCount = table.Indexes?.Count ?? 0;

        return $@"
## Summary

- **Columns:** {table.Columns.Count}
- **Primary Key Columns:** {pkCount}
- **Foreign Keys:** {fkCount}
- **Indexes:** {indexCount}
";
    }

    private string GenerateColumnsTable(TableInfo table)
    {
        var sb = new StringBuilder();

        sb.AppendLine("\n## Columns\n");
        sb.AppendLine("| Name | Type | Nullable | Default | PK | FK | Comment |");
        sb.AppendLine("|------|------|----------|---------|----|----|---------|");

        foreach (var col in table.Columns)
        {
            sb.AppendLine($"| {col.Name} | {FormatType(col)} | {(col.IsNullable ? "Yes" : "No")} | " +
                          $"{EscapeMarkdown(col.DefaultValue ?? "")} | {(col.IsPrimaryKey ? "✔" : "")} | " +
                          $"{(col.IsForeignKey ? $"→ `{col.ForeignKeyReference}`" : "")} | {EscapeMarkdown(col.Comment ?? "")} |");

            if (col.DistinctValuesSampled && col.SampleDistinctValues?.Any() == true)
            {
                sb.AppendLine($"\n> Sample values for `{col.Name}`: {string.Join(", ", col.SampleDistinctValues.Select(EscapeMarkdown))}\n");
            }
        }

        return sb.ToString();
    }

    private string GenerateConstraintsTable(TableInfo table)
    {
        if (!table.Constraints?.Any() ?? true)
            return "";

        var sb = new StringBuilder();
        sb.AppendLine("\n## Constraints\n");
        sb.AppendLine("| Name | Type | Columns | Reference |");
        sb.AppendLine("|------|------|---------|-----------|");

        foreach (var c in table.Constraints!)
        {
            var columns = string.Join(", ", c.Columns);
            var reference = c.Type == "FOREIGN KEY" && !string.IsNullOrWhiteSpace(c.ReferenceTable)
                ? $"`{c.ReferenceTable}` ({string.Join(", ", c.ReferenceColumns ?? new())})"
                : "";

            sb.AppendLine($"| {c.Name} | {c.Type} | {columns} | {reference} |");
        }

        return sb.ToString();
    }

    private string GenerateIndexesTable(TableInfo table)
    {
        if (!table.Indexes?.Any() ?? true)
            return "";

        var sb = new StringBuilder();
        sb.AppendLine("\n## Indexes\n");
        sb.AppendLine("| Name | Unique | Columns |");
        sb.AppendLine("|------|--------|---------|");

        foreach (var i in table.Indexes!)
        {
            var columns = string.Join(", ", i.Columns);
            sb.AppendLine($"| {i.Name} | {(i.IsUnique ? "Yes" : "No")} | {columns} |");
        }

        return sb.ToString();
    }

    private string FormatType(ColumnInfo col)
    {
        var type = col.DataType.ToUpperInvariant();

        if ((type == "VARCHAR2" || type == "CHAR") && col.DataLength.HasValue)
            return $"{type}({col.DataLength})";

        if ((type == "NUMBER" || type == "DECIMAL") && col.DataPrecision.HasValue)
        {
            if (col.DataScale.HasValue && col.DataScale != 0)
                return $"{type}({col.DataPrecision},{col.DataScale})";
            return $"{type}({col.DataPrecision})";
        }

        return type;
    }

    private string EscapeMarkdown(string input) =>
        input?.Replace("|", "\\|").Replace("`", "\\`").Replace("<", "\\<").Replace(">", "\\>") ?? "";

}
