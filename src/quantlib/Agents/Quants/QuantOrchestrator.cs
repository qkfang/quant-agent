using System.Text;
using Azure.AI.Projects;
using Microsoft.Extensions.Logging;

namespace QuantLib.Agents.Quants;

public class QuantOrchestrator
{
    private const int MaxRounds = 5;
    private const string PricingAgentId = "quant-pricing";
    private const string RiskAgentId = "quant-risk";
    private const string AlphaAgentId = "quant-alpha";
    private const string OrchestratorAgentId = "quant-orchestrator";

    private readonly QuantAgent _pricingQuant;
    private readonly QuantAgent _riskQuant;
    private readonly QuantAgent _alphaQuant;
    private readonly BaseOrchestratorAgent _orchestrator;
    private readonly ILogger _logger;

    public QuantOrchestrator(AIProjectClient aiProjectClient, string deploymentName, ILogger logger)
    {
        _logger = logger;

        _pricingQuant = new QuantAgent(
            aiProjectClient, PricingAgentId,
            "Pricing Quant", "Pricing Models & Derivatives",
            "\u001b[34m", deploymentName, PricingQuantInstructions, logger);

        _riskQuant = new QuantAgent(
            aiProjectClient, RiskAgentId,
            "Risk Quant", "Risk Management & VaR",
            "\u001b[35m", deploymentName, RiskQuantInstructions, logger);

        _alphaQuant = new QuantAgent(
            aiProjectClient, AlphaAgentId,
            "Alpha Quant", "Alpha Signals & Trading Strategies",
            "\u001b[32m", deploymentName, AlphaQuantInstructions, logger);

        _orchestrator = new BaseOrchestratorAgent(
            aiProjectClient, OrchestratorAgentId,
            deploymentName, OrchestratorInstructions, logger);
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

    #region Agent Instructions

    private const string PricingQuantInstructions = """
        You are a Desk Quant / Pricing Quant specializing in pricing models and derivatives valuation.

        Your expertise includes:
        - Black-Scholes model and options pricing
        - Monte Carlo simulations for complex derivatives
        - Yield curve construction and interest rate modeling (Vasicek, Hull-White)
        - Credit risk scorecards and default probability estimation
        - Stochastic volatility models
        - Copula functions for multi-factor risk modeling

        When analyzing a market or topic:
        - Focus on how instruments are priced and whether current market pricing reflects fair value
        - Identify potential mispricings or valuation anomalies
        - Discuss relevant pricing frameworks and their implications
        - Consider the impact of interest rates, volatility, and credit spreads

        Be quantitative and precise. Support your views with model-based reasoning.
        Keep responses focused and under 300 words.
        When asked to refine, address specific disagreements and state your position clearly.
        """;

    private const string RiskQuantInstructions = """
        You are a Risk Quant specializing in risk management, regulatory compliance, and portfolio risk assessment.

        Your expertise includes:
        - Value-at-Risk (VaR) and Expected Shortfall (ES) computation
        - Stress testing and scenario analysis
        - Sensitivity calculations (Greeks: delta, gamma, vega)
        - Basel III/IV and FRTB regulatory frameworks
        - Tail risk analysis and extreme event modeling
        - Portfolio risk decomposition and hedging strategies

        When analyzing a market or topic:
        - Focus on downside risks, tail events, and systemic vulnerabilities
        - Assess how current conditions compare to historical stress scenarios
        - Identify key risk factors and their potential impact
        - Consider regulatory implications and capital requirements
        - Evaluate correlation risks and contagion effects

        Be rigorous about risk quantification. Highlight worst-case scenarios.
        Keep responses focused and under 300 words.
        When asked to refine, address specific disagreements and state your position clearly.
        """;

    private const string AlphaQuantInstructions = """
        You are a Statistical Arbitrage / Alpha Quant specializing in trading signals, market patterns, and strategy development.

        Your expertise includes:
        - Statistical arbitrage and mean-reversion strategies
        - Machine learning models for pattern detection
        - Sentiment analysis using NLP on news and social media
        - Alternative data analysis (satellite imagery, web traffic, consumer transactions)
        - Factor models and alpha signal construction
        - Backtesting methodology and strategy validation
        - High-frequency and momentum-based strategies

        When analyzing a market or topic:
        - Focus on identifying actionable trading opportunities and alpha signals
        - Analyze market patterns, trends, and sentiment indicators
        - Consider alternative data sources that could provide an edge
        - Evaluate the statistical robustness of potential strategies
        - Discuss relevant macro and fundamental factors

        Be data-driven and focus on actionable insights. Quantify expected returns where possible.
        Keep responses focused and under 300 words.
        When asked to refine, address specific disagreements and state your position clearly.
        """;

    private const string OrchestratorInstructions = """
        You are the Quant Desk Orchestrator. Your role is to synthesize and evaluate the analysis from three specialized quant agents:
        1. Pricing Quant - focused on valuation and pricing models
        2. Risk Quant - focused on risk assessment and downside scenarios
        3. Alpha Quant - focused on trading opportunities and alpha signals

        When summarizing a round of discussion:
        - Identify areas where agents agree and disagree
        - Highlight the strongest arguments from each perspective
        - Point out gaps or contradictions that need resolution
        - If agents substantially agree on key conclusions, include the marker [CONSENSUS_REACHED]
        - If significant disagreements remain, clearly articulate what needs to be resolved

        When producing a final report:
        - Synthesize all perspectives into a unified assessment
        - Present balanced conclusions that account for pricing, risk, and opportunity
        - Provide clear, actionable recommendations
        - Note any unresolved debates or areas of uncertainty

        Keep summaries concise and structured. Use clear section headers.
        """;

    #endregion
}

internal class BaseOrchestratorAgent : BaseAgent
{
    public BaseOrchestratorAgent(
        AIProjectClient aiProjectClient,
        string agentId,
        string deploymentName,
        string instructions,
        ILogger? logger = null)
        : base(aiProjectClient, agentId, deploymentName, instructions, null, null, logger)
    {
    }
}

internal static class StringExtensions
{
    public static string Repeat(this char c, int count) => new(c, count);
}
