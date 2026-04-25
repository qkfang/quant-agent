using System.Runtime.CompilerServices;
using System.Text;
using Azure.AI.Projects;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;

namespace QuantLib.Agents.Quants;

public class DebateOrchestrator
{
    private const int MaxRounds = 5;

    private readonly PricingQuantAgent _pricingQuant;
    private readonly RiskQuantAgent _riskQuant;
    private readonly AlphaQuantAgent _alphaQuant;
    private readonly DebateOrchestratorAgent _orchestrator;
    private readonly ILogger _logger;

    public DebateOrchestrator(AIProjectClient aiProjectClient, string deploymentName, ILogger logger, string? searchConnectionId = null, string? searchIndexName = null)
    {
        _logger = logger;

        _pricingQuant = new PricingQuantAgent(aiProjectClient, deploymentName, searchConnectionId, searchIndexName, logger);
        _riskQuant = new RiskQuantAgent(aiProjectClient, deploymentName, searchConnectionId, searchIndexName, logger);
        _alphaQuant = new AlphaQuantAgent(aiProjectClient, deploymentName, searchConnectionId, searchIndexName, logger);
        _orchestrator = new DebateOrchestratorAgent(aiProjectClient, deploymentName, logger);
    }

    private Workflow BuildRoundWorkflow()
    {
        var dispatchExecutor = new DebateRoundDispatchExecutor();
        var pricingExec = new PricingQuantExecutor(_pricingQuant, _logger);
        var riskExec = new RiskQuantExecutor(_riskQuant, _logger);
        var alphaExec = new AlphaQuantExecutor(_alphaQuant, _logger);

        WorkflowBuilder builder = new(dispatchExecutor);
        builder.AddFanOutEdge(dispatchExecutor, [pricingExec, riskExec, alphaExec])
               .WithOutputFrom(pricingExec, riskExec, alphaExec);

        return builder.Build();
    }

