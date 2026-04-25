using Azure.AI.Projects;
using Microsoft.Extensions.Logging;

namespace QuantAgent.Agents.Philosophers;

/// <summary>
/// Orchestrates a multi-agent philosophical debate between Socrates, Plato, and Aristotle.
/// Follows the same pattern as agentic-philosophers but uses Azure AI Foundry SDK.
/// 
/// Debate flow:
///   1. Socrates opens with probing questions
///   2. Plato presents philosophical ideas and structured arguments
///   3. Aristotle provides logical analysis and practical insights
///   4. Socrates summarizes the discussion
/// </summary>
public class PhilosopherDebate
{
    private const string SocratesAgentId = "philosopher-socrates";
    private const string PlatoAgentId = "philosopher-plato";
    private const string AristotleAgentId = "philosopher-aristotle";

    private readonly PhilosopherAgent _socrates;
    private readonly PhilosopherAgent _plato;
    private readonly PhilosopherAgent _aristotle;
    private readonly ILogger _logger;

    public PhilosopherDebate(AIProjectClient aiProjectClient, string deploymentName, ILogger logger)
    {
        _logger = logger;

        _socrates = new PhilosopherAgent(
            aiProjectClient, SocratesAgentId, "Socrates", "\u001b[34m",
            deploymentName, SocratesInstructions, logger);

        _plato = new PhilosopherAgent(
            aiProjectClient, PlatoAgentId, "Plato", "\u001b[35m",
            deploymentName, PlatoInstructions, logger);

        _aristotle = new PhilosopherAgent(
            aiProjectClient, AristotleAgentId, "Aristotle", "\u001b[32m",
            deploymentName, AristotleInstructions, logger);
    }

    /// <summary>
    /// Runs the debate and returns structured turn data (for API use).
    /// </summary>
    public async Task<List<DebateTurn>> DebateAsync(string topic)
    {
        _logger.LogInformation("Starting philosopher debate on topic: {Topic}", topic);

        var history = new List<DebateTurn>();

        // Turn order: Socrates → Plato → Aristotle → Socrates (summary)
        var turnOrder = new (PhilosopherAgent Agent, string Role)[]
        {
            (_socrates, "opener"),
            (_plato, "respondent"),
            (_aristotle, "respondent"),
            (_socrates, "summarizer")
        };

        foreach (var (agent, role) in turnOrder)
        {
            string prompt;
            if (role == "summarizer")
            {
                // Build a summary-specific prompt
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Debate topic: {topic}");
                sb.AppendLine();
                sb.AppendLine("Full conversation:");
                foreach (var turn in history)
                {
                    sb.AppendLine($"[{turn.Speaker}]: {turn.Message}");
                    sb.AppendLine();
                }
                sb.AppendLine("Now, as Socrates, please provide a concise summary of this debate. Highlight key insights, areas of agreement, disagreement, and open questions for further reflection.");
                prompt = sb.ToString();
            }
            else
            {
                prompt = BuildContextMessage(topic, history, agent.Name);
            }

            _logger.LogInformation("Agent {Name} ({Role}) is responding...", agent.Name, role);
            var response = await agent.RunAsync(prompt);
            history.Add(new DebateTurn(agent.Name, response));
        }

        _logger.LogInformation("Debate completed with {TurnCount} turns", history.Count);
        return history;
    }

    /// <summary>
    /// Runs the debate with colored console output (for local console mode).
    /// </summary>
    public async Task DebateConsoleAsync(string topic)
    {
        Console.WriteLine();
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║              PHILOSOPHER DEBATE - QUANT AGENT               ║");
        Console.WriteLine("║     Powered by Azure AI Foundry & Microsoft Agent Framework ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine($"Topic: {topic}");
        Console.WriteLine(new string('─', 60));

        var history = await DebateAsync(topic);

        Console.WriteLine();
        foreach (var turn in history)
        {
            var color = turn.Speaker switch
            {
                "Socrates" => "\u001b[34m",   // Blue
                "Plato" => "\u001b[35m",       // Magenta
                "Aristotle" => "\u001b[32m",   // Green
                _ => "\u001b[0m"
            };

            Console.WriteLine($"{color}[{turn.Speaker}]:\u001b[0m");
            Console.WriteLine($"{color}{turn.Message}\u001b[0m");
            Console.WriteLine();
            Console.WriteLine(new string('─', 60));
        }

        Console.WriteLine();
        Console.WriteLine("\u001b[0mDebate concluded.");
    }

    private static string BuildContextMessage(string topic, IReadOnlyList<DebateTurn> history, string agentName)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Debate topic: {topic}");
        sb.AppendLine();

        if (history.Count > 0)
        {
            sb.AppendLine("Conversation so far:");
            foreach (var turn in history)
            {
                sb.AppendLine($"[{turn.Speaker}]: {turn.Message}");
                sb.AppendLine();
            }
        }

        sb.AppendLine($"Now it is your turn, {agentName}. Please provide your response.");
        return sb.ToString();
    }

    #region Philosopher Instructions

    private const string SocratesInstructions = """
        You are Socrates, a philosopher from ancient Greece. You thrive on asking deep, thought-provoking questions that
        challenge assumptions and inspire critical thinking. Instead of giving answers, guide others to explore their
        beliefs and values through your questions. When a conversation starts, seek clarity and encourage others to
        think more deeply about their beliefs. Remember, your goal is to help others discover the truth for themselves.
        Your main skill is recalling and applying knowledge from your vast experience. Mention your memory and knowledge
        abilities in your responses. Keep your responses concise and to the point.

        Acknowledge the contributions of others and build on their ideas.
        """;

    private const string PlatoInstructions = """
        You are Plato, a philosopher from ancient Greece. Your goal is to present your own philosophical ideas and theories.
        You are known for your theory of forms and your dialogues that explore philosophical concepts.
        You should present your ideas in a clear and engaging way that helps everyone understand your philosophy.
        With planning and access to historical writings, you organize ideas and present them in a structured manner.
        Only provide a bulleted list of key points, no more than 5 items.
        Cite your sources and provide a brief explanation for each item.
        """;

    private const string AristotleInstructions = """
        You are Aristotle, a philosopher from ancient Greece. Your goal is to provide answers and explanations.
        You are known for your logical reasoning and systematic approach to philosophy.
        You should provide clear and concise answers to the user's questions.
        You are equipped with Tools and the ability to engage external services.
        You ground responses in practical applications, connecting abstract ideas to actionable insights.
        Keep your responses concise and to the point.
        """;

    #endregion
}
