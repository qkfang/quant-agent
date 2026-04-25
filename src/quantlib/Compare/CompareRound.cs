using QuantLib.Agents;

namespace QuantLib.Agents.Compare;

public record CompareRoundInput(string UserInput, IReadOnlyList<CompareRound> PreviousRounds);

public record CompareResponse(string ModelName, string DeploymentName, string Message, IReadOnlyList<SearchCitation>? Citations = null);

public record CompareRound(int RoundNumber, IReadOnlyList<CompareResponse> Responses, string OrchestratorSummary);
