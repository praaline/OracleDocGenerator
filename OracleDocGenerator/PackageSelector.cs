using Oracle.ManagedDataAccess.Client;
using Spectre.Console;

public class PackageSelector
{
    private readonly OracleConnection _connection;
    private readonly string _schema;

    public PackageSelector(OracleConnection connection, string schema)
    {
        _connection = connection;
        _schema = schema.ToUpper();
    }

    public string PromptForPackage()
    {
        var packageNames = LoadPackageNames();
        if (packageNames.Count == 0)
        {
            AnsiConsole.MarkupLine($"[red]No packages found in schema '{_schema}'.[/]");
            return string.Empty;
        }

        return AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Select a [green]package[/] to analyze")
                .PageSize(10)
                .AddChoices(packageNames)
                .UseConverter(name => name));
    }

    private List<string> LoadPackageNames()
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT object_name
            FROM all_objects
            WHERE object_type = 'PACKAGE'
              AND owner = :owner
            ORDER BY object_name";
        cmd.Parameters.Add(new OracleParameter("owner", _schema));

        var results = new List<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(reader.GetString(0));
        }

        return results;
    }
}
