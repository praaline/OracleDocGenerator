using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;

public class PackageSourceCache
{
    private readonly OracleConnection _connection;
    private readonly ILogger _logger;
    private readonly Dictionary<string, List<string>> _sourceCache = new();
    private readonly Dictionary<string, List<(string Name, int Line)>> _identifierCache = new();

    public PackageSourceCache(OracleConnection connection, ILogger logger)
    {
        _connection = connection;
        _logger = logger;
    }

    public string ExtractRoutineBody(string schemaName, string packageName, string routineName)
    {
        var cacheKey = $"{schemaName}.{packageName}";
        _logger.LogDebug($"Extracting routine body from package using all_identifiers: {cacheKey}, routine: {routineName}...");

        if (!_sourceCache.TryGetValue(cacheKey, out var sourceLines))
        {
            var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                SELECT text FROM all_source
                WHERE owner = :owner AND name = :name AND type = 'PACKAGE BODY'
                ORDER BY line";
            cmd.Parameters.Add(new OracleParameter("owner", schemaName.ToUpper()));
            cmd.Parameters.Add(new OracleParameter("name", packageName));

            sourceLines = new List<string>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                sourceLines.Add(reader.GetString(0));

            _sourceCache[cacheKey] = sourceLines;
        }

        if (!_identifierCache.TryGetValue(cacheKey, out var identifiers))
        {
            var identCmd = _connection.CreateCommand();
            identCmd.CommandText = @"
                SELECT name, line
                FROM all_identifiers
                WHERE owner = :owner
                  AND object_name = :package
                  AND object_type = 'PACKAGE BODY'
                  AND type IN ('FUNCTION', 'PROCEDURE')
                  AND usage = 'DEFINITION'
                ORDER BY line";

            identCmd.Parameters.Add(new OracleParameter("owner", schemaName.ToUpper()));
            identCmd.Parameters.Add(new OracleParameter("package", packageName));

            identifiers = new List<(string, int)>();
            using var reader = identCmd.ExecuteReader();
            while (reader.Read())
                identifiers.Add((reader.GetString(0), reader.GetInt32(1)));

            _identifierCache[cacheKey] = identifiers;
        }

        var matchingDefs = identifiers
            .Where(x => x.Name.Equals(routineName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Line)
            .ToList();

        if (!matchingDefs.Any())
            return string.Empty;

        var otherDefs = identifiers
            .Where(x => !x.Name.Equals(routineName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Line)
            .ToList();

        var extractedBodies = new List<string>();

        foreach (var def in matchingDefs)
        {
            int startLine = def.Line;
            int endLine = sourceLines.Count;

            var next = otherDefs.FirstOrDefault(x => x.Line > startLine);
            if (next != default)
                endLine = next.Line - 1;

            var lines = sourceLines.Skip(startLine - 1).Take(endLine - startLine + 1);
            extractedBodies.Add(string.Join("\n", lines));
        }

        return string.Join("\n\n-- overload --\n\n", extractedBodies);
    }
}

