using QuantLib.Agents;

namespace QuantLib.Agents.Compare;

public enum CompareEventType
{
    RoundStarted,
    AgentStarted,
    AgentDelta,
    AgentCompleted,
    OrchestratorDelta,
    OrchestratorSummary,
    FinalReportDelta,
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
    string InputMessage = "",
    IReadOnlyList<SearchCitation>? Citations = null
)
{
    public static CompareEvent RoundStarted(int round)
        => new(CompareEventType.RoundStarted, round, "", "", "", DateTime.UtcNow);

    public static CompareEvent Started(int round, string agentName, string specialty, string inputMessage = "")
        => new(CompareEventType.AgentStarted, round, agentName, specialty, "", DateTime.UtcNow, inputMessage);

    public static CompareEvent Delta(int round, string agentName, string specialty, string delta)
        => new(CompareEventType.AgentDelta, round, agentName, specialty, delta, DateTime.UtcNow);

    public static CompareEvent Completed(int round, string agentName, string specialty, string message, IReadOnlyList<SearchCitation>? citations = null)
        => new(CompareEventType.AgentCompleted, round, agentName, specialty, message, DateTime.UtcNow, "", citations);

    public static CompareEvent Summary(int round, string message)
        => new(CompareEventType.OrchestratorSummary, round, "Orchestrator", "", message, DateTime.UtcNow);

    public static CompareEvent OrchestratorDeltaEvent(int round, string delta)
        => new(CompareEventType.OrchestratorDelta, round, "Orchestrator", "", delta, DateTime.UtcNow);

    public static CompareEvent FinalReportDeltaEvent(string delta)
        => new(CompareEventType.FinalReportDelta, 0, "Orchestrator", "", delta, DateTime.UtcNow);

    public static CompareEvent Consensus(int round)
        => new(CompareEventType.ConsensusReached, round, "Orchestrator", "", "All models have reached consensus.", DateTime.UtcNow);

    public static CompareEvent MaxRounds()
        => new(CompareEventType.MaxRoundsReached, 0, "Orchestrator", "", "Maximum rounds reached.", DateTime.UtcNow);

    public static CompareEvent FinalStarted()
        => new(CompareEventType.FinalReportStarted, 0, "Orchestrator", "", "", DateTime.UtcNow);

    public static CompareEvent FinalCompleted(string message, IReadOnlyList<SearchCitation>? citations = null)
        => new(CompareEventType.FinalReportCompleted, 0, "Orchestrator", "", message, DateTime.UtcNow, "", citations);

    public static CompareEvent ErrorEvent(int round, string message)
        => new(CompareEventType.Error, round, "", "", message, DateTime.UtcNow);
}
