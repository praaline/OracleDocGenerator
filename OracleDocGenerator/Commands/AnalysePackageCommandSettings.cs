using Oracle.ManagedDataAccess.Client;
using Spectre.Console.Cli;

public class AnalysePackageCommandSettings : BaseCommandSettings
{
    [CommandOption("--package <PACKAGE>")]
    public string? PackageName { get; set; }

    [CommandOption("--use-chatgpt")]
    public bool? UseChatGpt { get; set; }

    public new (ConnectionConfig Connection, string PackageName, bool UseChatGpt) MergeAndPrompt()
    {
        var conn = base.MergeAndPrompt();

        if (string.IsNullOrWhiteSpace(PackageName))
        {
            using var oraConn = new OracleConnection(conn.BuildConnectionString());
            oraConn.Open();
            var selector = new PackageSelector(oraConn, Schema ?? string.Empty);
            PackageName = selector.PromptForPackage();
        }

        bool useChatGpt = PromptHelper.Confirm("Use ChatGPT for analysis?", UseChatGpt);
        return (conn, PackageName, useChatGpt);
    }
}