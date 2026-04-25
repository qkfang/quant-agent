using QuantLib.Agents;
using QuantLib.Agents.Turn;
using QuantLib.Agents.Quants;
using QuantLib.Agents.Compare;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

// Usage:
//   dotnet run -- --turn "market analysis request"
//   dotnet run -- --quant "market analysis request"
//   dotnet run -- --compare "market analysis request"

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var endpoint = config["AZURE_AI_PROJECT_ENDPOINT"];
if (string.IsNullOrWhiteSpace(endpoint))
    throw new InvalidOperationException("AZURE_AI_PROJECT_ENDPOINT is not set.");

var deploymentName = config["AZURE_AI_MODEL_DEPLOYMENT_NAME"];
if (string.IsNullOrWhiteSpace(deploymentName))
    throw new InvalidOperationException("AZURE_AI_MODEL_DEPLOYMENT_NAME is not set.");
var tenantId = config["AZURE_TENANT_ID"];

var credentialOptions = new DefaultAzureCredentialOptions();
if (!string.IsNullOrWhiteSpace(tenantId))
{
    credentialOptions.TenantId = tenantId;
}
var credential = new DefaultAzureCredential(credentialOptions);
AIProjectClient aiProjectClient = new(new Uri(endpoint), credential);

using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());

if (args.Length > 0 && args[0] == "--turn")
{
    var request = args.Length > 1
        ? string.Join(" ", args.Skip(1))
        : "Analyze current conditions and provide recommendations";

    var turnOrchestrator = new TurnOrchestrator(aiProjectClient, deploymentName, loggerFactory.CreateLogger<TurnOrchestrator>(), config["AZURE_AI_SEARCH_CONNECTION_ID"], config["AZURE_AI_SEARCH_INDEX_NAME"], config["AZURE_BING_CONNECTION_ID"]);
    await turnOrchestrator.RunConsoleAsync(request);
    return;
}

if (args.Length > 0 && args[0] == "--quant")
{
    var request = args.Length > 1
        ? string.Join(" ", args.Skip(1))
        : "Analyze current conditions and provide recommendations";

    var orchestrator = new DebateOrchestrator(aiProjectClient, deploymentName, loggerFactory.CreateLogger<DebateOrchestrator>(), config["AZURE_AI_SEARCH_CONNECTION_ID"], config["AZURE_AI_SEARCH_INDEX_NAME"], config["AZURE_BING_CONNECTION_ID"]);
    await orchestrator.RunConsoleAsync(request);
    return;
}

if (args.Length > 0 && args[0] == "--compare")
{
    var request = args.Length > 1
        ? string.Join(" ", args.Skip(1))
        : "Analyze current conditions and provide recommendations";

    var models = new List<(string ModelName, string DeploymentName)>
    {
        ("gpt-4o", config["AZURE_AI_COMPARE_GPT4O_DEPLOYMENT"] ?? "gpt-4o"),
        ("gpt-4.1", config["AZURE_AI_COMPARE_GPT41_DEPLOYMENT"] ?? "gpt-4.1"),
        ("gpt-5.4", config["AZURE_AI_COMPARE_GPT52_DEPLOYMENT"] ?? "gpt-5.4")
    };

    var orchestrator = new CompareOrchestrator(
        aiProjectClient,
        models,
        deploymentName,
        loggerFactory.CreateLogger<CompareOrchestrator>());
    await orchestrator.RunConsoleAsync(request);
    return;
}

Console.WriteLine("Usage:");
Console.WriteLine("  dotnet run -- --turn \"market analysis request\"");
Console.WriteLine("  dotnet run -- --quant \"market analysis request\"");
Console.WriteLine("  dotnet run -- --compare \"market analysis request\"");
