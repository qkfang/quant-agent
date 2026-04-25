namespace QuantLib.Agents.Turn;

public record QuantTurnInput(string UserInput, IReadOnlyList<QuantTurnRound> PreviousRounds);

public record QuantTurnResponse(string AgentName, string Specialty, string Message);

public record QuantTurnRound(int RoundNumber, IReadOnlyList<QuantTurnResponse> Responses, string OrchestratorSummary);

public record QuantTurnState(
    string UserInput,
    IReadOnlyList<QuantTurnRound> PreviousRounds,
    List<QuantTurnResponse> CurrentResponses);
