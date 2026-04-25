using quantweb.Components;
using quantweb.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register multi-model debate service
builder.Services.AddHttpClient<IMultiModelDebateService, MultiModelDebateService>(client =>
{
    var apiBaseUrl = builder.Configuration["QUANTAPI_BASE_URL"] ?? "http://localhost:5100";
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromMinutes(10);
});

// Register research service - use mock when configured or when no API URL is set
var useMock = builder.Configuration.GetValue<bool>("UseMockResearch", false);
if (useMock)
{
    builder.Services.AddSingleton<IResearchService, MockResearchService>();
}
else
{
    builder.Services.AddHttpClient<IResearchService, ResearchService>(client =>
    {
        var apiBaseUrl = builder.Configuration["QUANTAPI_BASE_URL"] ?? "http://localhost:5100";
        client.BaseAddress = new Uri(apiBaseUrl);
        client.Timeout = TimeSpan.FromMinutes(10);
    });
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
