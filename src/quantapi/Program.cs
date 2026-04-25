using QuantLib.Agents;
using QuantLib.Agents.Turn;
using QuantLib.Agents.Quants;
using QuantLib.Agents.Compare;
using Azure.AI.Projects;
using Azure.Identity;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

var appInsightsConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
if (!string.IsNullOrEmpty(appInsightsConnectionString))
{
    builder.Services.AddOpenTelemetry().UseAzureMonitor();
}

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(
                builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
                ?? ["http://localhost:5000", "https://localhost:5001"])
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.MapHealthChecks("/health");

app.MapGet("/", () => Results.Redirect("/swagger"));

var logger = app.Services.GetRequiredService<ILogger<Program>>();

var apiEndpoint = app.Configuration["AZURE_AI_PROJECT_ENDPOINT"]
    ?? throw new InvalidOperationException("AZURE_AI_PROJECT_ENDPOINT is not set.");
var apiDeploymentName = app.Configuration["AZURE_AI_MODEL_DEPLOYMENT_NAME"]
    ?? throw new InvalidOperationException("AZURE_AI_MODEL_DEPLOYMENT_NAME is not set.");
var apiTenantId = app.Configuration["AZURE_TENANT_ID"];
var defaultCredential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
{
    TenantId = apiTenantId
});

AIProjectClient apiProjectClient = new(new Uri(apiEndpoint), defaultCredential);

var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();

// ──────────────────────────────────────────────────────────
// Debate SSE streaming endpoint (fan-out parallel)
// ──────────────────────────────────────────────────────────
var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
};

async Task HandleDebate(DebateRequest request, HttpContext httpContext)
{
    var sanitizedTopic = request.Topic.ReplaceLineEndings(string.Empty);
    logger.LogInformation("Debate request: {Topic}", sanitizedTopic);

    httpContext.Response.ContentType = "text/event-stream";
    httpContext.Response.Headers.CacheControl = "no-cache";
    httpContext.Response.Headers.Connection = "keep-alive";

    var searchConnectionId = app.Configuration["AZURE_AI_SEARCH_CONNECTION_ID"];
    var searchIndexName = app.Configuration["AZURE_AI_SEARCH_INDEX_NAME"];
    var orchestrator = new DebateOrchestrator(
        apiProjectClient,
        apiDeploymentName,
        loggerFactory.CreateLogger<DebateOrchestrator>(),
        searchConnectionId,
        searchIndexName);

    await foreach (var agentEvent in orchestrator.RunStreamingAsync(request.Topic, httpContext.RequestAborted))
    {
        var json = JsonSerializer.Serialize(agentEvent, jsonOptions);
        await httpContext.Response.WriteAsync($"data: {json}\n\n", httpContext.RequestAborted);
        await httpContext.Response.Body.FlushAsync(httpContext.RequestAborted);
    }

    await httpContext.Response.WriteAsync("data: [DONE]\n\n", httpContext.RequestAborted);
    await httpContext.Response.Body.FlushAsync(httpContext.RequestAborted);
}

app.MapPost("/debate", HandleDebate);
app.MapPost("/research", HandleDebate);

// ──────────────────────────────────────────────────────────
// Quant turn-based sequential analysis endpoint
// ──────────────────────────────────────────────────────────
app.MapPost("/turn", async (TurnRequest request, HttpContext httpContext) =>
{
    var sanitizedTopic = request.Topic.ReplaceLineEndings(string.Empty);
    logger.LogInformation("Turn request: {Topic}", sanitizedTopic);

    httpContext.Response.ContentType = "text/event-stream";
    httpContext.Response.Headers.CacheControl = "no-cache";
    httpContext.Response.Headers.Connection = "keep-alive";

    var searchConnectionId = app.Configuration["AZURE_AI_SEARCH_CONNECTION_ID"];
    var searchIndexName = app.Configuration["AZURE_AI_SEARCH_INDEX_NAME"];
    var turnOrchestrator = new TurnOrchestrator(
        apiProjectClient,
        apiDeploymentName,
        loggerFactory.CreateLogger<TurnOrchestrator>(),
        searchConnectionId,
        searchIndexName);

    await foreach (var agentEvent in turnOrchestrator.RunStreamingAsync(request.Topic, httpContext.RequestAborted))
    {
        var json = JsonSerializer.Serialize(agentEvent, jsonOptions);
        await httpContext.Response.WriteAsync($"data: {json}\n\n", httpContext.RequestAborted);
        await httpContext.Response.Body.FlushAsync(httpContext.RequestAborted);
    }

    await httpContext.Response.WriteAsync("data: [DONE]\n\n", httpContext.RequestAborted);
    await httpContext.Response.Body.FlushAsync(httpContext.RequestAborted);
});

