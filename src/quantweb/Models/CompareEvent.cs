namespace quantweb.Models;

public class CompareEvent
{
    public string Type { get; set; } = string.Empty;
    public int Round { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public string Specialty { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string InputMessage { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public List<SearchCitation>? Citations { get; set; }

    public string CssClass => AgentName switch
    {
        "gpt-4o" => "agent-gpt4o",
        "gpt-4.1" => "agent-gpt41",
        "gpt-5.2" => "agent-gpt52",
        "Orchestrator" => "agent-orchestrator",
        _ => "agent-default"
    };

    public string AvatarEmoji => AgentName switch
    {
        "gpt-4o" => "🔵",
        "gpt-4.1" => "🟣",
        "gpt-5.2" => "🟢",
        "Orchestrator" => "🎯",
        _ => "🤖"
    };

    public string LaneColor => AgentName switch
    {
        "gpt-4o" => "#3b82f6",
        "gpt-4.1" => "#8b5cf6",
        "gpt-5.2" => "#22c55e",
        "Orchestrator" => "#f59e0b",
        _ => "#94a3b8"
    };
}
