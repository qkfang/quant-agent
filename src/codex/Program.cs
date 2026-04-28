using System.Net.Http.Headers;
using System.Text.Json;

// Azure OpenAI Responses API (v1) - simple Codex example.
// NOTE: do not commit real keys; prefer environment variables.
string url = "https://aigwdfang.azure-api.net/fsi-foundry/api/projects/fsi-project/openai/v1/responses";
string apiKey = "";
string model = "gpt-5.3-codex-1";

Console.Write("Prompt: ");
string prompt = Console.ReadLine() ?? "Write a C# function that reverses a string.";

using HttpClient http = new();
http.DefaultRequestHeaders.Add("api-key", apiKey);

var payload = new
{
    model,
    input = prompt,
    reasoning = new { effort = "medium" }
};

using HttpRequestMessage request = new(HttpMethod.Post, url)
{
    Content = new StringContent(JsonSerializer.Serialize(payload))
    {
        Headers = { ContentType = new MediaTypeHeaderValue("application/json") }
    }
};

using HttpResponseMessage response = await http.SendAsync(request);
string body = await response.Content.ReadAsStringAsync();
response.EnsureSuccessStatusCode();

using JsonDocument doc = JsonDocument.Parse(body);
Console.WriteLine();

if (doc.RootElement.TryGetProperty("output_text", out JsonElement outputText) &&
    outputText.ValueKind == JsonValueKind.String)
{
    Console.WriteLine(outputText.GetString());
    return;
}

foreach (JsonElement item in doc.RootElement.GetProperty("output").EnumerateArray())
{
    if (!item.TryGetProperty("content", out JsonElement content)) continue;
    foreach (JsonElement part in content.EnumerateArray())
    {
        if (part.TryGetProperty("text", out JsonElement text) && text.ValueKind == JsonValueKind.String)
        {
            Console.WriteLine(text.GetString());
        }
    }
}

