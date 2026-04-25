using System.Runtime.CompilerServices;
using System.Text.Json;
using quantweb.Models;

namespace quantweb.Services;

public interface ITurnService
{
    IAsyncEnumerable<TurnEvent> StreamTurnAsync(
        string topic,
        CancellationToken cancellationToken = default);
}

public class TurnService : ITurnService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TurnService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public TurnService(HttpClient httpClient, ILogger<TurnService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async IAsyncEnumerable<TurnEvent> StreamTurnAsync(
        string topic,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "turn")
        {
            Content = JsonContent.Create(new { topic })
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
                _logger.LogInformation("Turn stream completed");
                yield break;
            }

            TurnEvent? evt = null;
            try
            {
                evt = JsonSerializer.Deserialize<TurnEvent>(data, JsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse SSE event: {Data}", data);
            }

            if (evt is not null)
            {
                yield return evt;
            }
        }
    }
}
