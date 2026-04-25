namespace QuantLib.Agents.Turn;

public record TurnInput(string UserInput, IReadOnlyList<TurnRound> PreviousRounds);

public record TurnResponse(string AgentName, string Specialty, string Message);

public record TurnRound(int RoundNumber, IReadOnlyList<TurnResponse> Responses, string OrchestratorSummary);

public record TurnState(
    string UserInput,
    IReadOnlyList<TurnRound> PreviousRounds,
    List<TurnResponse> CurrentResponses);
