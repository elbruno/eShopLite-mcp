using SearchEntities;
using DataEntities;
using System.Text.Json;

namespace eShopMcpSseServer.Services;

public class ProductService
{
    HttpClient httpClient;
    private readonly ILogger<ProductService> _logger;

    public ProductService(HttpClient httpClient, ILogger<ProductService> logger)
    {
		_logger = logger;
        this.httpClient = httpClient;
    }

    public async Task<SearchResponse?> Search(string searchTerm, bool semanticSearch = false)
    {
        try
        {
            // call the desired Endpoint
            HttpResponseMessage response = null;
            if (semanticSearch)
            {
                // AI Search
                response = await httpClient.GetAsync($"/api/aisearch/{searchTerm}");
            }
            else
            {
                // standard search
                response = await httpClient.GetAsync($"/api/product/search/{searchTerm}");
            }

            var responseText = await response.Content.ReadAsStringAsync();

            _logger.LogInformation($"Http status code: {response.StatusCode}");
            _logger.LogInformation($"Http response content: {responseText}");

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<SearchResponse>();
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.InternalServerError)
            {
                _logger.LogError($"Internal Server Error: {responseText}");
                throw new Exception($"Internal Server Error: {responseText}");
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning($"Not Found: {responseText}");
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Search.");
            throw ex;
        }

        return new SearchResponse { Response = "No response" };
    }    
}
