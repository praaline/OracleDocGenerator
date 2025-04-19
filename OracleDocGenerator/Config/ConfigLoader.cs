using System.Text.Json;

public class ConfigLoader
{
    public static ConnectionConfig? Load(string? configFile)
    {
        if (string.IsNullOrWhiteSpace(configFile) || !File.Exists(configFile))
            return null;

        var json = File.ReadAllText(configFile);
        return JsonSerializer.Deserialize<ConnectionConfig>(json);
    }
}