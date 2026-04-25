namespace QuantLib.Agents.Quants;

public enum AgentEventType
{
    RoundStarted,
    AgentStarted,
    AgentCompleted,
    OrchestratorSummary,
    ConsensusReached,
    MaxRoundsReached,
    FinalReportStarted,
    FinalReportCompleted,
    Error
}

public record AgentEvent(
    AgentEventType Type,
    int Round,
    string AgentName,
    string Specialty,
    string Message,
    DateTime Timestamp,
    string InputMessage = ""
)
{
    public static AgentEvent RoundStarted(int round)
        => new(AgentEventType.RoundStarted, round, "", "", "", DateTime.UtcNow);

    public static AgentEvent Started(int round, string agentName, string specialty, string inputMessage = "")
        => new(AgentEventType.AgentStarted, round, agentName, specialty, "", DateTime.UtcNow, inputMessage);

    public static AgentEvent Completed(int round, string agentName, string specialty, string message)
        => new(AgentEventType.AgentCompleted, round, agentName, specialty, message, DateTime.UtcNow);

    public static AgentEvent Summary(int round, string message)
        => new(AgentEventType.OrchestratorSummary, round, "Orchestrator", "", message, DateTime.UtcNow);

    public static AgentEvent Consensus(int round)
        => new(AgentEventType.ConsensusReached, round, "Orchestrator", "", "All agents have reached consensus.", DateTime.UtcNow);

    public static AgentEvent MaxRounds()
        => new(AgentEventType.MaxRoundsReached, 0, "Orchestrator", "", "Maximum rounds reached.", DateTime.UtcNow);

    public static AgentEvent FinalStarted()
        => new(AgentEventType.FinalReportStarted, 0, "Orchestrator", "", "", DateTime.UtcNow);

    public static AgentEvent FinalCompleted(string message)
        => new(AgentEventType.FinalReportCompleted, 0, "Orchestrator", "", message, DateTime.UtcNow);

    public static AgentEvent ErrorEvent(int round, string message)
        => new(AgentEventType.Error, round, "", "", message, DateTime.UtcNow);
}
