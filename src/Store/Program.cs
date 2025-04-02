using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol.Transport;
using OpenAI;
using Store.Components;
using Store.Services;
using System.Web;

var builder = WebApplication.CreateBuilder(args);

// add aspire service defaults
builder.AddServiceDefaults();

// add services
builder.Services.AddSingleton<ProductService>();
builder.Services.AddHttpClient<ProductService>(
    static client => client.BaseAddress = new("https+http://products"));

builder.Services.AddSingleton<McpServerService>();

// add openai client
var azureOpenAiClientName = "openai";
// builder.AddOpenAIClient(azureOpenAiClientName);

// get azure openai client and create Chat client from aspire hosting configuration
builder.Services.AddSingleton<IChatClient>(serviceProvider =>
{
    var chatDeploymentName = "gpt-4o-mini";
    var logger = serviceProvider.GetService<ILogger<Program>>()!;
    logger.LogInformation($"Chat client configuration, modelId: {chatDeploymentName}");
    IChatClient chatClient = null;
    //try
    //{
    //    OpenAIClient client = serviceProvider.GetRequiredService<OpenAIClient>();
    //    chatClient = client.AsChatClient(chatDeploymentName)
    //                    .AsBuilder()
    //                    .UseFunctionInvocation()
    //                    .Build();
    //}
    //catch (Exception exc)
    //{
    //    logger.LogError(exc, "Error creating embeddings client");
    //}

    var (endpoint, apiKey) = GetEndpointAndKey(builder, azureOpenAiClientName);

    // validate that the endpoint must end with openai.azure.com/openai/deployments/modelname, if not fix the endpoint url
    if (!endpoint.EndsWith($"openai/deployments/{chatDeploymentName}"))
    {
        logger.LogInformation($"Invalid endpoint: {endpoint}");
        endpoint = endpoint + $"openai/deployments/{chatDeploymentName}";
        logger.LogInformation($"Fixed endpoint: {endpoint}");
    }

    if (string.IsNullOrEmpty(apiKey))
    {
        // no apikey, use default azure credential  
        var endpointModel = new Uri(endpoint);
        logger.LogInformation($"No ApiKey, use DEFAULT AZURE CREDENTIALS");
        logger.LogInformation($"Creating chat client with modelId: [{chatDeploymentName}] / endpoint: [{endpoint}]");

        chatClient = new Azure.AI.Inference.ChatCompletionsClient(
            endpoint: new Uri(endpoint),
            credential: new DefaultAzureCredential())
        .AsChatClient(chatDeploymentName)
        .AsBuilder()
        .UseFunctionInvocation()
        .Build();
    }
    else
    {
        // using ApiKey
        logger.LogInformation($"ApiKey Found, use APIKEY CREDENTIALS");
        logger.LogInformation($"Creating chat client with modelId: [{chatDeploymentName}] / endpoint: [{endpoint}] / ApiKey length: {apiKey.Length}");
        chatClient = new Azure.AI.Inference.ChatCompletionsClient(
            endpoint: new Uri(endpoint),
            credential: new AzureKeyCredential(apiKey))
        .AsChatClient(chatDeploymentName)
        .AsBuilder()
        .UseFunctionInvocation()
        .Build(); 
    }
    return chatClient;
});

// create Mcp Client
builder.Services.AddSingleton<IMcpClient>(serviceProvider =>
{
    McpClientOptions mcpClientOptions = new()
    { ClientInfo = new() { Name = "AspNetCoreSseClient", Version = "1.0.0" } };

    // can't use the service discovery for ["https +http://aspnetsseserver"]
    // fix: read the environment value for the key 'services__aspnetsseserver__https__0' to get the url for the aspnet core sse server
    var serviceName = "eshopmcpserver";
    var name = $"services__{serviceName}__https__0";
    var url = Environment.GetEnvironmentVariable(name) + "/sse";

    McpServerConfig mcpServerConfig = new()
    {
        Id = "AspNetCoreSse",
        Name = "AspNetCoreSse",
        TransportType = TransportTypes.Sse,
        Location = url
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


static (string endpoint, string apiKey) GetEndpointAndKey(WebApplicationBuilder builder, string name)
{
    var connectionString = builder.Configuration.GetConnectionString(name);
    var parameters = HttpUtility.ParseQueryString(connectionString.Replace(";", "&"));
    return (parameters["Endpoint"], parameters["Key"]);
}