using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Microsoft.Extensions.Logging;

namespace QuantLib.Agents.Quants;

public class QuantAgent : BaseAgent
{
    public string Name { get; }
    public string Specialty { get; }
    public string ConsoleColor { get; }

    public QuantAgent(
        AIProjectClient aiProjectClient,
        string agentId,
        string name,
        string specialty,
        string consoleColor,
        string deploymentName,
        string instructions,
        string? searchConnectionId = null,
        string? searchIndexName = null,
        ILogger? logger = null)
        : base(aiProjectClient, agentId, deploymentName, instructions, null,
            agentDef =>
            {
                if (!string.IsNullOrWhiteSpace(searchConnectionId) && !string.IsNullOrWhiteSpace(searchIndexName))
                    agentDef.Tools.Add(new AzureAISearchTool(new AzureAISearchToolOptions([
                        new AzureAISearchToolIndex { ProjectConnectionId = searchConnectionId, IndexName = searchIndexName }
                    ])));
            },
            logger)
    {
        Name = name;
        Specialty = specialty;
        ConsoleColor = consoleColor;
    }
}
