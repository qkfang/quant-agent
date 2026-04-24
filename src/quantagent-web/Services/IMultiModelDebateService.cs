using quantagent_web.Models;

namespace quantagent_web.Services;

/// <summary>
/// Service interface for orchestrating multi-model debates where different
/// analyst perspectives reason with each other about market scenarios.
/// </summary>
public interface IMultiModelDebateService
{
    /// <summary>
    /// Runs a multi-model debate and streams messages as they are generated.
    /// </summary>
    IAsyncEnumerable<DebateMessage> RunDebateAsync(string query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the available analyst perspectives.
    /// </summary>
    IReadOnlyList<string> GetAvailableAgents();
}
