using System.Runtime.CompilerServices;
using System.Text.Json;
using quantweb.Models;

namespace quantweb.Services;

public interface IChatService
{
    IAsyncEnumerable<ChatEvent> StreamChatAsync(
        string agent,
        string message,
        List<ChatMessageItem>? history = null,
        CancellationToken cancellationToken = default);
}

public class ChatMessageItem
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class ChatService : IChatService
{
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ChatService(HttpClient httpClient, ILogger<ChatService> logger)
    {
        _httpClient = httpClient;
    }

    public async IAsyncEnumerable<ChatEvent> StreamChatAsync(
        string agent,
        string message,
        List<ChatMessageItem>? history = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "chat")
        {
            Content = JsonContent.Create(new { agent, message, history })
        };
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null) break;
            if (string.IsNullOrEmpty(line)) continue;

            if (!line.StartsWith("data: ")) continue;
            var data = line["data: ".Length..];

            if (data == "[DONE]")
            {
                yield break;
            }

            ChatEvent? evt = null;
            try
            {
                evt = JsonSerializer.Deserialize<ChatEvent>(data, JsonOptions);
            }
            catch (JsonException)
            {
            }

            if (evt is not null)
            {
                yield return evt;
            }
        }
    }
}
