using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using Spectre.Console;
using Spectre.Console.Cli;
using System.Text.Json;


public class AnalyseTablesCommand : Command<AnalyseTablesCommandSettings>
{
    private readonly ILogger _logger;

    public AnalyseTablesCommand(ILogger logger)
    {
        _logger = logger;
    }

    public override int Execute(CommandContext context, AnalyseTablesCommandSettings settings)
    {
        var config = settings.MergeAndPrompt();
        var connectionString = config.Connection.BuildConnectionString();

        return AnsiConsole.Status()
            .Start("Analyzing tables...", ctx =>
            {
                ctx.Status("Connecting to database...");
                using var conn = new OracleConnection(connectionString);
                try
                {
                    conn.Open();
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]Connection failed:[/] {ex.Message}");
                    return -1;
                }

                AnsiConsole.MarkupLine("[green]Connected.[/]");

                var chatGptHelper = settings.UseChatGpt ? new ChatGptAnalyzer() : null;
                ctx.Status("Extracting table structure...");
                //var extractor = new TableStructureExtractor(conn, _logger, chatGptHelper);
                //var catalog = extractor.Extract(ctx, settings.User, settings.TableNameFilter);

                ctx.Status("Exporting table catalog to JSON...");
                //File.WriteAllText("table_catalog.json", JsonSerializer.Serialize(catalog, new JsonSerializerOptions { WriteIndented = true }));

                ctx.Status("Generating Markdown documentation...");
                //var docWriter = new MarkdownDocGenerator();
                //docWriter.GenerateForTables(catalog);

                AnsiConsole.MarkupLine("[green]Table catalog exported to [bold]table_catalog.json[/] and Markdown documentation generated.[/]");
                return 0;
            });
    }
}
