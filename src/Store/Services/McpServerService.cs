using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Configuration;
using ModelContextProtocol.Protocol.Transport;
using SearchEntities;

namespace Store.Services;

public class McpServerService
{
    private readonly HttpClient httpClient;
    private readonly ILogger<ProductService> logger;
    IMcpClient mcpClient = null!;
    IList<AIFunction> tools = null!;
    private Microsoft.Extensions.AI.IChatClient? client;
    private IList<ChatMessage> ChatMessages = [];

    public McpServerService(HttpClient _httpClient, ILogger<ProductService> _logger, IMcpClient _mcpClient, IChatClient? _client)
    {
        logger = _logger;
        httpClient = _httpClient;
        mcpClient = _mcpClient;
        client = _client;

        // get mcp server tools
        tools = mcpClient.GetAIFunctionsAsync().GetAwaiter().GetResult();
    }

    public async Task<SearchResponse?> Search(string searchTerm)
    {
        try
        {
            // init chat messages
            ChatMessages = [];
            ChatMessages.Add(new ChatMessage(ChatRole.System, "You are a helpful assistant. You always replies using text and emojis. You never generate HTML or Markdown. You only do what the user ask you to do. If you don't have a function to answer a question, you just answer que question"));

            // call the desired Endpoint
            ChatMessages.Add(new ChatMessage(ChatRole.User, searchTerm));
            var responseComplete = await client.GetResponseAsync(
                ChatMessages, 
                new() { Tools = tools.ToArray() });
            logger.LogInformation($"Model Response: {responseComplete}");
            ChatMessages.AddMessages(responseComplete);

            SearchResponse searchResponse = new SearchResponse
            {
                Response = responseComplete.Text
            };
            return searchResponse;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during Search.");
        }

        return new SearchResponse { Response = "No response" };
    }
}
