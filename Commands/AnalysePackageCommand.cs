using System.Text.Json;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using Spectre.Console;
using Spectre.Console.Cli;

public class AnalysePackageCommand : AsyncCommand<AnalysePackageCommandSettings>
{
    private readonly ILogger<AnalysePackageCommand> _logger;

    public AnalysePackageCommand(ILogger<AnalysePackageCommand> logger)
    {
        _logger = logger;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, AnalysePackageCommandSettings settings)
    {
        var config = settings.MergeAndPrompt();
        var connectionString = config.Connection.BuildConnectionString();
        var packageName = settings.PackageName;

        return AnsiConsole.Status().Start("Analyzing package...", ctx =>
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

            if (string.IsNullOrWhiteSpace(packageName))
            {
                var selector = new PackageSelector(conn, config.Connection.Schema);
                packageName = selector.PromptForPackage();
            }

            ctx.Status($"Extracting package {packageName}...");
            var chatGptHelper = (settings.UseChatGpt ?? false) ? new ChatGptAnalyzer() : null;
            var extractor = new PLSQLCodeCatalogExtractor(conn, _logger, chatGptHelper);
            var catalog = new DatabaseCatalog();

            var package = new PackageInfo { Name = packageName };
            extractor.ExtractRoutines(ctx, config.Connection.Schema, package, isStandalone: false);
            catalog.Packages.Add(package);

            if (settings.UseChatGpt ?? false)
            {
                int total = package.Procedures.Count + package.Functions.Count;
                int analyzed = 0;

                foreach (var proc in package.Procedures)
                {
                    proc.Description = chatGptHelper?.SummarizeProcedure(proc.Body, proc.Name) ?? string.Empty;
                    ctx.Status($"Summarized {++analyzed}/{total} procedures...");
                }

                foreach (var func in package.Functions)
                {
                    func.Description = chatGptHelper?.SummarizeProcedure(func.Body, func.Name) ?? string.Empty;
                    ctx.Status($"Summarized {++analyzed}/{total} functions...");
                }
            }

            ctx.Status("Exporting package catalog to JSON...");
            File.WriteAllText($"docs/catalog_{packageName}.json", JsonSerializer.Serialize(catalog, new JsonSerializerOptions { WriteIndented = true }));

            ctx.Status("Generating Markdown documentation...");
            new MarkdownDocGenerator().Generate(catalog);

            ctx.Status("Generating dependency graph...");
            var graphGen = new DependencyGraphGenerator();
            graphGen.Generate($"docs/catalog_{packageName}.json", $"docs/graph_{packageName}.dot");
            graphGen.GenerateMermaid($"docs/catalog_{packageName}.json", $"docs/graph_{packageName}.mmd", config.Connection.Schema);
            graphGen.RenderDotGraph($"docs/graph_{packageName}.dot", $"docs/graph_{packageName}.png");

            AnsiConsole.MarkupLine($"[green]Documentation and graphs generated for package [bold]{packageName}[/].[/]");
            return 0;
        });
    }
}
