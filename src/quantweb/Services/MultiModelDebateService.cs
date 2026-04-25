using System.Runtime.CompilerServices;
using System.Text.Json;
using quantagent_web.Models;

namespace quantagent_web.Services;

public class MultiModelDebateService : IMultiModelDebateService
{
    private static readonly IReadOnlyList<string> AgentNames = ["Pricing Quant", "Risk Quant", "Alpha Quant"];
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;
    private readonly ILogger<MultiModelDebateService> _logger;

    public MultiModelDebateService(HttpClient httpClient, ILogger<MultiModelDebateService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public IReadOnlyList<string> GetAvailableAgents() => AgentNames;

    public async IAsyncEnumerable<DebateMessage> RunDebateAsync(
        string query,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "research")
        {
            Content = JsonContent.Create(new { topic = query })
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
            if (data == "[DONE]") yield break;

            ResearchEvent? evt = null;
            try
            {
                evt = JsonSerializer.Deserialize<ResearchEvent>(data, JsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse SSE event: {Data}", data);
            }

            if (evt is not null && !string.IsNullOrEmpty(evt.Message))
            {
                yield return new DebateMessage
                {
                    AgentName = evt.AgentName,
                    Content = evt.Message,
                    Timestamp = evt.Timestamp
                };
            }
        }
    }
}
