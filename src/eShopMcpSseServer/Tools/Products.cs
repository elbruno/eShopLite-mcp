using eShopMcpSseServer.Services;
using ModelContextProtocol.Server;
using SearchEntities;
using System.ComponentModel;

namespace eShopMcpSseServer.Tools;

[McpServerToolType]
public static class Products
{
    [McpServerTool, Description("Search outdoor products using a semantic search.")]
    public static SearchResponse SemanticSearchProducts(string query)
    {
        Console.WriteLine("==========================");
        Console.WriteLine($"Function Search products: {query}");
        
        // get an instance of the product service registered in the DI container
        var serviceProvider = new ServiceCollection()
            .AddSingleton<ProductService>()
            .BuildServiceProvider();
        var productService = serviceProvider.GetService<ProductService>();
        var response = productService.Search(query, true).GetAwaiter().GetResult();

        Console.WriteLine($"Response: {response?.Response}");
        Console.WriteLine($"Function End Semantic Search");
        Console.WriteLine("==========================");
        return response;
    }
}
