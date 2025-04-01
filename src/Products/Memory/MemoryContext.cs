using Microsoft.EntityFrameworkCore;
using SearchEntities;
using DataEntities;
using OpenAI.Chat;
using OpenAI.Embeddings;
using VectorEntities;
using Microsoft.SemanticKernel.Connectors.InMemory;
using Microsoft.Extensions.VectorData;
using Newtonsoft.Json;
using Products.Models;

namespace Products.Memory;

public class MemoryContext
{
    private ILogger _logger;
    public ChatClient? _chatClient;
    public EmbeddingClient? _embeddingClient;
    public IVectorStoreRecordCollection<int, ProductVector> _productsCollection;
    private string _systemPrompt = "";
    private bool _isMemoryCollectionInitialized = false;

    public MemoryContext(ILogger logger, ChatClient? chatClient, EmbeddingClient? embeddingClient)
    {
        _logger = logger;
        _chatClient = chatClient;
        _embeddingClient = embeddingClient;

        _logger.LogInformation("Memory context created");
        _logger.LogInformation($"Chat Client is null: {_chatClient is null}");
        _logger.LogInformation($"Embedding Client is null: {_embeddingClient is null}");
    }

    public async Task<bool> InitMemoryContextAsync(Context db)
    {
        _logger.LogInformation("Initializing memory context");
        var vectorProductStore = new InMemoryVectorStore();
        _productsCollection = vectorProductStore.GetCollection<int, ProductVector>("products");
        await _productsCollection.CreateCollectionIfNotExistsAsync();

        // define system prompt
        _systemPrompt = "You are a useful assistant. You always reply with a short and funny message. If you do not know an answer, you say 'I don't know that.' You only answer questions related to outdoor camping products. For any other type of questions, explain to the user that you only answer outdoor camping products questions. Do not store memory of the chat conversation.";

        _logger.LogInformation("Get a copy of the list of products");
        // get a copy of the list of products
        var products = await db.Product.ToListAsync();

        _logger.LogInformation("Filling products in memory");

        // iterate over the products and add them to the memory
        foreach (var product in products)
        {
            try
            {
                _logger.LogInformation("Adding product to memory: {Product}", product.Name);
                var productInfo = $"[{product.Name}] is a product that costs [{product.Price}] and is described as [{product.Description}]";

                // new product vector
                var productVector = new ProductVector
                {
                    Id = product.Id,
                    Name = product.Name,
                    Description = product.Description,
                    Price = product.Price,
                    ImageUrl = product.ImageUrl
                };
                var result = await _embeddingClient.GenerateEmbeddingAsync(productInfo);

                productVector.Vector = result.Value.ToFloats();
                var recordId = await _productsCollection.UpsertAsync(productVector);
                _logger.LogInformation("Product added to memory: {Product} with recordId: {RecordId}", product.Name, recordId);
            }
            catch (Exception exc)
            {
                _logger.LogError(exc, "Error adding product to memory");
            }
        }

        _logger.LogInformation("DONE! Filling products in memory");
        return true;
    }

    public async Task<SearchResponse> Search(string search, Context db)
    {
        if (!_isMemoryCollectionInitialized)
        {
            await InitMemoryContextAsync(db);
            _isMemoryCollectionInitialized = true;
        }

        var response = new SearchResponse();
        response.Response = $"I don't know the answer for your question. Your question is: [{search}]";
        Product? firstProduct = new Product();
        var responseText = "";
        try
        {
            var result = await _embeddingClient.GenerateEmbeddingAsync(search);
            var vectorSearchQuery = result.Value.ToFloats();

            var searchOptions = new VectorSearchOptions<ProductVector>()
            {
                Top = 3
            };

            // search the vector database for the most similar product        
            var searchResults = await _productsCollection.VectorizedSearchAsync(vectorSearchQuery, searchOptions);
            double searchScore = 0.0;
            await foreach (var searchItem in searchResults.Results)
            {
                if (searchItem.Score > 0.5)
                {
                    // product found, search the db for the product details                    
                    firstProduct = new Product
                    {
                        Id = searchItem.Record.Id,
                        Name = searchItem.Record.Name,
                        Description = searchItem.Record.Description,
                        Price = searchItem.Record.Price,
                        ImageUrl = searchItem.Record.ImageUrl
                    };

                    searchScore = searchItem.Score.Value;
                    responseText = $"The product [{firstProduct.Name}] fits with the search criteria [{search}][{searchItem.Score.Value.ToString("0.00")}]";
                    _logger.LogInformation($"Search Response: {responseText}");
                }
            }

            // let's improve the response message
            var prompt = @$"You are an intelligent assistant helping clients with their search about outdoor products. Generate a catchy and friendly message using the following information:
    - User Question: {search}
    - Found Product Name: {firstProduct.Name}
    - Found Product Description: {firstProduct.Description}
    - Found Product Price: {firstProduct.Price}
Include the found product information in the response to the user question.";

            var messages = new List<ChatMessage>
    {
        new SystemChatMessage(_systemPrompt),
        new UserChatMessage(prompt)
    };

            _logger.LogInformation("{ChatHistory}", JsonConvert.SerializeObject(messages));

            var resultPrompt = await _chatClient.CompleteChatAsync(messages);
            responseText = resultPrompt.Value.Content[0].Text!;

            // create a response object
            response = new SearchResponse
            {
                Products = firstProduct == null ? [new Product()] : [firstProduct],
                Response = responseText
            };

        }
        catch (Exception ex)
        {
            // Handle exceptions (log them, rethrow, etc.)
            response.Response = $"An error occurred: {ex.Message}";
        }
        return response;
    }
}