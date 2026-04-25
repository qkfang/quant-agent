namespace quantweb.Models;

public class TurnEvent
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
}
