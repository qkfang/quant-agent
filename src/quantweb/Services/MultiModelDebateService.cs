using System.Runtime.CompilerServices;
using Azure.Identity;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.ChatCompletion;
using quantagent_web.Models;

namespace quantagent_web.Services;

/// <summary>
/// Orchestrates multi-model debates using Semantic Kernel agents.
/// Each agent represents a different analyst perspective (Bull, Bear, Risk, Macro)
/// that reason with each other to build an end-to-end view of potential outcomes.
/// </summary>
public class MultiModelDebateService : IMultiModelDebateService
{
    private const string BullAnalystName = "Bull Analyst";
    private const string BearAnalystName = "Bear Analyst";
    private const string RiskAnalystName = "Risk Analyst";
    private const string MacroStrategistName = "Macro Strategist";
    private const string ModeratorName = "Moderator";

    private static readonly IReadOnlyList<string> AgentNames = new[]
    {
        BullAnalystName, BearAnalystName, RiskAnalystName, MacroStrategistName
    };

    private readonly IConfiguration _configuration;
    private readonly ILogger<MultiModelDebateService> _logger;

    public MultiModelDebateService(IConfiguration configuration, ILogger<MultiModelDebateService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public IReadOnlyList<string> GetAvailableAgents() => AgentNames;

    public async IAsyncEnumerable<DebateMessage> RunDebateAsync(
        string query,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var endpoint = _configuration["AZURE_OPENAI_ENDPOINT"]
            ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not configured.");
        var deploymentName = _configuration["AZURE_OPENAI_DEPLOYMENT_NAME"]
            ?? throw new InvalidOperationException("AZURE_OPENAI_DEPLOYMENT_NAME is not configured.");

        var kernel = CreateKernel(deploymentName, endpoint);
        var templateFactory = new KernelPromptTemplateFactory();

        // Create analyst agents from YAML prompt templates
        var bull = await CreateAgentFromYaml("PromptTemplates/BullAnalyst.yaml", kernel, templateFactory);
        var bear = await CreateAgentFromYaml("PromptTemplates/BearAnalyst.yaml", kernel, templateFactory);
        var risk = await CreateAgentFromYaml("PromptTemplates/RiskAnalyst.yaml", kernel, templateFactory);
        var macro = await CreateAgentFromYaml("PromptTemplates/MacroStrategist.yaml", kernel, templateFactory);

        // Selection function determines turn order
        KernelFunction selectionFunction = KernelFunctionFactory.CreateFromPrompt(
            $$$"""
            Your job is to determine which participant takes the next turn in a conversation according to the action of the most recent participant.
            State only the name of the participant to take the next turn.

            Choose only from these participants:
            - {{{MacroStrategistName}}}
            - {{{BullAnalystName}}}
            - {{{BearAnalystName}}}
            - {{{RiskAnalystName}}}

            Always follow these steps when selecting the next participant:
            1) After user input, it is {{{MacroStrategistName}}}'s turn to set the macro context.
            2) After {{{MacroStrategistName}}} replies, it's {{{BullAnalystName}}}'s turn to present the bullish case.
            3) After {{{BullAnalystName}}} replies, it's {{{BearAnalystName}}}'s turn to present the bearish counterpoint.
            4) After {{{BearAnalystName}}} replies, it's {{{RiskAnalystName}}}'s turn to synthesize a risk-managed view.
            5) After {{{RiskAnalystName}}} replies, it's {{{MacroStrategistName}}}'s turn to provide a final macro-aware summary.

            History:
            {{$history}}
            """);

        // Termination function ensures all participants contribute
        KernelFunction terminateFunction = KernelFunctionFactory.CreateFromPrompt(
            $$$"""
            Make sure every participant gets a chance to speak and provide their perspective.
            Once all participants have spoken at least once and a comprehensive view has been established, 
            respond with "yes" to end the conversation.

            History:
            {{$history}}
            """);

        // Create group chat with all agents
        AgentGroupChat chat = new(macro, bull, bear, risk)
        {
            ExecutionSettings = new()
            {
                TerminationStrategy = new KernelFunctionTerminationStrategy(terminateFunction, kernel)
                {
                    Agents = [risk],
                    ResultParser = (result) => result.GetValue<string>()?.Contains("yes", StringComparison.OrdinalIgnoreCase) ?? false,
                    HistoryVariableName = "history",
                    MaximumIterations = 5
                },
                SelectionStrategy = new KernelFunctionSelectionStrategy(selectionFunction, kernel)
                {
                    AgentsVariableName = "agents",
                    HistoryVariableName = "history"
                }
            }
        };

        // Start the debate
        chat.AddChatMessage(new ChatMessageContent(AuthorRole.User, query));

        _logger.LogInformation("Starting multi-model debate for query: {Query}", query);

        await foreach (var content in chat.InvokeAsync(cancellationToken))
        {
            var message = new DebateMessage
            {
                AgentName = content.AuthorName ?? "Unknown",
                Content = content.Content ?? string.Empty,
                Timestamp = DateTime.UtcNow
            };

            _logger.LogInformation("[{Agent}]: {Content}", message.AgentName, message.Content?[..Math.Min(100, message.Content.Length)]);

            yield return message;
        }

        // Yield a final moderator summary
        yield return new DebateMessage
        {
            AgentName = ModeratorName,
            Content = "All perspectives have been presented. Review the analysis above for a comprehensive end-to-end view of potential outcomes across bullish, bearish, risk management, and macroeconomic dimensions.",
            Timestamp = DateTime.UtcNow
        };
    }

    private Kernel CreateKernel(string deploymentName, string endpoint)
    {
        var tenantId = _configuration["AZURE_TENANT_ID"];
        var credentialOptions = new DefaultAzureCredentialOptions();
        if (!string.IsNullOrEmpty(tenantId))
        {
            credentialOptions.TenantId = tenantId;
        }
        var credential = new DefaultAzureCredential(credentialOptions);

        return Kernel.CreateBuilder()
            .AddAzureOpenAIChatCompletion(deploymentName, endpoint, credential)
            .Build();
    }

    private static async Task<ChatCompletionAgent> CreateAgentFromYaml(
        string yamlPath, Kernel kernel, KernelPromptTemplateFactory templateFactory)
    {
        var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, yamlPath);
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Prompt template '{yamlPath}' not found at '{filePath}'.");

        var yaml = await File.ReadAllTextAsync(filePath);
        var promptConfig = KernelFunctionYaml.ToPromptTemplateConfig(yaml);

        return new ChatCompletionAgent(promptConfig, templateFactory)
        {
            Kernel = kernel
        };
    }
}
