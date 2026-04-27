using System.Text;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;

namespace QuantLib.Agents.Compare;

internal sealed class CompareRoundDispatchExecutor()
    : Executor<CompareRoundInput, CompareRoundInput>("compare-round-dispatch")
{
    public override ValueTask<CompareRoundInput> HandleAsync(
        CompareRoundInput message, IWorkflowContext context, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(message);
}

internal sealed class CompareAgentExecutor : Executor<CompareRoundInput, CompareResponse>
{
    private readonly CompareAgent _agent;
    private readonly ILogger _logger;

    public CompareAgentExecutor(string id, CompareAgent agent, ILogger logger)
        : base(id)
    {
        _agent = agent;
        _logger = logger;
    }

    public override async ValueTask<CompareResponse> HandleAsync(
        CompareRoundInput input, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        string prompt = BuildPrompt(input, _agent.ModelName);
        _logger.LogInformation("Model {Name} is analyzing...", _agent.ModelName);
        var result = await _agent.RunAsync(prompt);
        _logger.LogInformation("Model {Name} completed analysis.", _agent.ModelName);
        return new CompareResponse(_agent.ModelName, _agent.ModelName, result.Text, result.Citations);
    }

    internal static string BuildPrompt(CompareRoundInput input, string modelName)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"User request: {input.UserInput}");
        sb.AppendLine();

        if (input.PreviousRounds.Count > 0)
        {
            sb.AppendLine($"You are responding as model: {modelName}. The previous discussion (responses from all models and orchestrator feedback) is in the shared conversation history.");
            sb.AppendLine("Refine your analysis. Address any disagreements or gaps the orchestrator identified.");
            sb.AppendLine("State clearly whether you agree or disagree with the other models' views and why.");
        }
        else
        {
            sb.AppendLine("Provide your analysis on this topic.");
        }

        return sb.ToString();
    }
}
