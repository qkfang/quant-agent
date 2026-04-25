using System.Runtime.CompilerServices;
using System.Text;
using Azure.AI.Projects;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;
using QuantLib.Agents.Quants;

namespace QuantLib.Agents.Turn;

public class TurnOrchestrator
{
    private const int MaxRounds = 10;

    private readonly PricingQuantAgent _pricingQuant;
    private readonly RiskQuantAgent _riskQuant;
    private readonly AlphaQuantAgent _alphaQuant;
    private readonly TurnOrchestratorAgent _orchestrator;
    private readonly ILogger _logger;

    public TurnOrchestrator(AIProjectClient aiProjectClient, string deploymentName, ILogger logger, string? searchConnectionId = null, string? searchIndexName = null)
    {
        _logger = logger;

        _alphaQuant = new AlphaQuantAgent(aiProjectClient, deploymentName, searchConnectionId, searchIndexName, logger);
        _pricingQuant = new PricingQuantAgent(aiProjectClient, deploymentName, searchConnectionId, searchIndexName, logger);
        _riskQuant = new RiskQuantAgent(aiProjectClient, deploymentName, searchConnectionId, searchIndexName, logger);
        _orchestrator = new TurnOrchestratorAgent(aiProjectClient, deploymentName, logger);
    }

    private Workflow BuildSequentialWorkflow()
    {
        var dispatch = new TurnDispatchExecutor();
        var alphaExec = new AlphaQuantTurnExecutor(_alphaQuant, _logger);
        var pricingExec = new PricingQuantTurnExecutor(_pricingQuant, _logger);
        var riskExec = new RiskQuantTurnExecutor(_riskQuant, _logger);

        WorkflowBuilder builder = new(dispatch);
        builder.AddEdge(dispatch, alphaExec)
               .AddEdge(alphaExec, pricingExec)
               .AddEdge(pricingExec, riskExec)
               .WithOutputFrom(riskExec);

        return builder.Build();
    }

