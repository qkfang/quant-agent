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
        "Bull Analyst" => "agent-bull",
        "Bear Analyst" => "agent-bear",
        "Risk Analyst" => "agent-risk",
        "Macro Strategist" => "agent-macro",
        "Moderator" => "agent-moderator",
        _ => "agent-default"
    };

    public string AvatarEmoji => AgentName switch
    {
        "Bull Analyst" => "🐂",
        "Bear Analyst" => "🐻",
        "Risk Analyst" => "🛡️",
        "Macro Strategist" => "🌍",
        "Moderator" => "⚖️",
        _ => "🤖"
    };
}
