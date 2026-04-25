using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.Identity;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using QuantAgent.Agents;
using QuantAgent.Agents.Philosophers;

// ──────────────────────────────────────────────────────────
// Console debate mode: dotnet run -- --debate "your topic"
// ──────────────────────────────────────────────────────────
if (args.Length > 0 && args[0] == "--debate")
{
    var topic = args.Length > 1
        ? string.Join(" ", args.Skip(1))
        : "How can we ensure that AI benefits all of humanity?";

    var config = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: false)
        .AddJsonFile("appsettings.Development.json", optional: true)
        .AddEnvironmentVariables()
        .Build();

    var endpoint = config["AZURE_AI_PROJECT_ENDPOINT"]
        ?? throw new InvalidOperationException("AZURE_AI_PROJECT_ENDPOINT is not set.");
    var deploymentName = config["AZURE_AI_MODEL_DEPLOYMENT_NAME"]
        ?? throw new InvalidOperationException("AZURE_AI_MODEL_DEPLOYMENT_NAME is not set.");
    var tenantId = config["AZURE_TENANT_ID"];

    var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
    {
        TenantId = tenantId
    });

    AIProjectClient aiProjectClient = new(new Uri(endpoint), credential);

    using var consoleLoggerFactory = LoggerFactory.Create(b => b.AddConsole());
    var consoleLogger = consoleLoggerFactory.CreateLogger<PhilosopherDebate>();

    var debate = new PhilosopherDebate(aiProjectClient, deploymentName, consoleLogger);
    await debate.DebateConsoleAsync(topic);
    return;
}

// ──────────────────────────────────────────────────────────
// Web API mode (default): starts the ASP.NET Core server
// ──────────────────────────────────────────────────────────
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetry().UseAzureMonitor();

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

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

// var apiMcpUrl = app.Configuration["API_INTG_MCP_URL"];

// var apiIntgTool = ResponseTool.CreateMcpTool(
//     serverLabel: "api-intg",
//     serverUri: new Uri($"{apiMcpUrl}/mcp"),
//     toolCallApprovalPolicy: new McpToolCallApprovalPolicy(GlobalMcpToolCallApprovalPolicy.NeverRequireApproval)
// );

var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();

// Configure Fabric data agent tool for FxAgInsight
var fabricConnectionName = app.Configuration["FABRIC_CONNECTION_NAME"];
Action<DeclarativeAgentDefinition>? insightFabricConfig = null;
if (!string.IsNullOrEmpty(fabricConnectionName))
{
    var fabricConnection = apiProjectClient.Connections.GetConnection(fabricConnectionName);
    var fabricToolOption = new FabricDataAgentToolOptions
    {
        ProjectConnections = { new ToolProjectConnection(projectConnectionId: fabricConnection.Id) }
    };
    insightFabricConfig = agentDef => agentDef.Tools.Add(new MicrosoftFabricPreviewTool(fabricToolOption));
    logger.LogInformation("Fabric data agent tool configured for FxAgInsight with connection: {ConnectionName}", fabricConnectionName);
}
else
{
    logger.LogWarning("FABRIC_CONNECTION_NAME is not set. FxAgInsight will run without Fabric data agent.");
}

var insightAgent = new QuantAgentInsight(apiProjectClient, apiDeploymentName, [], insightFabricConfig, loggerFactory.CreateLogger<QuantAgentInsight>());

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

await app.RunAsync();

record ChatRequest(string Message);
record DebateRequest(string Topic);
record DebateResponse(string Topic, List<DebateTurn> Turns);
