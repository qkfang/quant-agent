using QuantLib.Agents;
using QuantLib.Agents.Philosophers;
using QuantLib.Agents.Quants;
using QuantLib.Agents.Compare;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

// Usage:
//   dotnet run -- --debate "your topic"
//   dotnet run -- --quant "market analysis request"
//   dotnet run -- --compare "topic to compare across models"

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

if (args.Length > 0 && args[0] == "--debate")
{
    var topic = args.Length > 1
        ? string.Join(" ", args.Skip(1))
        : "How can we ensure that AI benefits all of humanity?";

    var debate = new PhilosopherDebate(aiProjectClient, deploymentName, loggerFactory.CreateLogger<PhilosopherDebate>());
    await debate.DebateConsoleAsync(topic);
    return;
}

if (args.Length > 0 && args[0] == "--quant")
{
    var request = args.Length > 1
        ? string.Join(" ", args.Skip(1))
        : "China market overview for May 2026";

    var orchestrator = new QuantOrchestrator(aiProjectClient, deploymentName, loggerFactory.CreateLogger<QuantOrchestrator>(), config["AZURE_AI_SEARCH_CONNECTION_ID"], config["AZURE_AI_SEARCH_INDEX_NAME"]);
    await orchestrator.RunConsoleAsync(request);
    return;
}

if (args.Length > 0 && args[0] == "--compare")
{
    var request = args.Length > 1
        ? string.Join(" ", args.Skip(1))
        : "What are the key factors driving global inflation in 2026?";

    var models = new List<(string ModelName, string DeploymentName)>
    {
        ("gpt-4o", config["AZURE_AI_COMPARE_GPT4O_DEPLOYMENT"] ?? "gpt-4o"),
        ("gpt-4.1", config["AZURE_AI_COMPARE_GPT41_DEPLOYMENT"] ?? "gpt-4.1"),
        ("gpt-5.2", config["AZURE_AI_COMPARE_GPT52_DEPLOYMENT"] ?? "gpt-5.2")
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
Console.WriteLine("  dotnet run -- --debate \"your topic\"");
Console.WriteLine("  dotnet run -- --quant \"market analysis request\"");
Console.WriteLine("  dotnet run -- --compare \"topic to compare across models\"");
