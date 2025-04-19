using System.Text.Json;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using Spectre.Console;
using Spectre.Console.Cli;

public class AnalyseSchemaCommand : AsyncCommand<AnalyseSchemaCommandSettings>
{
    private readonly ILogger<AnalyseSchemaCommand> _logger;

    public AnalyseSchemaCommand(ILogger<AnalyseSchemaCommand> logger)
    {
        _logger = logger;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, AnalyseSchemaCommandSettings settings)
    {
        var config = settings.MergeAndPrompt();
        var connectionString = config.Connection.BuildConnectionString();

        return AnsiConsole.Status().Start("Analyzing database...", ctx =>
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

            ctx.Status("Extracting PL/SQL packages...");
            var chatGptHelper = (settings.UseChatGpt ?? false) ? new ChatGptAnalyzer() : null;
            var extractor = new PLSQLCodeCatalogExtractor(conn, _logger, chatGptHelper);
            var catalog = extractor.Extract(ctx, settings.Schema ?? string.Empty);

            if (settings.UseChatGpt ?? false)
            {
                ctx.Status("Summarizing procedures with ChatGPT...");
                int totalProcedures = catalog.Packages.Sum(p => p.Procedures.Count + p.Functions.Count);
                int analyzed = 0;

                foreach (var package in catalog.Packages)
                {
                    foreach (var proc in package.Procedures)
                    {
                        proc.Description = chatGptHelper?.SummarizeProcedure(proc.Body, proc.Name) ?? string.Empty;
                        ctx.Status($"Summarized {++analyzed}/{totalProcedures} procedures...");
                    }

                    foreach (var func in package.Functions)
                    {
                        func.Description = chatGptHelper?.SummarizeProcedure(func.Body, func.Name) ?? string.Empty;
                        ctx.Status($"Summarized {++analyzed}/{totalProcedures} functions...");
                    }
                }
            }

            ctx.Status("Exporting catalog to JSON...");
            File.WriteAllText("docs/db_catalog.json", JsonSerializer.Serialize(catalog, new JsonSerializerOptions { WriteIndented = true }));

            ctx.Status("Generating Markdown documentation...");
            new PlSqlCodeMarkdownDocGenerator().Generate(catalog);

            ctx.Status("Generating dependency graphs...");
            var graphGen = new DependencyGraphGenerator();
            graphGen.Generate("docs/db_catalog.json", "docs/dependency_graph.dot");
            graphGen.GenerateMermaid("docs/db_catalog.json", "docs/dependency_graph.mmd", settings.Schema ?? string.Empty);
            graphGen.RenderDotGraph("docs/dependency_graph.dot", "docs/dependency_graph.png");

            AnsiConsole.MarkupLine("[green]Catalog exported to [bold]db_catalog.json[/], Markdown documentation generated, and dependency graph created.[/]");
            return 0;
        });
    }
}
