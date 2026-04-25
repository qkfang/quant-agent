using System.Runtime.CompilerServices;
using System.Text;
using Azure.AI.Projects;
using Microsoft.Extensions.Logging;
using QuantLib.Agents;
using QuantLib.Agents.Quants;

namespace QuantLib.Agents.Turn;

public class TurnOrchestrator
{
    private const int MaxRounds = 10;

    private readonly QuantAgent[] _agents;
    private readonly TurnOrchestratorAgent _orchestrator;
    private readonly ILogger _logger;

    public TurnOrchestrator(AIProjectClient aiProjectClient, string deploymentName, ILogger logger, string? searchConnectionId = null, string? searchIndexName = null, string? bingConnectionId = null)
    {
        _logger = logger;

        _agents =
        [
            new AlphaQuantAgent(aiProjectClient, deploymentName, searchConnectionId, searchIndexName, bingConnectionId, logger),
            new PricingQuantAgent(aiProjectClient, deploymentName, searchConnectionId, searchIndexName, bingConnectionId, logger),
            new RiskQuantAgent(aiProjectClient, deploymentName, searchConnectionId, searchIndexName, bingConnectionId, logger)
        ];
        _orchestrator = new TurnOrchestratorAgent(aiProjectClient, deploymentName, logger);
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

            var responses = new List<TurnResponse>();
            string orchestratorGuidance = "";

            for (int i = 0; i < _agents.Length; i++)
            {
                var agent = _agents[i];
                cancellationToken.ThrowIfCancellationRequested();

                if (i > 0)
                {
                    var guidancePrompt = BuildStepGuidancePrompt(userInput, rounds, responses, agent.Name, agent.Specialty, round);
                    var guidanceResult = await _orchestrator.RunAsync(guidancePrompt);
                    orchestratorGuidance = guidanceResult.Text;
                }

                string inputDesc = BuildInputDescription(round, i, userInput, orchestratorGuidance, rounds);
                yield return AgentEvent.Started(round, agent.Name, agent.Specialty, inputDesc);

                _logger.LogInformation("Agent {Name} ({Specialty}) is analyzing...", agent.Name, agent.Specialty);
                string agentPrompt = BuildAgentPrompt(userInput, rounds, responses, agent.Specialty, orchestratorGuidance);
                var agentText = new StringBuilder();
                var citations = new List<SearchCitation>();
                await foreach (var delta in agent.RunStreamingAsync(agentPrompt, cancellationToken, citations))
                {
                    agentText.Append(delta);
                    yield return AgentEvent.Delta(round, agent.Name, agent.Specialty, delta);
                }
                _logger.LogInformation("Agent {Name} completed analysis.", agent.Name);

                responses.Add(new TurnResponse(agent.Name, agent.Specialty, agentText.ToString()));
                yield return AgentEvent.Completed(round, agent.Name, agent.Specialty, agentText.ToString(), citations);
            }

            string orchestratorPrompt = BuildOrchestratorPrompt(userInput, rounds, responses, round);
            var summaryText = new StringBuilder();
            await foreach (var delta in _orchestrator.RunStreamingAsync(orchestratorPrompt, cancellationToken))
            {
                summaryText.Append(delta);
                yield return AgentEvent.OrchestratorDeltaEvent(round, delta);
            }
            var summary = summaryText.ToString();
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
        var finalText = new StringBuilder();
        await foreach (var delta in _orchestrator.RunStreamingAsync(finalPrompt, cancellationToken))
        {
            finalText.Append(delta);
            yield return AgentEvent.FinalReportDeltaEvent(delta);
        }
        yield return AgentEvent.FinalCompleted(finalText.ToString());
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

            var responses = new List<TurnResponse>();
            string orchestratorGuidance = "";

            for (int i = 0; i < _agents.Length; i++)
            {
                var agent = _agents[i];

                if (i > 0)
                {
                    Console.WriteLine("  \u001b[33m⟶ Orchestrator providing guidance for next agent...\u001b[0m");
                    Console.WriteLine();

                    var guidancePrompt = BuildStepGuidancePrompt(userInput, rounds, responses, agent.Name, agent.Specialty, round);
                    var guidanceResult = await _orchestrator.RunAsync(guidancePrompt);
                    orchestratorGuidance = guidanceResult.Text;

                    Console.WriteLine($"  \u001b[33m┌─ [Orchestrator → {agent.Name}] ─┐\u001b[0m");
                    Console.WriteLine($"  \u001b[33m{orchestratorGuidance}\u001b[0m");
                    Console.WriteLine($"  \u001b[33m└{'─'.Repeat(40)}┘\u001b[0m");
                    Console.WriteLine();
                }

                Console.WriteLine($"  ⟶ Running {agent.Name} ({agent.Specialty})...");
                Console.WriteLine();

                string agentPrompt = BuildAgentPrompt(userInput, rounds, responses, agent.Specialty, orchestratorGuidance);
                var result = await agent.RunAsync(agentPrompt);
                responses.Add(new TurnResponse(agent.Name, agent.Specialty, result.Text, result.Citations));

                var color = agent.Name switch
                {
                    "Alpha Quant" => "\u001b[32m",
                    "Pricing Quant" => "\u001b[34m",
                    "Risk Quant" => "\u001b[35m",
                    _ => "\u001b[0m"
                };
                Console.WriteLine($"  {color}┌─ [{agent.Name}] ({agent.Specialty}) ─┐\u001b[0m");
                Console.WriteLine($"  {color}{result.Text}\u001b[0m");
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

    private static string BuildInputDescription(int round, int agentIndex, string userInput, string orchestratorGuidance, IReadOnlyList<TurnRound> previousRounds)
    {
        if (agentIndex == 0 && round == 1)
            return $"Initial analysis: {Truncate(userInput, 200)}";

        if (!string.IsNullOrEmpty(orchestratorGuidance))
            return $"Orchestrator guidance: {Truncate(orchestratorGuidance, 300)}";

        if (previousRounds.Count > 0)
            return $"Refine analysis based on turn {round - 1} feedback: {Truncate(previousRounds[^1].OrchestratorSummary, 200)}";

        return $"Analyzing: {Truncate(userInput, 200)}";
    }

    private static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;
        if (text.Length <= maxLength)
            return text;
        return text[..maxLength] + "...";
    }

    private static string BuildAgentPrompt(string userInput, IReadOnlyList<TurnRound> previousRounds,
        IReadOnlyList<TurnResponse> currentResponses, string specialty, string orchestratorGuidance)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"User request: {userInput}");
        sb.AppendLine();

