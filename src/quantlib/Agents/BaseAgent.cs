using Azure.AI.Extensions.OpenAI;
using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Microsoft.Extensions.Logging;
using OpenAI.Responses;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace QuantLib.Agents;

public record SearchCitation(string Title, string Url);

public record AgentResult(string Text, IReadOnlyList<SearchCitation> Citations);

public abstract class BaseAgent
{
    protected readonly ProjectResponsesClient _responseClient;
    protected readonly ILogger _logger;
    protected readonly string _agentId;

    protected BaseAgent(AIProjectClient aiProjectClient, string agentId, string deploymentName, string instructions, IList<ResponseTool>? tools = null, ILogger? logger = null)
        : this(aiProjectClient, agentId, deploymentName, instructions, tools, null, logger)
    {
    }

    protected BaseAgent(AIProjectClient aiProjectClient, string agentId, string deploymentName, string instructions, IList<ResponseTool>? tools = null, Action<DeclarativeAgentDefinition>? configureAgent = null, ILogger? logger = null)
    {
        _agentId = agentId;
        _logger = logger ?? LoggerFactory.Create(b => b.AddConsole()).CreateLogger(agentId);

        var agentDefinition = new DeclarativeAgentDefinition(model: deploymentName)
        {
            Instructions = instructions
        };

        if (tools != null)
        {
            foreach (var tool in tools)
            {
                if (tool != null)
                    agentDefinition.Tools.Add(tool);
            }
        }

        configureAgent?.Invoke(agentDefinition);

        var agentVersion = aiProjectClient.AgentAdministrationClient.CreateAgentVersion(
            agentId,
            new ProjectsAgentVersionCreationOptions(agentDefinition)).Value;

        _responseClient = aiProjectClient.ProjectOpenAIClient.GetProjectResponsesClientForAgent(agentVersion.Name);
    }

    public async Task<AgentResult> RunAsync(string message)
    {
        var sw = Stopwatch.StartNew();

        CreateResponseOptions? nextOptions = new()
        {
            InputItems = { ResponseItem.CreateUserMessageItem(message) }
        };

        ResponseResult? result = null;

        while (nextOptions is not null)
        {
            result = await _responseClient.CreateResponseAsync(nextOptions);
            nextOptions = null;

            foreach (var item in result!.OutputItems)
            {
                if (item is McpToolCallApprovalRequestItem mcpCall)
                {
                    _logger.LogInformation("Auto-approving MCP tool call on {ServerLabel}", mcpCall.ServerLabel);
                    nextOptions ??= new CreateResponseOptions { PreviousResponseId = result.Id };
                    nextOptions.InputItems.Add(ResponseItem.CreateMcpApprovalResponseItem(mcpCall.Id, approved: true));
                }
            }
        }

        sw.Stop();
        _logger.LogInformation("Agent {AgentId} completed in {Duration}ms", _agentId, sw.ElapsedMilliseconds);

        var text = result?.GetOutputText() ?? string.Empty;
        var citations = ExtractCitations(result);

        return new AgentResult(text, citations);
    }

    public async IAsyncEnumerable<string> RunStreamingAsync(string message, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        CreateResponseOptions nextOptions = new()
        {
            InputItems = { ResponseItem.CreateUserMessageItem(message) }
        };

        while (true)
        {
            var pendingApprovals = new List<string>();
            ResponseResult? completedResponse = null;

            await foreach (var update in _responseClient.CreateResponseStreamingAsync(nextOptions, cancellationToken).WithCancellation(cancellationToken))
            {
                if (update is StreamingResponseOutputTextDeltaUpdate delta)
                    yield return delta.Delta;
                else if (update is StreamingResponseOutputItemDoneUpdate itemDone && itemDone.Item is McpToolCallApprovalRequestItem mcpCall)
                {
                    _logger.LogInformation("Auto-approving MCP tool call on {ServerLabel}", mcpCall.ServerLabel);
                    pendingApprovals.Add(mcpCall.Id);
                }
                else if (update is StreamingResponseCompletedUpdate done)
                    completedResponse = done.Response;
            }

            if (pendingApprovals.Count == 0)
                break;

            nextOptions = new CreateResponseOptions { PreviousResponseId = completedResponse!.Id };
            foreach (var id in pendingApprovals)
                nextOptions.InputItems.Add(ResponseItem.CreateMcpApprovalResponseItem(id, approved: true));
        }

        sw.Stop();
        _logger.LogInformation("Agent {AgentId} streaming completed in {Duration}ms", _agentId, sw.ElapsedMilliseconds);
    }

    private IReadOnlyList<SearchCitation> ExtractCitations(ResponseResult? result)
    {
        var citations = new List<SearchCitation>();
        if (result is null) return citations;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in result.OutputItems)
        {
            if (item is not MessageResponseItem messageItem) continue;

            foreach (var part in messageItem.Content)
            {
                if (part.OutputTextAnnotations is null) continue;

                foreach (var annotation in part.OutputTextAnnotations)
                {
                    if (annotation is UriCitationMessageAnnotation uriCitation)
                    {
                        var url = uriCitation.Uri?.ToString() ?? string.Empty;
                        if (!string.IsNullOrEmpty(url) && seen.Add(url))
                        {
                            citations.Add(new SearchCitation(
                                uriCitation.Title ?? string.Empty,
                                url));
                        }
                    }
                    else if (annotation is FileCitationMessageAnnotation fileCitation)
                    {
                        var fileIdentifier = fileCitation.FileId ?? fileCitation.Filename ?? string.Empty;
                        if (!string.IsNullOrEmpty(fileIdentifier) && seen.Add(fileIdentifier))
                        {
                            citations.Add(new SearchCitation(
                                fileCitation.Filename ?? fileCitation.FileId ?? string.Empty,
                                string.Empty));
                        }
                    }
                }
            }
        }

        return citations;
    }
}
