using QuantLib.Agents;

namespace QuantLib.Agents.Turn;

public record TurnInput(string UserInput, IReadOnlyList<TurnRound> PreviousRounds);

public record TurnResponse(string AgentName, string Specialty, string Message, IReadOnlyList<SearchCitation>? Citations = null);

public record TurnRound(int RoundNumber, IReadOnlyList<TurnResponse> Responses, string OrchestratorSummary);

public record TurnState(
    string UserInput,
    IReadOnlyList<TurnRound> PreviousRounds,
    List<TurnResponse> CurrentResponses);
