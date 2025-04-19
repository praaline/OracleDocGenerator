using Spectre.Console.Cli;
using System.ComponentModel;

public class BaseCommandSettings : CommandSettings
{
    [CommandOption("--host")]
    [Description("Database host")]
    public string? Host { get; set; }

    [CommandOption("--port")]
    [Description("Database port")]
    [DefaultValue(1521)]
    public int? Port { get; set; } = 1521;

    [CommandOption("--service")]
    [Description("Database service name")]
    public string? Service { get; set; }

    [CommandOption("--username")]
    [Description("Database connection user")]
    public string? Username { get; set; }

    [CommandOption("--password")]
    [Description("Database connection password")]
    public string? Password { get; set; } 

    [CommandOption("--schema")]
    [Description("Database schema to analyse")]
    public string? Schema { get; set; }

    [CommandOption("--config")]
    [Description("Configuration file")]
    public string? ConfigFile { get; set; }

    public virtual ConnectionConfig MergeAndPrompt()
    {
        var config = ConfigLoader.Load(this.ConfigFile);

        string host = PromptHelper.AskIfMissing(this.Host ?? config?.Host, "Enter database host:");
        int port = PromptHelper.AskIfMissing(this.Port ?? config?.Port, "Enter database port:");
        string service = PromptHelper.AskIfMissing(this.Service ?? config?.Service, "Enter database service:");
        string username = PromptHelper.AskIfMissing(this.Username ?? config?.Username, "Enter username:");
        string password = PromptHelper.AskIfMissing(this.Password ?? config?.Password, "Enter password:", isSecret: true);
        string schema = PromptHelper.AskIfMissing(this.Schema ?? config?.Schema, "Enter schema:");

        return (new ConnectionConfig(host, port, service, username, password, schema));
    }
}
