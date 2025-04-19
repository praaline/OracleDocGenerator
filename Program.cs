using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console.Cli;


class Program
{
    static int Main(string[] args)
    {
        var services = new ServiceCollection();

        services.AddLogging(configure =>
        {
            configure.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = "[HH:mm:ss] ";
            });
            configure.SetMinimumLevel(LogLevel.Information);
        });

        // Register services
        services.AddSingleton<PLSQLCodeCatalogExtractor>();
        services.AddSingleton<PackageSourceCache>();
        services.AddSingleton<ChatGptAnalyzer>();

        var registrar = new TypeRegistrar(services);
        var app = new CommandApp(registrar);

        app.Configure(config =>
        {
            config.SetApplicationName("plsql-tool");

            config.AddCommand<AnalyseSchemaCommand>("full")
                  .WithDescription("Analyze all PL/SQL code in a schema");

            config.AddCommand<AnalysePackageCommand>("package")
                  .WithDescription("Analyze a specific package");

            config.AddCommand<AnalyseTablesCommand>("tables")
                  .WithDescription("Analyze all tables in a schema");
        });

        // If no args, default to "full"
        if (args.Length == 0)
        {
            args = ["package"];
        }

        try
        {
            return app.Run(args);
        }
        catch (Exception ex)
        {
            var logger = services.BuildServiceProvider().GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "An unhandled exception occurred.");
            return -1;
        }
    }
}