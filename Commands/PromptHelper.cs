using Spectre.Console;

public static class PromptHelper
{
    public static string AskIfMissing(string? value, string prompt, bool isSecret = false)
    {
        if (!string.IsNullOrWhiteSpace(value))
            return value;

        var textPrompt = new TextPrompt<string>(prompt);
        if (isSecret)
            textPrompt.Secret();

        return AnsiConsole.Prompt(textPrompt);
    }

    public static int AskIfMissing(int? value, string prompt)
    {
        return value ?? AnsiConsole.Ask<int>(prompt);
    }

    public static bool Confirm(string prompt, bool? current)
    {
        return current ?? AnsiConsole.Confirm(prompt);
    }
}