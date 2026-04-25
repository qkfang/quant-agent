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
        string? knowledgeBaseId = null,
        ILogger? logger = null)
        : base(aiProjectClient, agentId, deploymentName, instructions, null,
            string.IsNullOrWhiteSpace(knowledgeBaseId)
                ? (Action<DeclarativeAgentDefinition>?)null
                : agentDef => agentDef.Tools.Add(new KnowledgeBaseToolDefinition(knowledgeBaseId)),
            logger)
    {
        Name = name;
        Specialty = specialty;
        ConsoleColor = consoleColor;
    }
}
