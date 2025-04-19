using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using Spectre.Console;

public class PLSQLCodeCatalogExtractor
{
    private readonly OracleConnection _connection;
    private readonly ChatGptAnalyzer? _chatGpt;
    private readonly ILogger _logger;

    private readonly PackageSourceCache _packageSourceCache;

    public PLSQLCodeCatalogExtractor(OracleConnection connection, ILogger logger, ChatGptAnalyzer? chatGpt = null)
    {
        _connection = connection;
        _chatGpt = chatGpt;
        _logger = logger;
        _packageSourceCache = new PackageSourceCache(_connection, _logger);
    }


    public DatabaseCatalog Extract(StatusContext ctx, string schemaName)
    {
        _logger.LogInformation($"Starting extraction for schema: {schemaName}");
        var catalog = new DatabaseCatalog();

        ctx.Status("Extracting PL/SQL packages...");
        ExtractPackages(ctx, schemaName, catalog);

        ctx.Status("Extracting standalone procedures, functions, and triggers...");
        ExtractStandaloneRoutines(ctx, schemaName, catalog);

        _logger.LogInformation("Extraction completed.");
        return catalog;
    }

    public Dictionary<string, string> ExtractAllRoutineSources(string schemaName)
    {
        _logger.LogInformation($"Loading all standalone routine sources for schema: {schemaName}");
        var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT name, text
            FROM all_source
            WHERE owner = :owner
              AND type IN ('PROCEDURE', 'FUNCTION', 'TRIGGER')
            ORDER BY name, line";
        cmd.Parameters.Add(new OracleParameter("owner", schemaName.ToUpper()));

        using var reader = cmd.ExecuteReader();
        var sourceDict = new Dictionary<string, List<string>>();

        while (reader.Read())
        {
            string name = reader.GetString(0);
            string text = reader.GetString(1);

            if (!sourceDict.ContainsKey(name))
                sourceDict[name] = new List<string>();

            sourceDict[name].Add(text);
        }

        return sourceDict.ToDictionary(kvp => kvp.Key, kvp => string.Join("\n", kvp.Value));
    }

    private void ExtractPackages(StatusContext ctx, string schemaName, DatabaseCatalog catalog)
    {
        _logger.LogDebug("Fetching package list...");
        var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT object_name
            FROM all_objects
            WHERE object_type = 'PACKAGE'
              AND owner = :owner";
        cmd.Parameters.Add(new OracleParameter("owner", schemaName.ToUpper()));

        using var reader = cmd.ExecuteReader();
        int count = 0;
        while (reader.Read())
        {
            var packageName = reader.GetString(0);
            _logger.LogDebug($"Analyzing package: {packageName}");
            ctx.Status($"Analyzing package: {packageName}...");
            var package = new PackageInfo { Name = packageName };
            ExtractRoutines(ctx, schemaName, package, isStandalone: false);
            catalog.Packages.Add(package);
            count++;
        }
        ctx.Status($"Finished analyzing {count} packages.");
        _logger.LogInformation($"Extracted {count} packages.");
    }

    private void ExtractStandaloneRoutines(StatusContext ctx, string schemaName, DatabaseCatalog catalog)
    {
        _logger.LogDebug("Extracting standalone routines...");
        var standalone = new PackageInfo { Name = "[Standalone]" };

        ExtractRoutines(ctx, schemaName, standalone, isStandalone: true);

        catalog.Packages.Add(standalone);
        _logger.LogInformation("Standalone routines extraction completed.");
    }