        if (previousRounds.Count > 0)
        {
            sb.AppendLine("=== PREVIOUS TURNS ===");
            foreach (var round in previousRounds)
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
            sb.AppendLine("=== END PREVIOUS TURNS ===");
            sb.AppendLine();
        }

        if (currentResponses.Count > 0)
        {
            sb.AppendLine("=== CURRENT TURN RESPONSES SO FAR ===");
            foreach (var resp in currentResponses)
            {
                sb.AppendLine($"[{resp.AgentName} - {resp.Specialty}]: {resp.Message}");
                sb.AppendLine();
            }
            sb.AppendLine("=== END CURRENT RESPONSES ===");
            sb.AppendLine();
        }

        if (!string.IsNullOrEmpty(orchestratorGuidance))
        {
            sb.AppendLine("=== ORCHESTRATOR GUIDANCE ===");
            sb.AppendLine(orchestratorGuidance);
            sb.AppendLine("=== END GUIDANCE ===");
            sb.AppendLine();
            sb.AppendLine($"Follow the orchestrator's guidance above. Provide your analysis from your specialty perspective ({specialty}).");
        }
        else if (currentResponses.Count > 0)
        {
            sb.AppendLine($"Based on the analyses above, provide your perspective from your specialty ({specialty}).");
            sb.AppendLine("Build upon, validate, or challenge the previous agents' views.");
        }
        else if (previousRounds.Count > 0)
        {
            sb.AppendLine($"Based on the previous turns and orchestrator feedback, refine your analysis from your specialty perspective ({specialty}).");
        }
        else
        {
            sb.AppendLine($"Provide your initial analysis from your specialty perspective ({specialty}).");
        }

        return sb.ToString();
    }

    private static string BuildStepGuidancePrompt(string userInput, IReadOnlyList<TurnRound> previousRounds,
        IReadOnlyList<TurnResponse> currentResponses, string nextAgentName, string nextAgentSpecialty, int currentRound)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"User request: {userInput}");
        sb.AppendLine($"Current turn: {currentRound}/{MaxRounds}");
        sb.AppendLine();
        sb.AppendLine($"The next agent to analyze is: {nextAgentName} ({nextAgentSpecialty})");
        sb.AppendLine();

        if (previousRounds.Count > 0)
        {
            sb.AppendLine("=== PREVIOUS TURNS ===");
            foreach (var round in previousRounds)
            {
                sb.AppendLine($"--- Turn {round.RoundNumber} ---");
                foreach (var resp in round.Responses)
                {
                    sb.AppendLine($"[{resp.AgentName}]: {Truncate(resp.Message, 300)}");
                }
                sb.AppendLine($"[Summary]: {Truncate(round.OrchestratorSummary, 300)}");
            }
            sb.AppendLine("=== END PREVIOUS TURNS ===");
            sb.AppendLine();
        }

        sb.AppendLine("=== CURRENT TURN RESPONSES SO FAR ===");
        foreach (var resp in currentResponses)
        {
            sb.AppendLine($"[{resp.AgentName} - {resp.Specialty}]: {resp.Message}");
            sb.AppendLine();
        }
        sb.AppendLine("=== END CURRENT RESPONSES ===");
        sb.AppendLine();

        sb.AppendLine($"Based on the analysis so far, provide focused guidance for {nextAgentName} ({nextAgentSpecialty}).");
        sb.AppendLine("What specific aspects should they focus on? What gaps need to be addressed from their specialty?");
        sb.AppendLine("Keep your guidance concise (2-3 sentences).");

        return sb.ToString();
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
        sb.AppendLine("Each agent was guided by you and built upon the previous agent's analysis.");
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
