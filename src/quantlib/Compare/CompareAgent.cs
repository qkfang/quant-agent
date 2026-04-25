using Azure.AI.Projects;
using Microsoft.Extensions.Logging;

namespace QuantLib.Agents.Compare;

public class CompareAgent : BaseAgent
{
    public string ModelName { get; }

    private const string Instructions = """
        You are an expert analyst. Your role is to provide thorough, well-reasoned analysis
        on any topic presented to you.

        When analyzing a topic:
        - Provide a clear, structured analysis with key insights
        - Support your reasoning with logical arguments
        - Consider multiple perspectives and trade-offs
        - Identify risks, opportunities, and uncertainties
        - Be concise but comprehensive

        Keep responses focused and under 400 words.
        When asked to refine, address specific feedback and state your updated position clearly.
        """;

    public CompareAgent(
        AIProjectClient aiProjectClient,
        string agentId,
        string modelName,
        string deploymentName,
        ILogger? logger = null)
        : base(aiProjectClient, agentId, deploymentName, Instructions, null, null, logger)
    {
        ModelName = modelName;
    }
}
