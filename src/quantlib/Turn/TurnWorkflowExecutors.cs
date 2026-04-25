using System.Text;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;

namespace QuantLib.Agents.Turn;

internal sealed class TurnDispatchExecutor()
    : Executor<TurnInput, TurnState>("quant-turn-dispatch")
{
    public override ValueTask<TurnState> HandleAsync(
        TurnInput message, IWorkflowContext context, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(new TurnState(message.UserInput, message.PreviousRounds, []));
}

internal abstract class TurnAgentExecutor : Executor<TurnState, TurnState>
{
    private readonly QuantAgent _agent;
    private readonly ILogger _logger;

    protected TurnAgentExecutor(string id, QuantAgent agent, ILogger logger)
        : base(id)
    {
        _agent = agent;
        _logger = logger;
    }

    public override async ValueTask<TurnState> HandleAsync(
        TurnState input, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        string prompt = BuildPrompt(input, _agent.Name, _agent.Specialty);
        _logger.LogInformation("Agent {Name} ({Specialty}) is analyzing sequentially...", _agent.Name, _agent.Specialty);
        string response = await _agent.RunAsync(prompt);
        _logger.LogInformation("Agent {Name} completed analysis.", _agent.Name);

        var updatedResponses = new List<TurnResponse>(input.CurrentResponses)
        {
            new(_agent.Name, _agent.Specialty, response)
        };

        return input with { CurrentResponses = updatedResponses };
    }

    private static string BuildPrompt(TurnState state, string agentName, string specialty)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"User request: {state.UserInput}");
        sb.AppendLine();

        if (state.PreviousRounds.Count > 0)
        {
            sb.AppendLine("=== PREVIOUS ROUNDS ===");
            foreach (var round in state.PreviousRounds)
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
            sb.AppendLine("=== END PREVIOUS ROUNDS ===");
            sb.AppendLine();
        }

        if (state.CurrentResponses.Count > 0)
        {
            sb.AppendLine("=== CURRENT ROUND RESPONSES SO FAR ===");
            foreach (var resp in state.CurrentResponses)
            {
                sb.AppendLine($"[{resp.AgentName} - {resp.Specialty}]: {resp.Message}");
                sb.AppendLine();
            }
            sb.AppendLine("=== END CURRENT RESPONSES ===");
            sb.AppendLine();
            sb.AppendLine($"Based on the analyses above, provide your perspective from your specialty ({specialty}).");
            sb.AppendLine("Build upon, validate, or challenge the previous agents' views. State clearly whether you agree or disagree and why.");
        }
        else if (state.PreviousRounds.Count > 0)
        {
            sb.AppendLine($"Based on the previous rounds and orchestrator feedback, refine your analysis from your specialty perspective ({specialty}).");
            sb.AppendLine("Address any disagreements or gaps identified.");
        }
        else
        {
            sb.AppendLine($"Provide your initial analysis from your specialty perspective ({specialty}).");
        }

        return sb.ToString();
    }
}

internal sealed class AlphaQuantTurnExecutor(AlphaQuantAgent agent, ILogger logger)
    : TurnAgentExecutor("alpha-quant-turn-executor", agent, logger);

internal sealed class PricingQuantTurnExecutor(PricingQuantAgent agent, ILogger logger)
    : TurnAgentExecutor("pricing-quant-turn-executor", agent, logger);

internal sealed class RiskQuantTurnExecutor(RiskQuantAgent agent, ILogger logger)
    : TurnAgentExecutor("risk-quant-turn-executor", agent, logger);
