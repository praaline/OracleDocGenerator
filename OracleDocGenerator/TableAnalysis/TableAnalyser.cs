using Oracle.ManagedDataAccess.Client;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using System.Collections.Concurrent;

public class TableAnalyser
{
    private readonly string _connectionString;
    private readonly ILogger _logger;

    public TableAnalyser(string connectionString, ILogger logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public List<TableInfo> AnalyzeTables(string schemaName, int maxParallelism = 4)
    {
        var tableInfos = GetTableNamesAndComments(schemaName);
        var results = new ConcurrentBag<TableInfo>();
        var options = new ParallelOptions { MaxDegreeOfParallelism = maxParallelism };

        Parallel.ForEach(tableInfos, options, partialTable =>
        {
            try
            {
                lock (_logger)
                    _logger.LogDebug($"Analyzing table: {partialTable.Name}");

                var fullTable = AnalyzeSingleTable(schemaName, partialTable.Name);
                fullTable.Comment = partialTable.Comment;
                results.Add(fullTable);

                lock (_logger)
                    _logger.LogInformation($"Analyzed table: {partialTable.Name}");
            }
            catch (Exception ex)
            {
                lock (_logger)
                    _logger.LogError($"Failed to analyze table {partialTable.Name}: {ex.Message}");
            }
        });

        return results.OrderBy(t => t.Name).ToList();
    }

    public TableInfo AnalyzeSingleTable(string schemaName, string tableName)
    {
        var columns = GetColumns(schemaName, tableName);
        var constraints = GetConstraints(schemaName, tableName);
        var indexes = GetIndexes(schemaName, tableName);

        return new TableInfo
        {
            Name = tableName,
            Schema = schemaName,
            Columns = columns,
            Constraints = constraints,
            Indexes = indexes
        };
    }

    private List<TableInfo> GetTableNamesAndComments(string schema)
    {
        var tables = new List<TableInfo>();

        using var connection = new OracleConnection(_connectionString);
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT table_name, comments
            FROM all_tab_comments
            WHERE owner = :owner AND table_type = 'TABLE'";
        cmd.Parameters.Add(new OracleParameter("owner", schema.ToUpper()));

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            tables.Add(new TableInfo
            {
                Name = reader.GetString(0),
                Schema = schema.ToUpper(),
                Comment = reader.IsDBNull(1) ? null : reader.GetString(1)
            });
        }

        return tables;
    }

    private List<ColumnInfo> GetColumns(string schema, string tableName)
    {
        var columns = new List<ColumnInfo>();

        using var connection = new OracleConnection(_connectionString);
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT col.column_name, col.data_type, col.data_length, col.data_precision, col.data_scale,
                   col.nullable, col.data_default,
                   com.comments
            FROM all_tab_columns col
            LEFT JOIN all_col_comments com
              ON com.owner = col.owner
             AND com.table_name = col.table_name
             AND com.column_name = col.column_name
            WHERE col.owner = :owner AND col.table_name = :table_name
            ORDER BY col.column_id";

        cmd.Parameters.Add(new OracleParameter("owner", schema.ToUpper()));
        cmd.Parameters.Add(new OracleParameter("table_name", tableName.ToUpper()));

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var column = new ColumnInfo
            {
                Name = reader.GetString(0),
                DataType = reader.GetString(1),
                DataLength = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                DataPrecision = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                DataScale = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                IsNullable = reader.GetString(5) == "Y",
                DefaultValue = reader.IsDBNull(6) ? null : reader.GetString(6),
                Comment = reader.IsDBNull(7) ? null : reader.GetString(7),
            };
            columns.Add(column);
        }

        MarkPrimaryAndForeignKeys(schema, tableName, columns);

        // Distinct value analysis for each column
        foreach (var column in columns)
        {
            AnalyzeColumnValues(schema, tableName, column);
        }

