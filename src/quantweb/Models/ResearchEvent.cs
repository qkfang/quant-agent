namespace quantweb.Models;

public class SearchCitation
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

public class ResearchEvent
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
        "Pricing Quant" => "agent-pricing",
        "Risk Quant" => "agent-risk",
        "Alpha Quant" => "agent-alpha",
        "Orchestrator" => "agent-orchestrator",
        _ => "agent-default"
    };

    public string AvatarEmoji => AgentName switch
    {
        "Pricing Quant" => "💰",
        "Risk Quant" => "🛡️",
        "Alpha Quant" => "📈",
        "Orchestrator" => "🎯",
        _ => "🤖"
    };

    public string LaneColor => AgentName switch
    {
        "Pricing Quant" => "#3b82f6",
        "Risk Quant" => "#ef4444",
        "Alpha Quant" => "#22c55e",
        "Orchestrator" => "#f59e0b",
        _ => "#94a3b8"
    };
}
