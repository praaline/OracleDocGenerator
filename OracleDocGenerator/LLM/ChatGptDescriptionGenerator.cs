using Microsoft.Extensions.Logging;
using System.Text;

public class ChatGptDescriptionGenerator : IDescriptionGenerator
{
    private readonly ILogger _logger;

    public ChatGptDescriptionGenerator(ILogger logger)
    {
        _logger = logger;
    }

    public async Task GenerateMissingColumnDescriptionsAsync(TableInfo table)
    {
        var prompt = BuildPrompt(table);

        // Placeholder for future API call
        _logger.LogInformation($"[GPT PROMPT START]\n{prompt}\n[GPT PROMPT END]");

        // TODO: Call ChatGPT and update table.Columns[x].Comment
        await Task.CompletedTask;
    }

    private string BuildPrompt(TableInfo table)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"You are analyzing the structure of a database table named `{table.Name}`.");
        sb.AppendLine("Here are its columns:");

        foreach (var column in table.Columns)
        {
            var description = string.IsNullOrWhiteSpace(column.Comment)
                ? "[no description]"
                : column.Comment;

            sb.AppendLine($"- `{column.Name}` ({column.DataType}): {description}");
        }

        sb.AppendLine();
        sb.AppendLine("Please suggest short, human-readable descriptions for the columns with [no description].");

        return sb.ToString();
    }
}