// ──────────────────────────────────────────────────────────
// Compare SSE streaming endpoint
// ──────────────────────────────────────────────────────────
app.MapPost("/compare", async (CompareRequest request, HttpContext httpContext) =>
{
    var sanitizedTopic = request.Topic.ReplaceLineEndings(string.Empty);
    logger.LogInformation("Compare request: {Topic}", sanitizedTopic);

    httpContext.Response.ContentType = "text/event-stream";
    httpContext.Response.Headers.CacheControl = "no-cache";
    httpContext.Response.Headers.Connection = "keep-alive";

    var models = new List<(string ModelName, string DeploymentName)>
    {
        ("gpt-4o", app.Configuration["AZURE_AI_COMPARE_GPT4O_DEPLOYMENT"] ?? "gpt-4o"),
        ("gpt-4.1", app.Configuration["AZURE_AI_COMPARE_GPT41_DEPLOYMENT"] ?? "gpt-4.1"),
        ("gpt-5.4", app.Configuration["AZURE_AI_COMPARE_GPT54_DEPLOYMENT"] ?? "gpt-5.4")
    };

    var searchConnectionId = app.Configuration["AZURE_AI_SEARCH_CONNECTION_ID"];
    var searchIndexName = app.Configuration["AZURE_AI_SEARCH_INDEX_NAME"];
    var orchestrator = new CompareOrchestrator(
        apiProjectClient,
        models,
        apiDeploymentName,
        loggerFactory.CreateLogger<CompareOrchestrator>(),
        searchConnectionId,
        searchIndexName);

    await foreach (var compareEvent in orchestrator.RunStreamingAsync(request.Topic, httpContext.RequestAborted))
    {
        var json = JsonSerializer.Serialize(compareEvent, jsonOptions);
        await httpContext.Response.WriteAsync($"data: {json}\n\n", httpContext.RequestAborted);
        await httpContext.Response.Body.FlushAsync(httpContext.RequestAborted);
    }

    await httpContext.Response.WriteAsync("data: [DONE]\n\n", httpContext.RequestAborted);
    await httpContext.Response.Body.FlushAsync(httpContext.RequestAborted);
});

// ──────────────────────────────────────────────────────────
// Chat endpoint (direct single-agent conversation)
// ──────────────────────────────────────────────────────────
app.MapPost("/chat", async (AgentChatRequest request, HttpContext httpContext) =>
{
    logger.LogInformation("Chat request to agent: {Agent}", request.Agent);

    httpContext.Response.ContentType = "text/event-stream";
    httpContext.Response.Headers.CacheControl = "no-cache";
    httpContext.Response.Headers.Connection = "keep-alive";

    var searchConnectionId = app.Configuration["AZURE_AI_SEARCH_CONNECTION_ID"];
    var searchIndexName = app.Configuration["AZURE_AI_SEARCH_INDEX_NAME"];

    QuantAgent agent = request.Agent.ToLowerInvariant() switch
    {
        "alpha" => new AlphaQuantAgent(apiProjectClient, apiDeploymentName, searchConnectionId, searchIndexName, logger: loggerFactory.CreateLogger<AlphaQuantAgent>()),
        "pricing" => new PricingQuantAgent(apiProjectClient, apiDeploymentName, searchConnectionId, searchIndexName, logger: loggerFactory.CreateLogger<PricingQuantAgent>()),
        "risk" => new RiskQuantAgent(apiProjectClient, apiDeploymentName, searchConnectionId, searchIndexName, logger: loggerFactory.CreateLogger<RiskQuantAgent>()),
        _ => throw new ArgumentException($"Unknown agent: {request.Agent}")
    };

    var prompt = request.Message;
    if (request.History is { Count: > 0 })
    {
        var historyText = string.Join("\n", request.History.Select(h => $"{h.Role}: {h.Content}"));
        prompt = $"Conversation so far:\n{historyText}\n\nUser: {request.Message}";
    }

    var startEvent = new { type = "AgentStarted", agentName = agent.Name, specialty = agent.Specialty, timestamp = DateTime.UtcNow };
    await httpContext.Response.WriteAsync($"data: {JsonSerializer.Serialize(startEvent, jsonOptions)}\n\n", httpContext.RequestAborted);
    await httpContext.Response.Body.FlushAsync(httpContext.RequestAborted);

    AgentResult response;
    try
    {
        response = await agent.RunAsync(prompt);
    }
    catch (Exception ex)
    {
        var errorEvent = new { type = "AgentError", agentName = agent.Name, error = ex.Message, timestamp = DateTime.UtcNow };
        await httpContext.Response.WriteAsync($"data: {JsonSerializer.Serialize(errorEvent, jsonOptions)}\n\n", httpContext.RequestAborted);
        await httpContext.Response.Body.FlushAsync(httpContext.RequestAborted);
        await httpContext.Response.WriteAsync("data: [DONE]\n\n", httpContext.RequestAborted);
        await httpContext.Response.Body.FlushAsync(httpContext.RequestAborted);
        return;
    }

    var responseEvent = new { type = "AgentResponse", agentName = agent.Name, specialty = agent.Specialty, message = response, timestamp = DateTime.UtcNow };
    await httpContext.Response.WriteAsync($"data: {JsonSerializer.Serialize(responseEvent, jsonOptions)}\n\n", httpContext.RequestAborted);
    await httpContext.Response.Body.FlushAsync(httpContext.RequestAborted);

    await httpContext.Response.WriteAsync("data: [DONE]\n\n", httpContext.RequestAborted);
    await httpContext.Response.Body.FlushAsync(httpContext.RequestAborted);
});

await app.RunAsync();

record AgentChatRequest(string Agent, string Message, List<ChatHistoryItem>? History = null);
record ChatHistoryItem(string Role, string Content);
record TurnRequest(string Topic);
record DebateRequest(string Topic);
record ResearchRequest(string Topic);
record CompareRequest(string Topic);