        return columns;
    }

    private void MarkPrimaryAndForeignKeys(string schema, string tableName, List<ColumnInfo> columns)
    {
        var primaryKeys = new HashSet<string>();
        var foreignKeys = new Dictionary<string, string>();

        using var connection = new OracleConnection(_connectionString);
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT acc.column_name,
                   ac.constraint_type,
                   r.table_name AS referenced_table
            FROM all_constraints ac
            JOIN all_cons_columns acc
              ON ac.owner = acc.owner AND ac.constraint_name = acc.constraint_name
            LEFT JOIN all_constraints r
              ON ac.r_owner = r.owner AND ac.r_constraint_name = r.constraint_name
            WHERE ac.owner = :owner AND ac.table_name = :table_name";

        cmd.Parameters.Add(new OracleParameter("owner", schema.ToUpper()));
        cmd.Parameters.Add(new OracleParameter("table_name", tableName.ToUpper()));

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var column = reader.GetString(0);
            var type = reader.GetString(1);
            if (type == "P") primaryKeys.Add(column);
            if (type == "R") foreignKeys[column] = reader.IsDBNull(2) ? "" : reader.GetString(2);
        }

        foreach (var col in columns)
        {
            col.IsPrimaryKey = primaryKeys.Contains(col.Name);
            if (foreignKeys.TryGetValue(col.Name, out var refTable))
            {
                col.IsForeignKey = true;
                col.ForeignKeyReference = refTable;
            }
        }
    }

    private List<ConstraintInfo> GetConstraints(string schema, string tableName)
    {
        var constraints = new List<ConstraintInfo>();

        using var connection = new OracleConnection(_connectionString);
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT c.constraint_name, c.constraint_type, cc.column_name,
                   r.table_name AS referenced_table
            FROM all_constraints c
            JOIN all_cons_columns cc
              ON c.owner = cc.owner AND c.constraint_name = cc.constraint_name
            LEFT JOIN all_constraints r
              ON c.r_owner = r.owner AND c.r_constraint_name = r.constraint_name
            WHERE c.owner = :owner AND c.table_name = :table_name
            ORDER BY c.constraint_name, cc.position";

        cmd.Parameters.Add(new OracleParameter("owner", schema.ToUpper()));
        cmd.Parameters.Add(new OracleParameter("table_name", tableName.ToUpper()));

        using var reader = cmd.ExecuteReader();
        var map = new Dictionary<string, ConstraintInfo>();

        while (reader.Read())
        {
            var name = reader.GetString(0);
            var typeCode = reader.GetString(1);
            var type = typeCode switch
            {
                "P" => "PRIMARY KEY",
                "U" => "UNIQUE",
                "R" => "FOREIGN KEY",
                _ => "OTHER"
            };

            if (!map.TryGetValue(name, out var constraint))
            {
                constraint = new ConstraintInfo
                {
                    Name = name,
                    Type = type,
                    Columns = new List<string>(),
                    ReferenceTable = reader.IsDBNull(3) ? null : reader.GetString(3)
                };
                map[name] = constraint;
                constraints.Add(constraint);
            }

            constraint.Columns.Add(reader.GetString(2));
        }

        return constraints;
    }

    private List<IndexInfo> GetIndexes(string schema, string tableName)
    {
        var indexes = new Dictionary<string, IndexInfo>();

        using var connection = new OracleConnection(_connectionString);
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT i.index_name, i.uniqueness, ic.column_name
            FROM all_indexes i
            JOIN all_ind_columns ic
              ON i.index_name = ic.index_name AND i.owner = ic.index_owner
            WHERE i.owner = :owner AND i.table_name = :table_name
            ORDER BY i.index_name, ic.column_position";

        cmd.Parameters.Add(new OracleParameter("owner", schema.ToUpper()));
        cmd.Parameters.Add(new OracleParameter("table_name", tableName.ToUpper()));

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var name = reader.GetString(0);
            var isUnique = reader.GetString(1) == "UNIQUE";
            var column = reader.GetString(2);

            if (!indexes.TryGetValue(name, out var index))
            {
                index = new IndexInfo
                {
                    Name = name,
                    IsUnique = isUnique,
                    Columns = new List<string>()
                };
                indexes[name] = index;
            }

            index.Columns.Add(column);
        }

        return indexes.Values.ToList();
    }

    private void AnalyzeColumnValues(string schema, string tableName, ColumnInfo column)
    {
        try
        {
            using var connection = new OracleConnection(_connectionString);
            connection.Open();

            using var countCmd = connection.CreateCommand();
            countCmd.CommandText = $@"
                SELECT COUNT(DISTINCT {QuoteIdentifier(column.Name)})
                FROM {QuoteIdentifier(schema)}.{QuoteIdentifier(tableName)}";
            column.DistinctValueCount = Convert.ToInt32(countCmd.ExecuteScalar());

            if (column.DistinctValueCount < 20)
            {
                using var valueCmd = connection.CreateCommand();
                valueCmd.CommandText = $@"
                    SELECT DISTINCT {QuoteIdentifier(column.Name)}
                    FROM {QuoteIdentifier(schema)}.{QuoteIdentifier(tableName)}
                    WHERE {QuoteIdentifier(column.Name)} IS NOT NULL
                    FETCH FIRST 20 ROWS ONLY";

                using var reader = valueCmd.ExecuteReader();
                column.SampleDistinctValues = new List<string>();
                while (reader.Read())
                {
                    column.SampleDistinctValues.Add(reader[0].ToString() ?? "");
                }
            }

            column.DistinctValuesSampled = true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error analyzing distinct values for {schema}.{tableName}.{column.Name}: {ex.Message}");
        }
    }
    
    // Helper function to quote Oracle identifiers correctly
    private string QuoteIdentifier(string identifier)
    {
        return "\"" + identifier.Replace("\"", "\"\"") + "\"";
    }

}
