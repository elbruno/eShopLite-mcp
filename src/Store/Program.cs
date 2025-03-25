using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using ModelContextProtocol.Configuration;
using ModelContextProtocol.Protocol.Transport;
using OpenAI;
using OpenAI.Chat;
using Store.Components;
using Store.Services;

var builder = WebApplication.CreateBuilder(args);

// add aspire service defaults
builder.AddServiceDefaults();

// add services
builder.Services.AddSingleton<ProductService>();
builder.Services.AddHttpClient<ProductService>(
    static client => client.BaseAddress = new("https+http://products"));

builder.Services.AddSingleton<McpServerService>();
builder.Services.AddHttpClient<McpServerService>(
    static client => client.BaseAddress = new("https+http://products/sse"));

// add openai client
var azureOpenAiClientName = "openai";
builder.AddOpenAIClient(azureOpenAiClientName);

// get azure openai client and create Chat client from aspire hosting configuration
builder.Services.AddSingleton<IChatClient>(serviceProvider =>
{
    var chatDeploymentName = "gpt-4o-mini";
    var logger = serviceProvider.GetService<ILogger<Program>>()!;
    logger.LogInformation($"Chat client configuration, modelId: {chatDeploymentName}");
    IChatClient chatClient = null;
    try
    {
        OpenAIClient client = serviceProvider.GetRequiredService<OpenAIClient>();
        chatClient = client.AsChatClient(chatDeploymentName)
                        .AsBuilder()
                        .UseFunctionInvocation()
                        .Build();
    }
    catch (Exception exc)
    {
        logger.LogError(exc, "Error creating embeddings client");
    }
    return chatClient;
});

// create Mcp Client
builder.Services.AddSingleton<IMcpClient>(serviceProvider =>
{
    var logger = serviceProvider.GetService<ILogger<Program>>()!;
    logger.LogInformation($"Create Mcp Client");

    // create the McpClient
    McpClientOptions mcpClientOptions = new()
    { ClientInfo = new() { Name = "AspNetCoreSseClient", Version = "1.0.0" } };
    McpServerConfig mcpServerConfig = new()
    {
        Id = "AspNetCoreSse",
        Name = "AspNetCoreSse",
        TransportType = TransportTypes.Sse,
        Location = "https://localhost:7133/sse"
    };

    var mcpClient = McpClientFactory.CreateAsync(mcpServerConfig, mcpClientOptions).GetAwaiter().GetResult();
    return mcpClient;
});

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// aspire map default endpoints
app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
