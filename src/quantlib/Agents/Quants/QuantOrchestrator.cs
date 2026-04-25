using System.Text;
using Azure.AI.Projects;
using Microsoft.Extensions.Logging;

namespace QuantLib.Agents.Quants;

public class QuantOrchestrator
{
    private const int MaxRounds = 5;

    private readonly PricingQuantAgent _pricingQuant;
    private readonly RiskQuantAgent _riskQuant;
    private readonly AlphaQuantAgent _alphaQuant;
    private readonly QuantOrchestratorAgent _orchestrator;
    private readonly ILogger _logger;

    public QuantOrchestrator(AIProjectClient aiProjectClient, string deploymentName, ILogger logger)
    {
        _logger = logger;

        _pricingQuant = new PricingQuantAgent(aiProjectClient, deploymentName, logger);
        _riskQuant = new RiskQuantAgent(aiProjectClient, deploymentName, logger);
        _alphaQuant = new AlphaQuantAgent(aiProjectClient, deploymentName, logger);
        _orchestrator = new QuantOrchestratorAgent(aiProjectClient, deploymentName, logger);
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

        var rounds = new List<QuantRound>();

        for (int round = 1; round <= MaxRounds; round++)
        {
            Console.WriteLine();
            Console.WriteLine($"\u001b[33m╔══ ROUND {round}/{MaxRounds} ══╗\u001b[0m");
            Console.WriteLine();

            // Build prompts for each quant agent
            string pricingPrompt = BuildQuantPrompt(userInput, rounds, _pricingQuant);
            string riskPrompt = BuildQuantPrompt(userInput, rounds, _riskQuant);
            string alphaPrompt = BuildQuantPrompt(userInput, rounds, _alphaQuant);

            // Fan-out: run all 3 quant agents concurrently
            Console.WriteLine("  ⟶ Dispatching to all quant agents concurrently...");
            Console.WriteLine();

            var pricingTask = RunAgentWithStreamingAsync(_pricingQuant, pricingPrompt);
            var riskTask = RunAgentWithStreamingAsync(_riskQuant, riskPrompt);
            var alphaTask = RunAgentWithStreamingAsync(_alphaQuant, alphaPrompt);

            var results = await Task.WhenAll(pricingTask, riskTask, alphaTask);

            var responses = new List<QuantResponse>
            {
                new(_pricingQuant.Name, _pricingQuant.Specialty, results[0]),
                new(_riskQuant.Name, _riskQuant.Specialty, results[1]),
                new(_alphaQuant.Name, _alphaQuant.Specialty, results[2])
            };

            // Stream each agent's response to console
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

            // Orchestrator: summarize, check consensus, decide next step
            string orchestratorPrompt = BuildOrchestratorPrompt(userInput, rounds, responses, round);
            Console.WriteLine("  \u001b[33m⟶ Orchestrator summarizing and evaluating consensus...\u001b[0m");
            Console.WriteLine();

            var summary = await _orchestrator.RunAsync(orchestratorPrompt);

            rounds.Add(new QuantRound(round, responses, summary));

            Console.WriteLine($"  \u001b[33m┌─ [Orchestrator Summary - Round {round}] ─┐\u001b[0m");
            Console.WriteLine($"  \u001b[33m{summary}\u001b[0m");
            Console.WriteLine($"  \u001b[33m└{'─'.Repeat(40)}┘\u001b[0m");
            Console.WriteLine();

            // Check if orchestrator indicates consensus
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

        // Final output
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

    private async Task<string> RunAgentWithStreamingAsync(QuantAgent agent, string prompt)
    {
        _logger.LogInformation("Agent {Name} ({Specialty}) is analyzing...", agent.Name, agent.Specialty);
        var response = await agent.RunAsync(prompt);
        _logger.LogInformation("Agent {Name} completed analysis.", agent.Name);
        return response;
    }

    private static string BuildQuantPrompt(string userInput, IReadOnlyList<QuantRound> previousRounds, QuantAgent agent)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"User request: {userInput}");
        sb.AppendLine();

        if (previousRounds.Count > 0)
        {
            sb.AppendLine("=== PREVIOUS DISCUSSION ===");
            foreach (var round in previousRounds)
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
            sb.AppendLine("=== END PREVIOUS DISCUSSION ===");
            sb.AppendLine();
            sb.AppendLine($"Based on the previous discussion and orchestrator feedback, please refine your analysis from your specialty perspective ({agent.Specialty}).");
            sb.AppendLine("Address any disagreements or gaps identified. State clearly whether you agree or disagree with the other agents' views and why.");
        }
        else
        {
            sb.AppendLine($"Provide your initial analysis from your specialty perspective ({agent.Specialty}).");
        }

        return sb.ToString();
    }

    private static string BuildOrchestratorPrompt(string userInput, IReadOnlyList<QuantRound> previousRounds, IReadOnlyList<QuantResponse> currentResponses, int currentRound)
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
        sb.AppendLine("Summarize the views from all agents. Identify areas of agreement, disagreement, and gaps.");
        sb.AppendLine("If all agents substantially agree on the key conclusions, include the exact marker [CONSENSUS_REACHED] in your response.");
        sb.AppendLine("If there are still significant disagreements or gaps, identify them clearly so agents can address them in the next round.");

        return sb.ToString();
    }

    private static string BuildFinalSummaryPrompt(string userInput, IReadOnlyList<QuantRound> allRounds)
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
        sb.AppendLine("Produce a comprehensive final analysis report for the user. Synthesize all perspectives (pricing, risk, alpha) into a cohesive assessment. Include key findings, risk considerations, opportunities, and actionable recommendations.");

        return sb.ToString();
    }

}

internal static class StringExtensions
{
    public static string Repeat(this char c, int count) => new(c, count);
}
