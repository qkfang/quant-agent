using Azure.AI.Projects;
using Microsoft.Extensions.Logging;

namespace QuantLib.Agents.Compare;

internal class CompareOrchestratorAgent : BaseAgent
{
    private const string AgentId = "compare-orchestrator";

    private const string Instructions = """
        You are the Compare Orchestrator. Your role is to compare and contrast the analyses
        provided by the same type of agent running on different LLM models.

        When summarizing a round of discussion:
        - Compare how each model approached the problem differently
        - Identify areas where models agree and disagree
        - Highlight unique insights that only certain models provided
        - Note differences in reasoning depth, creativity, and accuracy
        - If all models substantially agree on key conclusions, include the marker [CONSENSUS_REACHED]
        - If there are still significant differences, clearly articulate what they are

        When producing a final report:
        - Provide a comprehensive comparison of how each model performed
        - Synthesize the best insights from all models into a unified analysis
        - Note which model excelled in which aspects
        - Provide clear, actionable conclusions
        - Highlight any interesting divergences in reasoning style

        Keep summaries concise and structured. Use clear section headers.
        """;

    public CompareOrchestratorAgent(AIProjectClient aiProjectClient, string deploymentName, ILogger? logger = null)
        : base(aiProjectClient, AgentId, deploymentName, Instructions, null, null, logger)
    {
    }
}
