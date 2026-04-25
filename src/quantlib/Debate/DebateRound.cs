using QuantLib.Agents;

namespace QuantLib.Agents.Quants;

public record DebateRoundInput(string UserInput, IReadOnlyList<DebateRound> PreviousRounds);

public record DebateResponse(string AgentName, string Specialty, string Message, IReadOnlyList<SearchCitation>? Citations = null);

public record DebateRound(int RoundNumber, IReadOnlyList<DebateResponse> Responses, string OrchestratorSummary);
