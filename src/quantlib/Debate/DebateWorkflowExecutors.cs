using System.Text;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;

namespace QuantLib.Agents.Quants;

internal sealed class DebateRoundDispatchExecutor()
    : Executor<DebateRoundInput, DebateRoundInput>("quant-round-dispatch")
{
    public override ValueTask<DebateRoundInput> HandleAsync(
        DebateRoundInput message, IWorkflowContext context, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(message);
}

internal abstract class DebateAgentExecutorBase : Executor<DebateRoundInput, DebateResponse>
{
    private readonly QuantAgent _agent;
    private readonly ILogger _logger;

    protected DebateAgentExecutorBase(string id, QuantAgent agent, ILogger logger)
        : base(id)
    {
        _agent = agent;
        _logger = logger;
    }

    public override async ValueTask<DebateResponse> HandleAsync(
        DebateRoundInput input, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        string prompt = BuildPrompt(input, _agent.Specialty);
        _logger.LogInformation("Agent {Name} ({Specialty}) is analyzing...", _agent.Name, _agent.Specialty);
        string response = await _agent.RunAsync(prompt);
        _logger.LogInformation("Agent {Name} completed analysis.", _agent.Name);
        return new DebateResponse(_agent.Name, _agent.Specialty, response);
    }

    private static string BuildPrompt(DebateRoundInput input, string specialty)
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
            sb.AppendLine($"Based on the previous discussion and orchestrator feedback, refine your analysis from your specialty perspective ({specialty}).");
            sb.AppendLine();
            sb.AppendLine("You MUST do the following:");
            sb.AppendLine("1. **Validate other agents' opinions:** Review each opinion from other agents. For each, state whether you agree or disagree and provide counter-evidence or supporting evidence.");
            sb.AppendLine("2. **Update your own opinions:** Based on the discussion, revise, add, or remove opinions. Each opinion must include evidence and a confidence level.");
            sb.AppendLine();
            sb.AppendLine("Structure your response as:");
            sb.AppendLine("### Validation of Other Agents' Opinions");
            sb.AppendLine("- [Agent Name] Opinion [N]: Agree/Disagree - [Your reasoning and evidence]");
            sb.AppendLine();
            sb.AppendLine("### My Quant Opinions");
            sb.AppendLine("**Opinion [N]:** [Statement]");
            sb.AppendLine("- **Evidence:** [Supporting evidence]");
            sb.AppendLine("- **Confidence:** [High / Medium / Low]");
        }
        else
        {
            sb.AppendLine($"Provide your initial analysis from your specialty perspective ({specialty}).");
            sb.AppendLine();
            sb.AppendLine("Structure your response as a list of Quant Opinions:");
            sb.AppendLine("**Opinion [N]:** [Your opinion statement]");
            sb.AppendLine("- **Evidence:** [Supporting evidence and reasoning]");
            sb.AppendLine("- **Confidence:** [High / Medium / Low]");
        }

        return sb.ToString();
    }
}

internal sealed class PricingQuantExecutor(PricingQuantAgent agent, ILogger logger)
    : DebateAgentExecutorBase("pricing-quant-executor", agent, logger);

internal sealed class RiskQuantExecutor(RiskQuantAgent agent, ILogger logger)
    : DebateAgentExecutorBase("risk-quant-executor", agent, logger);

internal sealed class AlphaQuantExecutor(AlphaQuantAgent agent, ILogger logger)
    : DebateAgentExecutorBase("alpha-quant-executor", agent, logger);