    public async IAsyncEnumerable<AgentEvent> RunStreamingAsync(
        string userInput,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var rounds = new List<DebateRound>();

        for (int round = 1; round <= MaxRounds; round++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return AgentEvent.RoundStarted(round);

            // Build a concise input description for the UI
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

            // Signal that each agent is starting with their input
            yield return AgentEvent.Started(round, _pricingQuant.Name, _pricingQuant.Specialty, agentInputDesc);
            yield return AgentEvent.Started(round, _riskQuant.Name, _riskQuant.Specialty, agentInputDesc);
            yield return AgentEvent.Started(round, _alphaQuant.Name, _alphaQuant.Specialty, agentInputDesc);

            var roundInput = new DebateRoundInput(userInput, rounds);
            var workflow = BuildRoundWorkflow();
            await using var run = await InProcessExecution.RunStreamingAsync(workflow, roundInput);

            var responses = new List<DebateResponse>();
            await foreach (var evt in run.WatchStreamAsync())
            {
                if (evt is WorkflowOutputEvent outputEvt && outputEvt.Data is DebateResponse response)
                {
                    responses.Add(response);
                    yield return AgentEvent.Completed(round, response.AgentName, response.Specialty, response.Message);
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
            var summary = await _orchestrator.RunAsync(orchestratorPrompt);
            rounds.Add(new DebateRound(round, responses, summary));

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
        var finalReport = await _orchestrator.RunAsync(finalPrompt);
        yield return AgentEvent.FinalCompleted(finalReport);
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
        Console.WriteLine("║               QUANT DESK ANALYSIS WORKFLOW                  ║");
        Console.WriteLine("║     Powered by Azure AI Foundry & Microsoft Agent Framework ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine($"  User Request: {userInput}");
        Console.WriteLine(new string('═', 62));

        var rounds = new List<DebateRound>();

        for (int round = 1; round <= MaxRounds; round++)
        {
            Console.WriteLine();
            Console.WriteLine($"\u001b[33m╔══ ROUND {round}/{MaxRounds} ══╗\u001b[0m");
            Console.WriteLine();
            Console.WriteLine("  ⟶ Dispatching to all quant agents concurrently...");
            Console.WriteLine();

            var roundInput = new DebateRoundInput(userInput, rounds);
            var workflow = BuildRoundWorkflow();
            await using var run = await InProcessExecution.RunStreamingAsync(workflow, roundInput);

            var responses = new List<DebateResponse>();
            await foreach (var evt in run.WatchStreamAsync())
            {
                if (evt is WorkflowOutputEvent outputEvt && outputEvt.Data is DebateResponse response)
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

            foreach (var response in responses)
            {
                var color = response.AgentName switch
                {
                    "Pricing Quant" => "\u001b[34m",
                    "Risk Quant" => "\u001b[35m",
                    "Alpha Quant" => "\u001b[32m",
                    _ => "\u001b[0m"
                };
                Console.WriteLine($"  {color}┌─ [{response.AgentName}] ({response.Specialty}) ─┐\u001b[0m");
                Console.WriteLine($"  {color}{response.Message}\u001b[0m");
                Console.WriteLine($"  {color}└{'─'.Repeat(40)}┘\u001b[0m");
                Console.WriteLine();
            }

            string orchestratorPrompt = BuildOrchestratorPrompt(userInput, rounds, responses, round);
            Console.WriteLine("  \u001b[33m⟶ Orchestrator summarizing and evaluating consensus...\u001b[0m");
            Console.WriteLine();

            var summary = await _orchestrator.RunAsync(orchestratorPrompt);

            rounds.Add(new DebateRound(round, responses, summary));

            Console.WriteLine($"  \u001b[33m┌─ [Orchestrator Summary - Round {round}] ─┐\u001b[0m");
            Console.WriteLine($"  \u001b[33m{summary}\u001b[0m");
            Console.WriteLine($"  \u001b[33m└{'─'.Repeat(40)}┘\u001b[0m");
            Console.WriteLine();

            if (summary.Contains("[CONSENSUS_REACHED]", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("  \u001b[32m✓ All agents have reached consensus. Terminating workflow.\u001b[0m");
                break;
            }

            if (round == MaxRounds)
            {
                Console.WriteLine("  \u001b[31m✗ Maximum rounds reached. Terminating workflow.\u001b[0m");
            }
        }

        Console.WriteLine();
        Console.WriteLine(new string('═', 62));
        Console.WriteLine("\u001b[36m  FINAL ANALYSIS REPORT\u001b[0m");
        Console.WriteLine(new string('═', 62));

        string finalPrompt = BuildFinalSummaryPrompt(userInput, rounds);
        var finalReport = await _orchestrator.RunAsync(finalPrompt);

        Console.WriteLine($"\u001b[36m{finalReport}\u001b[0m");
        Console.WriteLine();
        Console.WriteLine(new string('═', 62));
        Console.WriteLine("  Workflow completed.");
    }

    private static string BuildOrchestratorPrompt(string userInput, IReadOnlyList<DebateRound> previousRounds, IReadOnlyList<DebateResponse> currentResponses, int currentRound)
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
                    sb.AppendLine($"[{resp.AgentName}]: {resp.Message}");
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
            sb.AppendLine($"[{resp.AgentName} - {resp.Specialty}]: {resp.Message}");
            sb.AppendLine();
        }
        sb.AppendLine("=== END CURRENT ROUND ===");
        sb.AppendLine();
        sb.AppendLine("Strictly evaluate every opinion from all agents in this round. For EACH opinion:");
        sb.AppendLine("- Assess the supporting evidence and mark the opinion as:");
        sb.AppendLine("  ✅ VALID - Evidence is strong and reasoning is sound");
        sb.AppendLine("  ❌ INVALID - Evidence is weak, flawed, or contradicted by stronger counter-evidence");
        sb.AppendLine("  ⚠️ PENDING - Evidence is inconclusive; needs more data in the next round");
        sb.AppendLine();

        if (previousRounds.Count > 0)
        {
            sb.AppendLine("IMPORTANT: Compare opinions in this round against opinions from previous rounds.");
            sb.AppendLine("- Track which opinions changed status (e.g., PENDING → VALID, VALID → INVALID)");
            sb.AppendLine("- Note if agents revised or dropped previous opinions and whether the revision is justified");
            sb.AppendLine("- Maintain a running Opinion Ledger that shows how each opinion evolved across rounds");
            sb.AppendLine();
        }

        sb.AppendLine("Present your evaluation under the heading 'Opinion Ledger' with columns: Agent, Opinion, Status, Reasoning.");
        sb.AppendLine();
        sb.AppendLine("If all key opinions are marked VALID and agents agree, include the exact marker [CONSENSUS_REACHED] in your response.");
        sb.AppendLine("If PENDING or INVALID opinions remain on critical topics, clearly state what evidence is needed.");
        sb.AppendLine();
        sb.AppendLine("Additionally, pose 2-3 targeted debate questions for the agents to address in the next round.");
        sb.AppendLine("Focus on challenging INVALID or PENDING opinions and requesting stronger evidence.");
        sb.AppendLine("Format them clearly under a 'Debate Questions' heading.");

        return sb.ToString();
    }

    private static string BuildFinalSummaryPrompt(string userInput, IReadOnlyList<DebateRound> allRounds)
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
                sb.AppendLine($"[{resp.AgentName} - {resp.Specialty}]: {resp.Message}");
                sb.AppendLine();
            }
            sb.AppendLine($"[Orchestrator Summary]: {round.OrchestratorSummary}");
            sb.AppendLine();
        }
        sb.AppendLine("=== END DISCUSSION ===");
        sb.AppendLine();
        sb.AppendLine("Produce a comprehensive final analysis report. Structure the report as follows:");
        sb.AppendLine();
        sb.AppendLine("### Final Opinion Ledger");
        sb.AppendLine("List EVERY opinion from all rounds with its final status (✅ VALID or ❌ INVALID).");
        sb.AppendLine("Format: | Agent | Opinion | Final Status | Reasoning |");
        sb.AppendLine();
        sb.AppendLine("### Validated Conclusions");
        sb.AppendLine("Synthesize only the ✅ VALID opinions into actionable conclusions.");
        sb.AppendLine("Group by theme (pricing, risk, alpha) and provide clear recommendations.");
        sb.AppendLine();
        sb.AppendLine("### Rejected Opinions");
        sb.AppendLine("List all ❌ INVALID opinions with the reason they were rejected.");
        sb.AppendLine();
        sb.AppendLine("### Recommendations");
        sb.AppendLine("Provide actionable recommendations based solely on validated opinions.");

        return sb.ToString();
    }
}

internal static class StringExtensions
{
    public static string Repeat(this char c, int count) => new(c, count);
}
