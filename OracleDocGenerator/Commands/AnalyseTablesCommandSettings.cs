using Spectre.Console.Cli;
using System.ComponentModel;

public class AnalyseTablesCommandSettings : BaseCommandSettings
{
    [Description("If set, use ChatGPT to summarize the table structure.")]
    [CommandOption("--use-chatgpt")]
    public bool UseChatGpt { get; set; }

    [Description("Optional: Filter by a specific table name (partial match supported).")]
    [CommandOption("--filter")]
    public string? TableNameFilter { get; set; }

    public new (ConnectionConfig Connection, bool? UseChatGpt, string? TableName) MergeAndPrompt()
    {
        var conn = base.MergeAndPrompt();
        return (conn, UseChatGpt, TableNameFilter);
    }
}