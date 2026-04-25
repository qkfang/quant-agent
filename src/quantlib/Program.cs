using QuantLib.Agents;
using QuantLib.Agents.Philosophers;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

// Usage:
//   dotnet run -- --debate "your topic"
//   dotnet run -- --insight "your question"

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

Console.WriteLine("Usage:");
Console.WriteLine("  dotnet run -- --debate \"your topic\"");
