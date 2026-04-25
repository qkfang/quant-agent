using QuantLib.Agents;

namespace QuantLib.Agents.Quants;

public enum AgentEventType
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

public record AgentEvent(
    AgentEventType Type,
    int Round,
    string AgentName,
    string Specialty,
    string Message,
    DateTime Timestamp,
    string InputMessage = "",
    IReadOnlyList<SearchCitation>? Citations = null
)
{
    public static AgentEvent RoundStarted(int round)
        => new(AgentEventType.RoundStarted, round, "", "", "", DateTime.UtcNow);

    public static AgentEvent Started(int round, string agentName, string specialty, string inputMessage = "")
        => new(AgentEventType.AgentStarted, round, agentName, specialty, "", DateTime.UtcNow, inputMessage);

    public static AgentEvent Delta(int round, string agentName, string specialty, string delta)
        => new(AgentEventType.AgentDelta, round, agentName, specialty, delta, DateTime.UtcNow);

    public static AgentEvent Completed(int round, string agentName, string specialty, string message, IReadOnlyList<SearchCitation>? citations = null)
        => new(AgentEventType.AgentCompleted, round, agentName, specialty, message, DateTime.UtcNow, "", citations);

    public static AgentEvent Summary(int round, string message)
        => new(AgentEventType.OrchestratorSummary, round, "Orchestrator", "", message, DateTime.UtcNow);

    public static AgentEvent OrchestratorDeltaEvent(int round, string delta)
        => new(AgentEventType.OrchestratorDelta, round, "Orchestrator", "", delta, DateTime.UtcNow);

    public static AgentEvent FinalReportDeltaEvent(string delta)
        => new(AgentEventType.FinalReportDelta, 0, "Orchestrator", "", delta, DateTime.UtcNow);

    public static AgentEvent Consensus(int round)
        => new(AgentEventType.ConsensusReached, round, "Orchestrator", "", "All agents have reached consensus.", DateTime.UtcNow);

    public static AgentEvent MaxRounds()
        => new(AgentEventType.MaxRoundsReached, 0, "Orchestrator", "", "Maximum rounds reached.", DateTime.UtcNow);

    public static AgentEvent FinalStarted()
        => new(AgentEventType.FinalReportStarted, 0, "Orchestrator", "", "", DateTime.UtcNow);

    public static AgentEvent FinalCompleted(string message, IReadOnlyList<SearchCitation>? citations = null)
        => new(AgentEventType.FinalReportCompleted, 0, "Orchestrator", "", message, DateTime.UtcNow, "", citations);

    public static AgentEvent ErrorEvent(int round, string message)
        => new(AgentEventType.Error, round, "", "", message, DateTime.UtcNow);
}
