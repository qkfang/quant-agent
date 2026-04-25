namespace QuantLib.Agents.Quants;

public record DebateRoundInput(string UserInput, IReadOnlyList<DebateRound> PreviousRounds);

public record DebateResponse(string AgentName, string Specialty, string Message);

public record DebateRound(int RoundNumber, IReadOnlyList<DebateResponse> Responses, string OrchestratorSummary);
