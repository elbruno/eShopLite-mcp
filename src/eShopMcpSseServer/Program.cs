using eShopMcpSseServer.Services;
using ModelContextProtocol;

var builder = WebApplication.CreateBuilder(args);

// Add default services
builder.AddServiceDefaults();
builder.Services.AddProblemDetails();

// add product service
builder.Services.AddSingleton<ProductService>();
builder.Services.AddHttpClient<ProductService>(
    static client => client.BaseAddress = new("https+http://products"));

// add MCP server
builder.Services.AddMcpServer().WithToolsFromAssembly();

var app = builder.Build();

// Initialize default endpoints
app.MapDefaultEndpoints();
app.UseHttpsRedirection();

// map endpoints
app.MapGet("/", () => $"eShopLite-MCP Server! {DateTime.Now}");
app.MapMcpSse();

app.Run();
