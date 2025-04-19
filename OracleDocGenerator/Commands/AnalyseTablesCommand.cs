using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using Spectre.Console;
using Spectre.Console.Cli;
using System.Text.Json;


public class AnalyseTablesCommand : AsyncCommand<AnalyseTablesCommandSettings>
{
    private readonly ILogger _logger;
    private readonly IDescriptionGenerator _descriptionGenerator;

    public AnalyseTablesCommand(ILogger logger, IDescriptionGenerator descriptionGenerator)
    {
        _logger = logger;
        _descriptionGenerator = descriptionGenerator;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, AnalyseTablesCommandSettings settings)
    {
        var config = settings.MergeAndPrompt();
        var connectionString = config.Connection.BuildConnectionString();

        return await AnsiConsole.Status()
            .Start("Analyzing tables...", async ctx =>
            {
                ctx.Status("Connecting to database...");
                using var connection = new OracleConnection(connectionString);
                try
                {
                    connection.Open();
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]Connection failed:[/] {ex.Message}");
                    return -1;
                }

                AnsiConsole.MarkupLine("[green]Connected.[/]");

                ctx.Status("Extracting table metadata...");
                var extractor = new TableAnalyser(connectionString, _logger);
                var tables = extractor.AnalyzeTables(config.Connection.Schema);

                // If the user authorised the use of ChatGPT, use it to complete missing column descriptions
                if (settings.UseChatGpt)
                {
                    foreach (var table in tables)
                    {
                        await _descriptionGenerator.GenerateMissingColumnDescriptionsAsync(table);
                    }
                }

                ctx.Status("Exporting table catalog to JSON...");
                var jsonPath = "table_catalog.json";
                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                File.WriteAllText(jsonPath, JsonSerializer.Serialize(tables, jsonOptions));
                AnsiConsole.MarkupLine($"[green]Exported JSON catalog to [bold]{jsonPath}[/][/].");

                ctx.Status("Generating Markdown documentation...");
                var docGenerator = new MarkdownDocGeneratorForTables();
                docGenerator.Generate(tables, "docs");
                AnsiConsole.MarkupLine($"[green]Markdown documentation generated in [bold]docs/[/][/].");

                return 0;
            });
    }
}