    public async IAsyncEnumerable<AgentEvent> RunStreamingAsync(
        string userInput,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var rounds = new List<TurnRound>();

        for (int round = 1; round <= MaxRounds; round++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return AgentEvent.RoundStarted(round);

            string agentInputDesc;
            if (round == 1)
            {
                agentInputDesc = $"Provide initial sequential analysis on: {Truncate(userInput, 200)}";
            }
            else
            {
                var lastSummary = rounds[^1].OrchestratorSummary;
                agentInputDesc = $"Refine your analysis. Orchestrator feedback:\n{Truncate(lastSummary, 300)}";
            }

            yield return AgentEvent.Started(round, _alphaQuant.Name, _alphaQuant.Specialty, agentInputDesc);
            yield return AgentEvent.Started(round, _pricingQuant.Name, _pricingQuant.Specialty, agentInputDesc);
            yield return AgentEvent.Started(round, _riskQuant.Name, _riskQuant.Specialty, agentInputDesc);

            var turnInput = new TurnInput(userInput, rounds);
            var workflow = BuildSequentialWorkflow();
            await using var run = await InProcessExecution.RunStreamingAsync(workflow, turnInput);

            var responses = new List<TurnResponse>();
            await foreach (var evt in run.WatchStreamAsync())
            {
                if (evt is WorkflowOutputEvent outputEvt && outputEvt.Data is TurnState finalState)
                {
                    responses = finalState.CurrentResponses;
                    foreach (var resp in responses)
                    {
                        yield return AgentEvent.Completed(round, resp.AgentName, resp.Specialty, resp.Message, resp.Citations);
                    }
                }
                else if (evt is WorkflowErrorEvent errorEvt)
                {
                    _logger.LogError("Workflow error: {Error}", errorEvt.Exception?.Message);
                    yield return AgentEvent.ErrorEvent(round, errorEvt.Exception?.Message ?? "Unknown error");
                }
                else if (evt is ExecutorFailedEvent failedEvt)
                {
                    _logger.LogError("Executor {Id} failed: {Data}", failedEvt.ExecutorId, failedEvt.Data);
                    yield return AgentEvent.ErrorEvent(round, $"Executor {failedEvt.ExecutorId} failed");
                }
            }

            string orchestratorPrompt = BuildOrchestratorPrompt(userInput, rounds, responses, round);
            var summaryResult = await _orchestrator.RunAsync(orchestratorPrompt);
            var summary = summaryResult.Text;
            rounds.Add(new TurnRound(round, responses, summary));

            yield return AgentEvent.Summary(round, summary);

            if (summary.Contains("[CONSENSUS_REACHED]", StringComparison.OrdinalIgnoreCase))
            {
                yield return AgentEvent.Consensus(round);
                break;
            }

            if (round == MaxRounds)
            {
                yield return AgentEvent.MaxRounds();
            }
        }

        yield return AgentEvent.FinalStarted();
        string finalPrompt = BuildFinalSummaryPrompt(userInput, rounds);
        var finalResult = await _orchestrator.RunAsync(finalPrompt);
        yield return AgentEvent.FinalCompleted(finalResult.Text, finalResult.Citations);
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
        Console.WriteLine("║             QUANT DESK SEQUENTIAL TURN WORKFLOW             ║");
        Console.WriteLine("║     Powered by Azure AI Foundry & Microsoft Agent Framework ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine($"  User Request: {userInput}");
        Console.WriteLine(new string('═', 62));

        var rounds = new List<TurnRound>();

        for (int round = 1; round <= MaxRounds; round++)
        {
            Console.WriteLine();
            Console.WriteLine($"\u001b[33m╔══ TURN {round}/{MaxRounds} ══╗\u001b[0m");
            Console.WriteLine();
            Console.WriteLine("  ⟶ Running agents sequentially: Alpha → Pricing → Risk...");
            Console.WriteLine();

            var turnInput = new TurnInput(userInput, rounds);
            var workflow = BuildSequentialWorkflow();
            await using var run = await InProcessExecution.RunStreamingAsync(workflow, turnInput);

            var responses = new List<TurnResponse>();
            await foreach (var evt in run.WatchStreamAsync())
            {
                if (evt is WorkflowOutputEvent outputEvt && outputEvt.Data is TurnState finalState)
                {
                    responses = finalState.CurrentResponses;
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

            foreach (var response in responses)
            {
                var color = response.AgentName switch
                {
                    "Alpha Quant" => "\u001b[32m",
                    "Pricing Quant" => "\u001b[34m",
                    "Risk Quant" => "\u001b[35m",
                    _ => "\u001b[0m"
                };
                Console.WriteLine($"  {color}┌─ [{response.AgentName}] ({response.Specialty}) ─┐\u001b[0m");
                Console.WriteLine($"  {color}{response.Message}\u001b[0m");
                Console.WriteLine($"  {color}└{'─'.Repeat(40)}┘\u001b[0m");
                Console.WriteLine();
            }

            string orchestratorPrompt = BuildOrchestratorPrompt(userInput, rounds, responses, round);
            Console.WriteLine("  \u001b[33m⟶ Orchestrator validating views and deciding next action...\u001b[0m");
            Console.WriteLine();

            var summaryResult = await _orchestrator.RunAsync(orchestratorPrompt);
            var summary = summaryResult.Text;

            rounds.Add(new TurnRound(round, responses, summary));

            Console.WriteLine($"  \u001b[33m┌─ [Orchestrator Summary - Turn {round}] ─┐\u001b[0m");
            Console.WriteLine($"  \u001b[33m{summary}\u001b[0m");
            Console.WriteLine($"  \u001b[33m└{'─'.Repeat(40)}┘\u001b[0m");
            Console.WriteLine();

            if (summary.Contains("[CONSENSUS_REACHED]", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("  \u001b[32m✓ Views validated. Consensus reached. Finishing workflow.\u001b[0m");
                break;
            }

            if (round == MaxRounds)
            {
                Console.WriteLine("  \u001b[31m✗ Maximum turns reached. Finishing workflow.\u001b[0m");
            }
        }

        Console.WriteLine();
        Console.WriteLine(new string('═', 62));
        Console.WriteLine("\u001b[36m  FINAL ANALYSIS REPORT\u001b[0m");
        Console.WriteLine(new string('═', 62));

        string finalPrompt = BuildFinalSummaryPrompt(userInput, rounds);
        var finalResult = await _orchestrator.RunAsync(finalPrompt);

        Console.WriteLine($"\u001b[36m{finalResult.Text}\u001b[0m");
        Console.WriteLine();
        Console.WriteLine(new string('═', 62));
        Console.WriteLine("  Workflow completed.");
    }

    private static string BuildOrchestratorPrompt(string userInput, IReadOnlyList<TurnRound> previousRounds, IReadOnlyList<TurnResponse> currentResponses, int currentRound)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"User request: {userInput}");
        sb.AppendLine($"Current turn: {currentRound}/{MaxRounds}");
        sb.AppendLine();

        if (previousRounds.Count > 0)
        {
            sb.AppendLine("=== PREVIOUS TURNS ===");
            foreach (var round in previousRounds)
            {
                sb.AppendLine($"--- Turn {round.RoundNumber} ---");
                foreach (var resp in round.Responses)
                {
                    sb.AppendLine($"[{resp.AgentName}]: {resp.Message}");
                    sb.AppendLine();
                }
                sb.AppendLine($"[Your Summary]: {round.OrchestratorSummary}");
                sb.AppendLine();
            }
            sb.AppendLine("=== END PREVIOUS TURNS ===");
            sb.AppendLine();
        }

        sb.AppendLine("=== CURRENT TURN RESPONSES (Sequential: Alpha → Pricing → Risk) ===");
        foreach (var resp in currentResponses)
        {
            sb.AppendLine($"[{resp.AgentName} - {resp.Specialty}]: {resp.Message}");
            sb.AppendLine();
        }
        sb.AppendLine("=== END CURRENT TURN ===");
        sb.AppendLine();
        sb.AppendLine("The agents provided their views sequentially, each building on the previous agent's analysis.");
        sb.AppendLine("Validate whether the combined view is coherent and correct.");
        sb.AppendLine("Identify areas of agreement, disagreement, and gaps.");
        sb.AppendLine("If the views are validated and agents substantially agree on key conclusions, include the exact marker [CONSENSUS_REACHED] in your response.");
        sb.AppendLine("If there are still significant disagreements or gaps, identify them clearly so agents can address them in the next turn.");
        sb.AppendLine();
        sb.AppendLine("Additionally, pose 2-3 targeted questions for agents to address in the next turn.");

        return sb.ToString();
    }

    private static string BuildFinalSummaryPrompt(string userInput, IReadOnlyList<TurnRound> allRounds)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"User request: {userInput}");
        sb.AppendLine();
        sb.AppendLine("=== COMPLETE TURN HISTORY ===");
        foreach (var round in allRounds)
        {
            sb.AppendLine($"--- Turn {round.RoundNumber} ---");
            foreach (var resp in round.Responses)
            {
                sb.AppendLine($"[{resp.AgentName} - {resp.Specialty}]: {resp.Message}");
                sb.AppendLine();
            }
            sb.AppendLine($"[Orchestrator Summary]: {round.OrchestratorSummary}");
            sb.AppendLine();
        }
        sb.AppendLine("=== END TURN HISTORY ===");
        sb.AppendLine();
        sb.AppendLine("Produce a comprehensive final analysis report for the user. Synthesize all perspectives (alpha, pricing, risk) into a cohesive assessment. Include key findings, risk considerations, opportunities, and actionable recommendations.");

        return sb.ToString();
    }
}

internal static class TurnStringExtensions
{
    public static string Repeat(this char c, int count) => new(c, count);
}
