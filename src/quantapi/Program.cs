using QuantLib.Agents;
using QuantLib.Agents.Philosophers;
using QuantLib.Agents.Quants;
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

var insightAgent = new QuantAgentInsight(apiProjectClient, apiDeploymentName, [], null, loggerFactory.CreateLogger<QuantAgentInsight>());

app.MapPost("/insight", async (ChatRequest request) =>
{
    logger.LogInformation("Insight request: {Message}", request.Message);
    var response = await insightAgent.RunAsync(request.Message);
    return Results.Ok(new { response });
});

// ──────────────────────────────────────────────────────────
// Philosopher debate API endpoint
// ──────────────────────────────────────────────────────────
app.MapPost("/debate", async (DebateRequest request) =>
{
    logger.LogInformation("Debate request: {Topic}", request.Topic);
    var debate = new PhilosopherDebate(apiProjectClient, apiDeploymentName, loggerFactory.CreateLogger<PhilosopherDebate>());
    var turns = await debate.DebateAsync(request.Topic);
    return Results.Ok(new DebateResponse(request.Topic, turns));
});

// ──────────────────────────────────────────────────────────
// Research SSE streaming endpoint
// ──────────────────────────────────────────────────────────
var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

app.MapPost("/research", async (ResearchRequest request, HttpContext httpContext) =>
{
    var sanitizedTopic = request.Topic.ReplaceLineEndings(string.Empty);
    logger.LogInformation("Research request: {Topic}", sanitizedTopic);

    httpContext.Response.ContentType = "text/event-stream";
    httpContext.Response.Headers.CacheControl = "no-cache";
    httpContext.Response.Headers.Connection = "keep-alive";

    var searchConnectionId = app.Configuration["AZURE_AI_SEARCH_CONNECTION_ID"];
    var searchIndexName = app.Configuration["AZURE_AI_SEARCH_INDEX_NAME"];
    var orchestrator = new QuantOrchestrator(
        apiProjectClient,
        apiDeploymentName,
        loggerFactory.CreateLogger<QuantOrchestrator>(),
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
});

await app.RunAsync();

record ChatRequest(string Message);
record DebateRequest(string Topic);
record DebateResponse(string Topic, List<DebateTurn> Turns);
record ResearchRequest(string Topic);
