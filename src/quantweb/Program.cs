using quantweb.Components;
using quantweb.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register debate service (fan-out parallel analysis)
builder.Services.AddHttpClient<IResearchService, ResearchService>(client =>
{
    var apiBaseUrl = builder.Configuration["QUANTAPI_BASE_URL"] ?? "http://localhost:5100";
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromMinutes(10);
});

// Register turn service (sequential analysis)
builder.Services.AddHttpClient<ITurnService, TurnService>(client =>
{
    var apiBaseUrl = builder.Configuration["QUANTAPI_BASE_URL"] ?? "http://localhost:5100";
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromMinutes(10);
});

// Register compare service (multi-model comparison)
builder.Services.AddHttpClient<ICompareService, CompareService>(client =>
{
    var apiBaseUrl = builder.Configuration["QUANTAPI_BASE_URL"] ?? "http://localhost:5100";
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromMinutes(10);
});

// Register chat service (direct agent conversation)
builder.Services.AddHttpClient<IChatService, ChatService>(client =>
{
    var apiBaseUrl = builder.Configuration["QUANTAPI_BASE_URL"] ?? "http://localhost:5100";
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromMinutes(10);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
