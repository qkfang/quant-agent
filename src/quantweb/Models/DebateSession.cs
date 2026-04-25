namespace quantagent_web.Models;

/// <summary>
/// Represents a complete multi-model debate session.
/// </summary>
public class DebateSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Query { get; set; } = string.Empty;
    public List<DebateMessage> Messages { get; set; } = new();
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public bool IsComplete { get; set; }
    public string? Summary { get; set; }
}
