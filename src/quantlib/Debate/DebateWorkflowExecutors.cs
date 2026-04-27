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
        string prompt = BuildPrompt(input, _agent.Name, _agent.Specialty);
        _logger.LogInformation("Agent {Name} ({Specialty}) is analyzing...", _agent.Name, _agent.Specialty);
        var result = await _agent.RunAsync(prompt);
        _logger.LogInformation("Agent {Name} completed analysis.", _agent.Name);
        return new DebateResponse(_agent.Name, _agent.Specialty, result.Text, result.Citations);
    }

    internal static string BuildPrompt(DebateRoundInput input, string agentName, string specialty)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"You are **{agentName}** ({specialty}). Any prior message in the shared conversation history attributed to \"{agentName}\" is YOUR OWN previous opinion - do not treat it as another agent's view.");
        sb.AppendLine();
        sb.AppendLine($"User request: {input.UserInput}");
        sb.AppendLine();

        if (input.PreviousRounds.Count > 0)
        {
            sb.AppendLine("The previous discussion (responses from all agents and orchestrator summaries) is in the shared conversation history.");
            sb.AppendLine("Before responding, scan the history and identify:");
            sb.AppendLine($"  - Opinions you ({agentName}) authored previously - these are YOURS to defend, refine, or retract.");
            sb.AppendLine("  - Opinions authored by the OTHER agents - these are the ones to validate.");
            sb.AppendLine("  - The orchestrator's feedback may quote or summarize your prior opinions; treat them as yours when the orchestrator attributes them to your name.");
            sb.AppendLine();
            sb.AppendLine($"Refine your analysis from your specialty perspective ({specialty}).");
            sb.AppendLine();
            sb.AppendLine("You MUST do the following:");
            sb.AppendLine("1. **Validate OTHER agents' opinions only:** For each opinion from agents other than yourself, state whether you agree or disagree and provide counter-evidence or supporting evidence. Do NOT re-validate your own prior opinions as if they belonged to someone else.");
            sb.AppendLine("2. **Update your own opinions:** Address the orchestrator's feedback on YOUR prior opinions. Defend, revise, or retract them with new evidence. Each opinion must include evidence and a confidence level.");
            sb.AppendLine();
            sb.AppendLine("Structure your response as:");
            sb.AppendLine("### Validation of Other Agents' Opinions");
            sb.AppendLine("- [Other Agent Name] Opinion [N]: Agree/Disagree - [Your reasoning and evidence]");
            sb.AppendLine();
            sb.AppendLine($"### My ({agentName}) Updated Opinions");
            sb.AppendLine("**Opinion [N]:** [Statement] (New / Revised from prior round / Retracted)");
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

        sb.AppendLine();
        sb.AppendLine($"IMPORTANT: Sign off with your name (\"{agentName}\") and keep your entire response under 150 words.");

        return sb.ToString();
    }
}

internal sealed class PricingQuantExecutor(PricingQuantAgent agent, ILogger logger)
    : DebateAgentExecutorBase("pricing-quant-executor", agent, logger);

internal sealed class RiskQuantExecutor(RiskQuantAgent agent, ILogger logger)
    : DebateAgentExecutorBase("risk-quant-executor", agent, logger);

internal sealed class AlphaQuantExecutor(AlphaQuantAgent agent, ILogger logger)
    : DebateAgentExecutorBase("alpha-quant-executor", agent, logger);
