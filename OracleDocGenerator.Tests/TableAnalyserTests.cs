using Oracle.ManagedDataAccess.Client;
using Xunit.Abstractions;

namespace OracleDocGenerator.Tests;

public class TableAnalyserTests
{
    private readonly ITestOutputHelper _output;
    private const string ConnectionString = "User Id=HR;Password=hr;Data Source=DWIN10/XEPDB1;";

    public TableAnalyserTests(ITestOutputHelper output)
    {
        _output = output;

    }
    private TableAnalyser CreateAnalyser()
    {
        var logger = new TestLogger(_output);
        return new TableAnalyser(ConnectionString, logger);
    }

    [Fact]
    public void TableAnalyser_HRSchema_ShouldContainExpectedTablesAndColumns()
    {
        var analyser = CreateAnalyser();
        var tables = analyser.AnalyzeTables("HR");

        // ✅ Check expected tables exist
        Assert.Contains(tables, t => t.Name == "EMPLOYEES");
        Assert.Contains(tables, t => t.Name == "DEPARTMENTS");
        Assert.Contains(tables, t => t.Name == "JOBS");

        // ✅ Check a specific table has expected columns
        var employees = tables.First(t => t.Name == "EMPLOYEES");
        Assert.True(employees.Columns.Count >= 10);

        // ✅ Check PK and FK flags
        var empIdColumn = employees.Columns.FirstOrDefault(c => c.Name == "EMPLOYEE_ID");
        Assert.NotNull(empIdColumn);
        Assert.True(empIdColumn!.IsPrimaryKey);

        var deptIdColumn = employees.Columns.FirstOrDefault(c => c.Name == "DEPARTMENT_ID");
        Assert.True(deptIdColumn!.IsForeignKey);
    }

    [Fact]
    public void TableAnalyser_ExtractsTableWithColumns()
    {
        var analyser = CreateAnalyser();
        var tables = analyser.AnalyzeTables("HR");

        var employees = tables.FirstOrDefault(t => t.Name == "EMPLOYEES");
        Assert.NotNull(employees);
        Assert.NotEmpty(employees.Columns);

        var firstNameCol = employees.Columns.FirstOrDefault(c => c.Name == "FIRST_NAME");
        Assert.NotNull(firstNameCol);
        Assert.Equal("VARCHAR2", firstNameCol.DataType);
    }

    [Fact]
    public void TableAnalyser_SamplesDistinctColumnValues()
    {
        var analyser = CreateAnalyser();
        var tables = analyser.AnalyzeTables("HR");

        var employees = tables.FirstOrDefault(t => t.Name == "EMPLOYEES");
        Assert.NotNull(employees);
        var jobIdCol = employees.Columns.FirstOrDefault(c => c.Name == "JOB_ID");

        Assert.NotNull(jobIdCol);
        Assert.NotNull(jobIdCol.SampleDistinctValues);
        Assert.InRange(jobIdCol.DistinctValueCount ?? 0, 1, 20);
    }

    [Fact]
    public void TableAnalyser_DetectsPrimaryKeyConstraint()
    {
        var analyser = CreateAnalyser();
        var tables = analyser.AnalyzeTables("HR");

        var jobs = tables.FirstOrDefault(t => t.Name == "JOBS");
        Assert.NotNull(jobs);

        var pk = jobs.Constraints.FirstOrDefault(c => c.Type == "PRIMARY KEY");
        Assert.NotNull(pk);
        Assert.Contains("JOB_ID", pk.Columns);
    }

    [Fact]
    public void TableAnalyser_HandlesMissingComments()
    {
        var analyser = CreateAnalyser();
        var tables = analyser.AnalyzeTables("HR");

        var countries = tables.FirstOrDefault(t => t.Name == "COUNTRIES");
        Assert.NotNull(countries);

        foreach (var col in countries.Columns)
        {
            Assert.True(col.Comment == null || col.Comment is string);
        }
    }
}
