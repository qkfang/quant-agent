using System.Runtime.CompilerServices;
using System.Text;
using Azure.AI.Projects;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;

namespace QuantLib.Agents.Compare;

public class CompareOrchestrator
{
    private const int MaxRounds = 3;

    private readonly List<CompareAgent> _agents;
    private readonly CompareOrchestratorAgent _orchestrator;
    private readonly ILogger _logger;

    public CompareOrchestrator(
        AIProjectClient aiProjectClient,
        IReadOnlyList<(string ModelName, string DeploymentName)> models,
        string orchestratorDeploymentName,
        ILogger logger,
        string? searchConnectionId = null,
        string? searchIndexName = null)
    {
        _logger = logger;
        _agents = new List<CompareAgent>();

        foreach (var (modelName, deploymentName) in models)
        {
            var agentId = $"compare-{modelName.Replace(".", "-").Replace(" ", "-").ToLowerInvariant()}";
            _agents.Add(new CompareAgent(aiProjectClient, agentId, modelName, deploymentName, searchConnectionId, searchIndexName, logger));
        }

        _orchestrator = new CompareOrchestratorAgent(aiProjectClient, orchestratorDeploymentName, logger);
    }

    private Workflow BuildRoundWorkflow()
    {
        var dispatchExecutor = new CompareRoundDispatchExecutor();
        var executors = _agents
            .Select(agent => new CompareAgentExecutor(
                $"compare-{agent.ModelName.Replace(".", "-").Replace(" ", "-").ToLowerInvariant()}-executor",
                agent,
                _logger))
            .ToList();

        WorkflowBuilder builder = new(dispatchExecutor);
        builder.AddFanOutEdge(dispatchExecutor, [.. executors])
               .WithOutputFrom([.. executors]);

        return builder.Build();
    }

    public async IAsyncEnumerable<CompareEvent> RunStreamingAsync(
        string userInput,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var rounds = new List<CompareRound>();

        for (int round = 1; round <= MaxRounds; round++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return CompareEvent.RoundStarted(round);

            string agentInputDesc;
            if (round == 1)
            {
                agentInputDesc = $"Provide initial analysis on: {Truncate(userInput, 200)}";
            }
            else
            {
                var lastSummary = rounds[^1].OrchestratorSummary;
                agentInputDesc = $"Refine your analysis. Orchestrator feedback:\n{Truncate(lastSummary, 300)}";
            }

            foreach (var agent in _agents)
            {
                yield return CompareEvent.Started(round, agent.ModelName, agent.ModelName, agentInputDesc);
            }

            var roundInput = new CompareRoundInput(userInput, rounds);
            var workflow = BuildRoundWorkflow();
            await using var run = await InProcessExecution.RunStreamingAsync(workflow, roundInput);

            var responses = new List<CompareResponse>();
            await foreach (var evt in run.WatchStreamAsync())
            {
                if (evt is WorkflowOutputEvent outputEvt && outputEvt.Data is CompareResponse response)
                {
                    responses.Add(response);
                    yield return CompareEvent.Completed(round, response.ModelName, response.DeploymentName, response.Message);
                }
                else if (evt is WorkflowErrorEvent errorEvt)
                {
                    _logger.LogError("Workflow error: {Error}", errorEvt.Exception?.Message);
                    yield return CompareEvent.ErrorEvent(round, errorEvt.Exception?.Message ?? "Unknown error");
                }
                else if (evt is ExecutorFailedEvent failedEvt)
                {
                    _logger.LogError("Executor {Id} failed: {Data}", failedEvt.ExecutorId, failedEvt.Data);
                    yield return CompareEvent.ErrorEvent(round, $"Executor {failedEvt.ExecutorId} failed");
                }
            }

            string orchestratorPrompt = BuildOrchestratorPrompt(userInput, rounds, responses, round);
            var summary = await _orchestrator.RunAsync(orchestratorPrompt);
            rounds.Add(new CompareRound(round, responses, summary));

            yield return CompareEvent.Summary(round, summary);

            if (summary.Contains("[CONSENSUS_REACHED]", StringComparison.OrdinalIgnoreCase))
            {
                yield return CompareEvent.Consensus(round);
                break;
            }

            if (round == MaxRounds)
            {
                yield return CompareEvent.MaxRounds();
            }
        }

        yield return CompareEvent.FinalStarted();
        string finalPrompt = BuildFinalSummaryPrompt(userInput, rounds);
        var finalReport = await _orchestrator.RunAsync(finalPrompt);
        yield return CompareEvent.FinalCompleted(finalReport);
    }

