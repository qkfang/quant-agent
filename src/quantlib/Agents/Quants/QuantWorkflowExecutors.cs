using System.Text;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;

namespace QuantLib.Agents.Quants;

internal sealed class QuantRoundDispatchExecutor()
    : Executor<QuantRoundInput, QuantRoundInput>("quant-round-dispatch")
{
    public override ValueTask<QuantRoundInput> HandleAsync(
        QuantRoundInput message, IWorkflowContext context, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(message);
}

internal abstract class QuantAgentExecutorBase : Executor<QuantRoundInput, QuantResponse>
{
    private readonly QuantAgent _agent;
    private readonly ILogger _logger;

    protected QuantAgentExecutorBase(string id, QuantAgent agent, ILogger logger)
        : base(id)
    {
        _agent = agent;
        _logger = logger;
    }

    public override async ValueTask<QuantResponse> HandleAsync(
        QuantRoundInput input, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        string prompt = BuildPrompt(input, _agent.Specialty);
        _logger.LogInformation("Agent {Name} ({Specialty}) is analyzing...", _agent.Name, _agent.Specialty);
        string response = await _agent.RunAsync(prompt);
        _logger.LogInformation("Agent {Name} completed analysis.", _agent.Name);
        return new QuantResponse(_agent.Name, _agent.Specialty, response);
    }

    private static string BuildPrompt(QuantRoundInput input, string specialty)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"User request: {input.UserInput}");
        sb.AppendLine();

        if (input.PreviousRounds.Count > 0)
        {
            sb.AppendLine("=== PREVIOUS DISCUSSION ===");
            foreach (var round in input.PreviousRounds)
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
            sb.AppendLine($"Based on the previous discussion and orchestrator feedback, please refine your analysis from your specialty perspective ({specialty}).");
            sb.AppendLine("Address any disagreements or gaps identified. State clearly whether you agree or disagree with the other agents' views and why.");
        }
        else
        {
            sb.AppendLine($"Provide your initial analysis from your specialty perspective ({specialty}).");
        }

        return sb.ToString();
    }
}

internal sealed class PricingQuantExecutor(PricingQuantAgent agent, ILogger logger)
    : QuantAgentExecutorBase("pricing-quant-executor", agent, logger);

internal sealed class RiskQuantExecutor(RiskQuantAgent agent, ILogger logger)
    : QuantAgentExecutorBase("risk-quant-executor", agent, logger);

internal sealed class AlphaQuantExecutor(AlphaQuantAgent agent, ILogger logger)
    : QuantAgentExecutorBase("alpha-quant-executor", agent, logger);
