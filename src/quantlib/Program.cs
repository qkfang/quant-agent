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

var endpoint = config["AZURE_AI_PROJECT_ENDPOINT"]
    ?? throw new InvalidOperationException("AZURE_AI_PROJECT_ENDPOINT is not set.");
var deploymentName = config["AZURE_AI_MODEL_DEPLOYMENT_NAME"]
    ?? throw new InvalidOperationException("AZURE_AI_MODEL_DEPLOYMENT_NAME is not set.");
var tenantId = config["AZURE_TENANT_ID"];

var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions { TenantId = tenantId });
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

if (args.Length > 0 && args[0] == "--insight")
{
    var question = args.Length > 1
        ? string.Join(" ", args.Skip(1))
        : "What is the current market outlook for AUDUSD?";

    var agent = new QuantAgentInsight(aiProjectClient, deploymentName, [], null, loggerFactory.CreateLogger<QuantAgentInsight>());
    var response = await agent.RunAsync(question);
    Console.WriteLine(response);
    return;
}

Console.WriteLine("Usage:");
Console.WriteLine("  dotnet run -- --debate \"your topic\"");
Console.WriteLine("  dotnet run -- --insight \"your question\"");
