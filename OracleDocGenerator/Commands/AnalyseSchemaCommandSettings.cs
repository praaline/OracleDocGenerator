using Spectre.Console.Cli;

public class AnalyseSchemaCommandSettings : BaseCommandSettings
{
    [CommandOption("--use-chatgpt")]
    public bool? UseChatGpt { get; set; }

    public new (ConnectionConfig Connection, bool UseChatGpt) MergeAndPrompt()
    {
        var conn = base.MergeAndPrompt();
        bool useChatGpt = PromptHelper.Confirm("Use ChatGPT for analysis?", UseChatGpt);
        return (conn, useChatGpt);
    }
}