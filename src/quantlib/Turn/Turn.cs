using QuantLib.Agents;

namespace QuantLib.Agents.Turn;

public record TurnResponse(string AgentName, string Specialty, string Message, IReadOnlyList<SearchCitation>? Citations = null);

public record TurnRound(int RoundNumber, IReadOnlyList<TurnResponse> Responses, string OrchestratorSummary);
