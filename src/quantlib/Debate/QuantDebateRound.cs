namespace QuantLib.Agents.Quants;

public record QuantRoundInput(string UserInput, IReadOnlyList<QuantRound> PreviousRounds);

public record QuantResponse(string AgentName, string Specialty, string Message);

public record QuantRound(int RoundNumber, IReadOnlyList<QuantResponse> Responses, string OrchestratorSummary);
