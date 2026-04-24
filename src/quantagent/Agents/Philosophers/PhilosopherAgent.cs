using Azure.AI.Projects;
using Microsoft.Extensions.Logging;

namespace QuantAgent.Agents.Philosophers;

/// <summary>
/// A philosopher agent that participates in multi-agent debates.
/// Each philosopher has a unique persona defined by their name and instructions.
/// </summary>
public class PhilosopherAgent : BaseAgent
{
    public string Name { get; }
    public string ConsoleColor { get; }

    public PhilosopherAgent(
        AIProjectClient aiProjectClient,
        string agentId,
        string name,
        string consoleColor,
        string deploymentName,
        string instructions,
        ILogger? logger = null)
        : base(aiProjectClient, agentId, deploymentName, instructions, null, null, logger)
    {
        Name = name;
        ConsoleColor = consoleColor;
    }
}
