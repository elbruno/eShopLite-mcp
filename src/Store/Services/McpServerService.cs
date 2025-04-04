using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using SearchEntities;
using System.Web;

namespace Store.Services;

public class McpServerService
{
    private readonly ILogger<ProductService> logger;
    IMcpClient mcpClient = null!;
    IList<McpClientTool> tools = null!;
    private Microsoft.Extensions.AI.IChatClient? chatClient;
    private IList<ChatMessage> ChatMessages = [];

    public McpServerService(ILogger<ProductService> _logger, IMcpClient _mcpClient, IChatClient? _chatClient)
    {
        logger = _logger;
        mcpClient = _mcpClient;
        chatClient = _chatClient;

        // get mcp server tools
        tools = mcpClient.ListToolsAsync().GetAwaiter().GetResult();
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
            var responseComplete = await chatClient.GetResponseAsync(
                ChatMessages,
                new() { Tools = tools.ToArray() });
            logger.LogInformation($"Model Response: {responseComplete}");
            ChatMessages.AddMessages(responseComplete);

            // create search response
            SearchResponse searchResponse = new SearchResponse
            {
                Response = responseComplete.Text
            };


            // iterate through the messages
            foreach (var message in responseComplete.Messages)
            {
                // validate if the message is a function call
                if (message.Role == ChatRole.Tool)
                {
                    var functionResult = message.Contents.FirstOrDefault() as FunctionResultContent;
                    string functionResultJsonString = functionResult.Result.ToString();

                    // get the fnc call id
                    searchResponse.McpFunctionCallId = functionResult.CallId;

                    // from the functionResultJson, get the element at [JSON].content.[0].text
                    // this is the serialization from the function call response object
                    var functionResultJson = System.Text.Json.JsonDocument.Parse(functionResultJsonString);
                    var searchResponseJson = functionResultJson.RootElement.GetProperty("content").EnumerateArray().FirstOrDefault().GetProperty("text").ToString();

                    // try to deserialize the message.RawRepresentation, in Json, to a SearchResponse object
                    try
                    {
                        var searchResponseTool = System.Text.Json.JsonSerializer.Deserialize<SearchResponse>(searchResponseJson);
                        searchResponse.Products = searchResponseTool?.Products;
                        searchResponse.McpFunctionCallName = searchResponseTool?.McpFunctionCallName;
                        searchResponse.McpServerInfoName = searchResponseTool?.McpServerInfoName;
                    }
                    catch (Exception exc)
                    {
                        logger.LogError(exc, "Error deserializing function result JSON to SearchResponse object.");
                    }

                }
            }


            return searchResponse;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during Search.");
        }

        return new SearchResponse { Response = "No response" };
    }
}