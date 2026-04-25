using QuantLib.Agents;
using QuantLib.Agents.Turn;
using QuantLib.Agents.Quants;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

// Usage:
//   dotnet run -- --turn "market analysis request"
//   dotnet run -- --quant "market analysis request"

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

    var turnOrchestrator = new QuantTurnOrchestrator(aiProjectClient, deploymentName, loggerFactory.CreateLogger<QuantTurnOrchestrator>(), config["AZURE_AI_SEARCH_CONNECTION_ID"], config["AZURE_AI_SEARCH_INDEX_NAME"]);
    await turnOrchestrator.RunConsoleAsync(request);
    return;
}

if (args.Length > 0 && args[0] == "--quant")
{
    var request = args.Length > 1
        ? string.Join(" ", args.Skip(1))
        : "Analyze current conditions and provide recommendations";

    var orchestrator = new QuantOrchestrator(aiProjectClient, deploymentName, loggerFactory.CreateLogger<QuantOrchestrator>(), config["AZURE_AI_SEARCH_CONNECTION_ID"], config["AZURE_AI_SEARCH_INDEX_NAME"]);
    await orchestrator.RunConsoleAsync(request);
    return;
}

Console.WriteLine("Usage:");
Console.WriteLine("  dotnet run -- --turn \"market analysis request\"");
Console.WriteLine("  dotnet run -- --quant \"market analysis request\"");
