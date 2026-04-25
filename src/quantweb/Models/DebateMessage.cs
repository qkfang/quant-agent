namespace quantagent_web.Models;

/// <summary>
/// Represents a single message in a multi-model debate.
/// </summary>
public class DebateMessage
{
    public string AgentName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
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
