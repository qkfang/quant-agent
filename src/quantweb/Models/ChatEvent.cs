namespace quantweb.Models;

public class ChatEvent
{
    public string Type { get; set; } = string.Empty;
    public string AgentName { get; set; } = string.Empty;
    public string Specialty { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Delta { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public string AvatarEmoji => AgentName switch
    {
        "Pricing Quant" => "💰",
        "Risk Quant" => "🛡️",
        "Alpha Quant" => "📈",
        _ => "🤖"
    };

    public string LaneColor => AgentName switch
    {
        "Pricing Quant" => "#3b82f6",
        "Risk Quant" => "#ef4444",
        "Alpha Quant" => "#22c55e",
        _ => "#94a3b8"
    };
}
