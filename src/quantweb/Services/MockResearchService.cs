using System.Runtime.CompilerServices;
using quantweb.Models;

namespace quantweb.Services;

public class MockResearchService : IResearchService
{
    public async IAsyncEnumerable<ResearchEvent> StreamResearchAsync(
        string topic,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Round 1
        yield return Evt("RoundStarted", 1);
        await Task.Delay(300, cancellationToken);

        yield return Evt("AgentStarted", 1, "Pricing Quant", "Pricing Models & Derivatives",
            inputMessage: $"Provide initial analysis on: {topic}");
        yield return Evt("AgentStarted", 1, "Risk Quant", "Risk Management & VaR",
            inputMessage: $"Provide initial analysis on: {topic}");
        yield return Evt("AgentStarted", 1, "Alpha Quant", "Alpha Signals & Trading Strategies",
            inputMessage: $"Provide initial analysis on: {topic}");
        await Task.Delay(1500, cancellationToken);

        yield return Evt("AgentCompleted", 1, "Pricing Quant", "Pricing Models & Derivatives",
            "Current implied volatility surfaces suggest moderate overpricing in near-term contracts. " +
            "DCF models indicate fair value approximately 8-12% below current levels when using a risk-adjusted " +
            "discount rate. The yield curve inversion signals potential valuation compression ahead.");
        await Task.Delay(800, cancellationToken);

        yield return Evt("AgentCompleted", 1, "Risk Quant", "Risk Management & VaR",
            "VaR analysis at 99% confidence shows tail risk has increased 40% over the past quarter. " +
            "Stress testing under historical scenarios (similar to prior downturns) indicates potential drawdowns of 15-25%. " +
            "Correlation risk is elevated across sectors, reducing diversification benefits.");
        await Task.Delay(600, cancellationToken);

        yield return Evt("AgentCompleted", 1, "Alpha Quant", "Alpha Signals & Trading Strategies",
            "Momentum signals remain positive on a 3-month basis but show divergence at the 1-month horizon. " +
            "Sentiment analysis from news sources indicates shifting narrative towards caution. " +
            "Mean-reversion signals suggest a potential entry point if prices decline 5-7% from current levels.");
        await Task.Delay(1000, cancellationToken);

        yield return Evt("OrchestratorSummary", 1, "Orchestrator", "",
            "**Areas of Agreement:** All agents note elevated risk conditions and potential overvaluation in current markets.\n\n" +
            "**Areas of Disagreement:** Pricing Quant sees 8-12% overvaluation while Alpha Quant suggests only a 5-7% correction is needed for entry. " +
            "Risk Quant emphasizes downside scenarios more aggressively.\n\n" +
            "**Gaps:** No discussion of sector rotation opportunities or hedging instrument recommendations.\n\n" +
            "**Debate Questions:**\n" +
            "1. How do you reconcile the Pricing Quant's 8-12% overvaluation with the Alpha Quant's 5-7% entry target?\n" +
            "2. What specific hedging strategies would mitigate the tail risk identified by Risk Quant while preserving upside?\n" +
            "3. Are there sector-specific opportunities that diverge from the broad market assessment?");
        await Task.Delay(500, cancellationToken);

        // Round 2
        yield return Evt("RoundStarted", 2);
        await Task.Delay(300, cancellationToken);

        var round2Input = "Refine your analysis. Orchestrator feedback:\n" +
            "Reconcile valuation gap (8-12% vs 5-7%), propose hedging strategies, identify sector-specific opportunities.";

        yield return Evt("AgentStarted", 2, "Pricing Quant", "Pricing Models & Derivatives",
            inputMessage: round2Input);
        yield return Evt("AgentStarted", 2, "Risk Quant", "Risk Management & VaR",
            inputMessage: round2Input);
        yield return Evt("AgentStarted", 2, "Alpha Quant", "Alpha Signals & Trading Strategies",
            inputMessage: round2Input);
        await Task.Delay(1800, cancellationToken);

        yield return Evt("AgentCompleted", 2, "Pricing Quant", "Pricing Models & Derivatives",
            "Adjusting for sector-specific dynamics, I refine my estimate: broad overvaluation is 6-10%, " +
            "closer to Alpha Quant's range for defensive sectors. Growth sectors remain 10-15% overpriced. " +
            "Recommend put spreads on broad indices as cost-effective hedging, costing approximately 1.5% of portfolio value.");
        await Task.Delay(700, cancellationToken);

        yield return Evt("AgentCompleted", 2, "Risk Quant", "Risk Management & VaR",
            "I agree with the refined valuation range. For hedging: collar strategies on concentrated positions provide " +
            "downside protection while capping upside at 8%. Defensive sectors (utilities, healthcare) show 30% lower " +
            "beta-adjusted risk. I recommend reducing growth exposure by 15-20% and rotating into defensive positions.");
        await Task.Delay(900, cancellationToken);

        yield return Evt("AgentCompleted", 2, "Alpha Quant", "Alpha Signals & Trading Strategies",
            "I concur with the refined 6-10% range. Sector-level alpha signals: defensive sectors show positive momentum " +
            "divergence. Infrastructure and energy show mean-reversion opportunities. Recommend a pairs trade: " +
            "long defensive/short growth with 2:1 ratio. Backtesting shows 65% hit rate in similar environments.");
        await Task.Delay(1000, cancellationToken);

        yield return Evt("OrchestratorSummary", 2, "Orchestrator", "",
            "**Convergence achieved.** All agents now agree on a 6-10% overvaluation range with sector differentiation.\n\n" +
            "**Agreed Recommendations:**\n" +
            "- Reduce growth sector exposure by 15-20%\n" +
            "- Implement put spreads for portfolio hedging (~1.5% cost)\n" +
            "- Rotate into defensive sectors showing momentum divergence\n" +
            "- Consider pairs trading (long defensive/short growth)\n\n" +
            "[CONSENSUS_REACHED]");
        await Task.Delay(300, cancellationToken);

        yield return Evt("ConsensusReached", 2, "Orchestrator");
        await Task.Delay(300, cancellationToken);

        // Final report
        yield return Evt("FinalReportStarted", 0, "Orchestrator");
        await Task.Delay(1500, cancellationToken);

        yield return Evt("FinalReportCompleted", 0, "Orchestrator", "",
            "**Comprehensive Analysis Report**\n\n" +
            "**Executive Summary:** Multi-perspective analysis indicates current markets are 6-10% overvalued " +
            "with elevated tail risk. Consensus recommendation is a defensive repositioning with targeted hedging.\n\n" +
            "**Valuation Assessment (Pricing Perspective):**\n" +
            "Broad market overvaluation of 6-10%, with growth sectors at 10-15% premium to fair value. " +
            "Defensive sectors are near fair value. DCF models and implied volatility analysis both confirm this range.\n\n" +
            "**Risk Assessment (Risk Perspective):**\n" +
            "99th percentile VaR has increased 40% quarter-over-quarter. Tail risk scenarios indicate 15-25% " +
            "potential drawdowns. Cross-sector correlation at elevated levels reduces diversification benefits.\n\n" +
            "**Opportunity Assessment (Alpha Perspective):**\n" +
            "Sector rotation from growth to defensive shows positive alpha signals. Infrastructure and energy " +
            "present mean-reversion opportunities. Pairs trade strategy backtests show 65% success rate.\n\n" +
            "**Actionable Recommendations:**\n" +
            "1. Reduce growth sector allocation by 15-20%\n" +
            "2. Implement index put spreads (~1.5% portfolio cost) for tail risk protection\n" +
            "3. Increase defensive sector allocation (utilities, healthcare)\n" +
            "4. Consider pairs trade: long defensive / short growth at 2:1 ratio\n" +
            "5. Monitor correlation breakdowns for re-entry signals");
    }

    private static ResearchEvent Evt(string type, int round, string agentName = "", string specialty = "", string message = "", string inputMessage = "")
    {
        return new ResearchEvent
        {
            Type = type,
            Round = round,
            AgentName = agentName,
            Specialty = specialty,
            Message = message,
            InputMessage = inputMessage,
            Timestamp = DateTime.UtcNow
        };
    }
}