    public void ExtractRoutines(StatusContext ctx, string schemaName, PackageInfo package, bool isStandalone)
    {
        _logger.LogDebug($"Fetching {(isStandalone ? "standalone" : package.Name)} routines...");
        var cmd = _connection.CreateCommand();
        cmd.CommandText = isStandalone ? @"
            SELECT object_name, object_type
            FROM all_objects
            WHERE object_type IN ('PROCEDURE', 'FUNCTION', 'TRIGGER')
              AND owner = :owner
              AND object_name NOT IN (SELECT object_name FROM all_procedures WHERE object_type = 'PACKAGE')"
        : @"
            SELECT procedure_name AS object_name,
                   (SELECT DECODE(aa.ARGUMENT_NAME, NULL, 'FUNCTION', 'PROCEDURE')
                      FROM all_arguments aa
                     WHERE aa.owner = ap.owner
                       AND aa.package_name = ap.object_name
                       AND aa.object_name = ap.procedure_name
                       AND aa.subprogram_id = ap.subprogram_id
                       AND ROWNUM = 1) AS object_type
            FROM all_procedures ap
            WHERE owner = :owner
              AND object_name = :package
              AND procedure_name IS NOT NULL
              AND object_type = 'PACKAGE'
            ORDER BY subprogram_id";

        cmd.Parameters.Add(new OracleParameter("owner", schemaName.ToUpper()));
        if (!isStandalone)
        {
            cmd.Parameters.Add(new OracleParameter("package", package.Name));
        }

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var routineName = reader.GetString(0);
            var routineType = reader.GetString(1).ToLower();

            _logger.LogDebug($"Analyzing {(isStandalone ? "standalone" : "")} {routineType}: {routineName}");
            ctx.Status($"Analyzing {(isStandalone ? "standalone" : "")} {routineType}: {routineName}...");

            var parameters = (routineType != "trigger")
                ? ExtractParametersFromDb(schemaName, routineName, routineType, isStandalone ? null : package.Name)
                : new List<PlsqlParameter>();

            string body = (isStandalone || routineType == "trigger")
                ? ExtractRoutineSource(schemaName, routineName, routineType)
                : _packageSourceCache.ExtractRoutineBody(schemaName, package.Name, routineName);

            if (!string.IsNullOrWhiteSpace(body))
                body = StripDocumentationComments(body);

            var dependencies = ExtractDependenciesFromDb(schemaName, routineName, routineType, isStandalone ? null : package.Name);
            var inferredDeps = ExtractDependenciesFromBody(schemaName, body);

            foreach (var inferred in inferredDeps)
            {
                var matchDep = dependencies.FirstOrDefault(d => d.Name == inferred.Name && d.Type == inferred.Type);
                if (matchDep != null)
                {
                    matchDep.Usage = inferred.Usage;
                }
            }

            var routine = new PlsqlRoutine
            {
                Name = routineName,
                Type = routineType,
                Body = body,
                Parameters = parameters,
                Dependencies = dependencies
            };

            if (routineType == "procedure")
                package.Procedures.Add(routine);
            else
                package.Functions.Add(routine);
        }
    }

    private string ExtractRoutineSource(string schemaName, string routineName, string routineType)
    {
        _logger.LogDebug($"Loading source for standalone {routineType} {routineName}...");
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT text FROM all_source
            WHERE owner = :owner AND name = :name AND type = :type
            ORDER BY line";
        cmd.Parameters.Add(new OracleParameter("owner", schemaName.ToUpper()));
        cmd.Parameters.Add(new OracleParameter("name", routineName));
        cmd.Parameters.Add(new OracleParameter("type", routineType.ToUpper()));

        var bodyLines = new List<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            bodyLines.Add(reader.GetString(0));
        }
        return string.Join("\n", bodyLines);
    }

    private string StripDocumentationComments(string body)
    {
        return Regex.Replace(body, @"(?s)/\*.*?\*/|--.*?$", "", RegexOptions.Multiline);
    }

    private List<PlsqlParameter> ExtractParametersFromDb(string schemaName, string name, string type, string? package = null)
    {
        var parameters = new List<PlsqlParameter>();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT argument_name, data_type
            FROM all_arguments
            WHERE owner = :owner
              AND object_name = :name
              AND (package_name IS NULL OR package_name = :package)";
        cmd.Parameters.Add(new OracleParameter("owner", schemaName.ToUpper()));
        cmd.Parameters.Add(new OracleParameter("name", name));
        cmd.Parameters.Add(new OracleParameter("package", package ?? (object)DBNull.Value));

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var paramName = reader.IsDBNull(0) ? "RETURN" : reader.GetString(0);
            var dataType = reader.IsDBNull(1) ? "UNKNOWN" : reader.GetString(1);
            parameters.Add(new PlsqlParameter { Name = paramName, Type = dataType });
        }

        return parameters;
    }

    private List<PlsqlDependency> ExtractDependenciesFromDb(string schemaName, string name, string type, string? package = null)
    {
        var dependencies = new List<PlsqlDependency>();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT referenced_name, referenced_type, referenced_owner
            FROM all_dependencies
            WHERE name = :name
              AND (referenced_type IN ('TABLE', 'VIEW', 'PROCEDURE', 'FUNCTION'))
              AND referenced_owner = :owner";
        cmd.Parameters.Add(new OracleParameter("name", name));
        cmd.Parameters.Add(new OracleParameter("owner", schemaName.ToUpper()));

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var typeName = reader.GetString(1);
            var usage = InferUsageFromType(typeName);

            dependencies.Add(new PlsqlDependency
            {
                Name = reader.GetString(0),
                Type = typeName,
                Schema = reader.GetString(2),
                Usage = usage
            });
        }

        return dependencies;
    }

    private List<PlsqlDependency> ExtractDependenciesFromBody(string schemaName, string body)
    {
        var dependencies = new List<PlsqlDependency>();
        var lines = body.Split('\n');

        foreach (var line in lines)
        {
            if (Regex.IsMatch(line, @"\bSELECT\b", RegexOptions.IgnoreCase))
                dependencies.Add(new PlsqlDependency { Name = ExtractObjectName(line), Type = "TABLE/VIEW", Schema = schemaName.ToUpper(), Usage = "read" });
            if (Regex.IsMatch(line, @"\bINSERT\b|\bUPDATE\b|\bDELETE\b", RegexOptions.IgnoreCase))
                dependencies.Add(new PlsqlDependency { Name = ExtractObjectName(line), Type = "TABLE", Schema = schemaName.ToUpper(), Usage = "write" });
            if (Regex.IsMatch(line, @"\bEXECUTE\b|\bCALL\b| := ", RegexOptions.IgnoreCase))
                dependencies.Add(new PlsqlDependency { Name = ExtractObjectName(line), Type = "PROCEDURE/FUNCTION", Schema = schemaName.ToUpper(), Usage = "call" });
        }

        return dependencies;
    }

    private string ExtractObjectName(string line)
    {
        var match = Regex.Match(line, @"\bFROM\b\s+(\w+)|\bINTO\b\s+(\w+)|\bUPDATE\b\s+(\w+)|\bINTO\b\s+(\w+)", RegexOptions.IgnoreCase);
        for (int i = 1; i < match.Groups.Count; i++)
        {
            if (match.Groups[i].Success)
                return match.Groups[i].Value;
        }
        return "UNKNOWN";
    }

    private string InferUsageFromType(string type)
    {
        return type switch
        {
            "TABLE" => "read/write",
            "VIEW" => "read",
            "FUNCTION" => "call",
            "PROCEDURE" => "call",
            _ => "reference"
        };
    }

}