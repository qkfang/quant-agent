using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using Azure.AI.Projects;
using Microsoft.Extensions.Logging;

namespace QuantLib.Agents.Quants;

public class DebateOrchestrator
{
    private const int MinRounds = 3;
    private const int MaxRounds = 5;

    private readonly PricingQuantAgent _pricingQuant;
    private readonly RiskQuantAgent _riskQuant;
    private readonly AlphaQuantAgent _alphaQuant;
    private readonly DebateOrchestratorAgent _orchestrator;
    private readonly ILogger _logger;

    public DebateOrchestrator(AIProjectClient aiProjectClient, string deploymentName, ILogger logger, string? searchConnectionId = null, string? searchIndexName = null, string? bingConnectionId = null)
    {
        _logger = logger;

        _pricingQuant = new PricingQuantAgent(aiProjectClient, deploymentName, searchConnectionId, searchIndexName, bingConnectionId, logger);
        _riskQuant = new RiskQuantAgent(aiProjectClient, deploymentName, searchConnectionId, searchIndexName, bingConnectionId, logger);
        _alphaQuant = new AlphaQuantAgent(aiProjectClient, deploymentName, searchConnectionId, searchIndexName, bingConnectionId, logger);
        _orchestrator = new DebateOrchestratorAgent(aiProjectClient, deploymentName, logger);
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
            var channel = Channel.CreateUnbounded<AgentEvent>();
            QuantAgent[] allAgents = [_pricingQuant, _riskQuant, _alphaQuant];

            var tasks = allAgents.Select(agent => Task.Run(async () =>
            {
                var text = new StringBuilder();
                var citations = new List<SearchCitation>();
                string prompt = DebateAgentExecutorBase.BuildPrompt(roundInput, agent.Specialty);
                await foreach (var delta in agent.RunStreamingAsync(prompt, cancellationToken, citations))
                {
                    text.Append(delta);
                    await channel.Writer.WriteAsync(AgentEvent.Delta(round, agent.Name, agent.Specialty, delta), cancellationToken);
                }
                await channel.Writer.WriteAsync(AgentEvent.Completed(round, agent.Name, agent.Specialty, text.ToString(), citations), cancellationToken);
            }, cancellationToken)).ToArray();

            _ = Task.WhenAll(tasks).ContinueWith(
                t => channel.Writer.TryComplete(t.Exception?.InnerException),
                TaskScheduler.Default);

            var responses = new List<DebateResponse>();
            await foreach (var agentEvent in channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return agentEvent;
                if (agentEvent.Type == AgentEventType.AgentCompleted)
                    responses.Add(new DebateResponse(agentEvent.AgentName, agentEvent.Specialty, agentEvent.Message));
            }
            await Task.WhenAll(tasks);

            string orchestratorPrompt = BuildOrchestratorPrompt(userInput, rounds, responses, round);
            var summaryText = new StringBuilder();
            await foreach (var delta in _orchestrator.RunStreamingAsync(orchestratorPrompt, cancellationToken))
            {
                summaryText.Append(delta);
                yield return AgentEvent.OrchestratorDeltaEvent(round, delta);
            }
            var summary = summaryText.ToString();
            rounds.Add(new DebateRound(round, responses, summary));

            yield return AgentEvent.Summary(round, summary);

            if (round >= MinRounds && summary.Contains("[CONSENSUS_REACHED]", StringComparison.OrdinalIgnoreCase))
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
        var finalText = new StringBuilder();
        await foreach (var delta in _orchestrator.RunStreamingAsync(finalPrompt, cancellationToken))
        {
            finalText.Append(delta);
            yield return AgentEvent.FinalReportDeltaEvent(delta);
        }
        yield return AgentEvent.FinalCompleted(finalText.ToString());
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
            var channel = Channel.CreateUnbounded<DebateResponse>();
            QuantAgent[] allAgents = [_pricingQuant, _riskQuant, _alphaQuant];
            var tasks = allAgents.Select(agent => Task.Run(async () =>
            {
                string prompt = DebateAgentExecutorBase.BuildPrompt(roundInput, agent.Specialty);
                var result = await agent.RunAsync(prompt);
                await channel.Writer.WriteAsync(new DebateResponse(agent.Name, agent.Specialty, result.Text, result.Citations));
            })).ToArray();
            _ = Task.WhenAll(tasks).ContinueWith(
                t => channel.Writer.TryComplete(t.Exception?.InnerException),
                TaskScheduler.Default);
            var responses = new List<DebateResponse>();
            await foreach (var response in channel.Reader.ReadAllAsync())
            {
                responses.Add(response);
            }
            await Task.WhenAll(tasks);

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

            var summaryResult = await _orchestrator.RunAsync(orchestratorPrompt);
            var summary = summaryResult.Text;

            rounds.Add(new DebateRound(round, responses, summary));

            Console.WriteLine($"  \u001b[33m┌─ [Orchestrator Summary - Round {round}] ─┐\u001b[0m");
            Console.WriteLine($"  \u001b[33m{summary}\u001b[0m");
            Console.WriteLine($"  \u001b[33m└{'─'.Repeat(40)}┘\u001b[0m");
            Console.WriteLine();

            if (round >= MinRounds && summary.Contains("[CONSENSUS_REACHED]", StringComparison.OrdinalIgnoreCase))
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
        var finalResult = await _orchestrator.RunAsync(finalPrompt);

        Console.WriteLine($"\u001b[36m{finalResult.Text}\u001b[0m");
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
        if (currentRound < MinRounds)
        {
            sb.AppendLine($"IMPORTANT: A minimum of {MinRounds} rounds is required before consensus can be declared. Do NOT include the [CONSENSUS_REACHED] marker in this round ({currentRound}/{MinRounds}). Continue to challenge opinions and surface gaps.");
        }
        else
        {
            sb.AppendLine("If all key opinions are marked VALID and agents agree, include the exact marker [CONSENSUS_REACHED] in your response.");
        }
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
