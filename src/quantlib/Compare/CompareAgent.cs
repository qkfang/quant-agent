using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
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
        string? searchConnectionId = null,
        string? searchIndexName = null,
        string? bingConnectionId = null,
        ILogger? logger = null)
        : base(aiProjectClient, agentId, deploymentName, Instructions, null,
            agentDef =>
            {
                if (!string.IsNullOrWhiteSpace(searchConnectionId) && !string.IsNullOrWhiteSpace(searchIndexName))
                    agentDef.Tools.Add(new AzureAISearchTool(new AzureAISearchToolOptions([
                        new AzureAISearchToolIndex { ProjectConnectionId = searchConnectionId, IndexName = searchIndexName }
                    ])));
                if (!string.IsNullOrWhiteSpace(bingConnectionId))
                    agentDef.Tools.Add(new BingGroundingTool(new BingGroundingSearchToolOptions([
                        new BingGroundingSearchConfiguration(bingConnectionId)
                    ])));
            },
            logger)
    {
        ModelName = modelName;
    }
}