    private static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;
        if (text.Length <= maxLength)
            return text;
        return text[..maxLength] + "...";
    }

    public async Task RunConsoleAsync(string userInput)
    {
        Console.WriteLine();
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║              MULTI-MODEL COMPARE WORKFLOW                   ║");
        Console.WriteLine("║     Powered by Azure AI Foundry & Microsoft Agent Framework ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine($"  User Request: {userInput}");
        Console.WriteLine($"  Models: {string.Join(", ", _agents.Select(a => a.ModelName))}");
        Console.WriteLine(new string('═', 62));

        var rounds = new List<CompareRound>();

        for (int round = 1; round <= MaxRounds; round++)
        {
            Console.WriteLine();
            Console.WriteLine($"\u001b[33m╔══ ROUND {round}/{MaxRounds} ══╗\u001b[0m");
            Console.WriteLine();
            Console.WriteLine("  ⟶ Dispatching to all models concurrently...");
            Console.WriteLine();

            var roundInput = new CompareRoundInput(userInput, rounds);
            var workflow = BuildRoundWorkflow();
            await using var run = await InProcessExecution.RunStreamingAsync(workflow, roundInput);

            var responses = new List<CompareResponse>();
            await foreach (var evt in run.WatchStreamAsync())
            {
                if (evt is WorkflowOutputEvent outputEvt && outputEvt.Data is CompareResponse response)
                {
                    responses.Add(response);
                }
                else if (evt is WorkflowErrorEvent errorEvt)
                {
                    _logger.LogError("Workflow error: {Error}", errorEvt.Exception?.Message);
                }
                else if (evt is ExecutorFailedEvent failedEvt)
                {
                    _logger.LogError("Executor {Id} failed: {Data}", failedEvt.ExecutorId, failedEvt.Data);
                }
            }

            var colors = new[] { "\u001b[34m", "\u001b[35m", "\u001b[32m", "\u001b[36m" };
            for (int i = 0; i < responses.Count; i++)
            {
                var response = responses[i];
                var color = colors[i % colors.Length];
                Console.WriteLine($"  {color}┌─ [{response.ModelName}] ─┐\u001b[0m");
                Console.WriteLine($"  {color}{response.Message}\u001b[0m");
                Console.WriteLine($"  {color}└{new string('─', 40)}┘\u001b[0m");
                Console.WriteLine();
            }

            string orchestratorPrompt = BuildOrchestratorPrompt(userInput, rounds, responses, round);
            Console.WriteLine("  \u001b[33m⟶ Orchestrator comparing model outputs...\u001b[0m");
            Console.WriteLine();

            var summary = await _orchestrator.RunAsync(orchestratorPrompt);
            rounds.Add(new CompareRound(round, responses, summary));

            Console.WriteLine($"  \u001b[33m┌─ [Orchestrator Comparison - Round {round}] ─┐\u001b[0m");
            Console.WriteLine($"  \u001b[33m{summary}\u001b[0m");
            Console.WriteLine($"  \u001b[33m└{new string('─', 40)}┘\u001b[0m");
            Console.WriteLine();

            if (summary.Contains("[CONSENSUS_REACHED]", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("  \u001b[32m✓ All models have reached consensus. Terminating workflow.\u001b[0m");
                break;
            }

            if (round == MaxRounds)
            {
                Console.WriteLine("  \u001b[31m✗ Maximum rounds reached. Terminating workflow.\u001b[0m");
            }
        }

        Console.WriteLine();
        Console.WriteLine(new string('═', 62));
        Console.WriteLine("\u001b[36m  FINAL COMPARISON REPORT\u001b[0m");
        Console.WriteLine(new string('═', 62));

        string finalPrompt = BuildFinalSummaryPrompt(userInput, rounds);
        var finalReport = await _orchestrator.RunAsync(finalPrompt);

        Console.WriteLine($"\u001b[36m{finalReport}\u001b[0m");
        Console.WriteLine();
        Console.WriteLine(new string('═', 62));
        Console.WriteLine("  Workflow completed.");
    }

    private static string BuildOrchestratorPrompt(string userInput, IReadOnlyList<CompareRound> previousRounds, IReadOnlyList<CompareResponse> currentResponses, int currentRound)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"User request: {userInput}");
        sb.AppendLine($"Current round: {currentRound}/{MaxRounds}");
        sb.AppendLine();

        if (previousRounds.Count > 0)
        {
            sb.AppendLine("=== PREVIOUS ROUNDS ===");
            foreach (var round in previousRounds)
            {
                sb.AppendLine($"--- Round {round.RoundNumber} ---");
                foreach (var resp in round.Responses)
                {
                    sb.AppendLine($"[{resp.ModelName}]: {resp.Message}");
                    sb.AppendLine();
                }
                sb.AppendLine($"[Your Summary]: {round.OrchestratorSummary}");
                sb.AppendLine();
            }
            sb.AppendLine("=== END PREVIOUS ROUNDS ===");
            sb.AppendLine();
        }

        sb.AppendLine("=== CURRENT ROUND RESPONSES ===");
        foreach (var resp in currentResponses)
        {
            sb.AppendLine($"[{resp.ModelName}]: {resp.Message}");
            sb.AppendLine();
        }
        sb.AppendLine("=== END CURRENT ROUND ===");
        sb.AppendLine();
        sb.AppendLine("Compare the responses from all models. Identify differences in reasoning, depth, and conclusions.");
        sb.AppendLine("If all models substantially agree on the key conclusions, include the exact marker [CONSENSUS_REACHED] in your response.");
        sb.AppendLine("If there are still significant differences, clearly articulate what they are so models can address them in the next round.");
        sb.AppendLine();
        sb.AppendLine("Additionally, pose 2-3 targeted questions for the models to address in the next round.");
        sb.AppendLine("These should probe areas where models diverged or made different assumptions.");

        return sb.ToString();
    }

    private static string BuildFinalSummaryPrompt(string userInput, IReadOnlyList<CompareRound> allRounds)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"User request: {userInput}");
        sb.AppendLine();
        sb.AppendLine("=== COMPLETE DISCUSSION HISTORY ===");
        foreach (var round in allRounds)
        {
            sb.AppendLine($"--- Round {round.RoundNumber} ---");
            foreach (var resp in round.Responses)
            {
                sb.AppendLine($"[{resp.ModelName}]: {resp.Message}");
                sb.AppendLine();
            }
            sb.AppendLine($"[Orchestrator Summary]: {round.OrchestratorSummary}");
            sb.AppendLine();
        }
        sb.AppendLine("=== END DISCUSSION ===");
        sb.AppendLine();
        sb.AppendLine("Produce a comprehensive final comparison report. Synthesize the best insights from all models.");
        sb.AppendLine("Compare each model's strengths and weaknesses in their analysis.");
        sb.AppendLine("Include key findings, areas of agreement, notable divergences, and actionable recommendations.");

        return sb.ToString();
    }
}
