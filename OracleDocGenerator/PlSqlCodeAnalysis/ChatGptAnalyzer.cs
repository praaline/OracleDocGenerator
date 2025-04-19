using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

public class ChatGptAnalyzer
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "";
    private const string Endpoint = "https://api.openai.com/v1/chat/completions";

    public ChatGptAnalyzer()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
    }

    public string SummarizeProcedure(string code, string name)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            return "[OpenAI API key not set]";

        var prompt = $"""
        Analyze the following PL/SQL code. Provide a concise English summary of what it does. The code may contain both English and French comments or identifiers.
        Also infer and describe the meaning of the input and output parameters, in a Markdown table format.

        Procedure/Function name: {name}

        Code:
        {code}
        """;

        var payload = new
        {
            model = "gpt-4",
            messages = new[]
            {
                new { role = "system", content = "You are an expert Oracle PL/SQL documentation assistant." },
                new { role = "user", content = prompt }
            },
            temperature = 0.3
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var response = _httpClient.PostAsync(Endpoint, content).Result;
        if (!response.IsSuccessStatusCode)
            return $"[ChatGPT request failed: {response.StatusCode}]";

        using var stream = response.Content.ReadAsStream();
        using var doc = JsonDocument.Parse(stream);
        var reply = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return reply ?? "[No response]";
    }
}
