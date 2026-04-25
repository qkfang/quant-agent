using System.Runtime.CompilerServices;
using System.Text.Json;
using quantweb.Models;

namespace quantweb.Services;

public interface IResearchService
{
    IAsyncEnumerable<ResearchEvent> StreamResearchAsync(
        string topic,
        CancellationToken cancellationToken = default);
}

public class ResearchService : IResearchService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ResearchService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ResearchService(HttpClient httpClient, ILogger<ResearchService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async IAsyncEnumerable<ResearchEvent> StreamResearchAsync(
        string topic,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "research")
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
                _logger.LogInformation("Research stream completed");
                yield break;
            }

            ResearchEvent? evt = null;
            try
            {
                evt = JsonSerializer.Deserialize<ResearchEvent>(data, JsonOptions);
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
