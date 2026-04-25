namespace QuantLib.Agents.Compare;

public enum CompareEventType
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

public record CompareEvent(
    CompareEventType Type,
    int Round,
    string AgentName,
    string Specialty,
    string Message,
    DateTime Timestamp,
    string InputMessage = ""
)
{
    public static CompareEvent RoundStarted(int round)
        => new(CompareEventType.RoundStarted, round, "", "", "", DateTime.UtcNow);

    public static CompareEvent Started(int round, string agentName, string specialty, string inputMessage = "")
        => new(CompareEventType.AgentStarted, round, agentName, specialty, "", DateTime.UtcNow, inputMessage);

    public static CompareEvent Completed(int round, string agentName, string specialty, string message)
        => new(CompareEventType.AgentCompleted, round, agentName, specialty, message, DateTime.UtcNow);

    public static CompareEvent Summary(int round, string message)
        => new(CompareEventType.OrchestratorSummary, round, "Orchestrator", "", message, DateTime.UtcNow);

    public static CompareEvent Consensus(int round)
        => new(CompareEventType.ConsensusReached, round, "Orchestrator", "", "All models have reached consensus.", DateTime.UtcNow);

    public static CompareEvent MaxRounds()
        => new(CompareEventType.MaxRoundsReached, 0, "Orchestrator", "", "Maximum rounds reached.", DateTime.UtcNow);

    public static CompareEvent FinalStarted()
        => new(CompareEventType.FinalReportStarted, 0, "Orchestrator", "", "", DateTime.UtcNow);

    public static CompareEvent FinalCompleted(string message)
        => new(CompareEventType.FinalReportCompleted, 0, "Orchestrator", "", message, DateTime.UtcNow);

    public static CompareEvent ErrorEvent(int round, string message)
        => new(CompareEventType.Error, round, "", "", message, DateTime.UtcNow);
}
