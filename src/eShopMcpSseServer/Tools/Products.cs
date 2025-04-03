using eShopMcpSseServer.Services;
using ModelContextProtocol.Server;
using SearchEntities;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace eShopMcpSseServer.Tools;

[McpServerToolType]
public static class Products 
{
    [McpServerTool(Name = "SemanticSearchProducts"), Description("Performs a search in the outdoor products catalog. Returns a text with the found products")]
    public static async Task<SearchResponse> SemanticSearchProducts(
        ProductService productService,
        ILogger<ProductService> logger,
        IMcpServer currentMcpServer,
        [Description("The search query to be used in the products search")] string query)
    {
        logger.LogInformation("==========================");
        logger.LogInformation($"Function Search products: {query}");

        SearchResponse response = new();
        try
        {
            // call the desired Endpoint
            response = await productService.Search(query, true);
            response.McpFunctionCallName = "SemanticSearchProducts";
            response.McpServerInfoName = currentMcpServer.ServerOptions.ServerInfo.Name;
        }
        catch (Exception ex)
        {
            logger.LogError($"Error during Search: {ex.Message}");
            response.Response = $"No response. {ex}";
        }

        logger.LogInformation($"Response: {response?.Response}");
        logger.LogInformation("==========================");
        return response;
    }
}
