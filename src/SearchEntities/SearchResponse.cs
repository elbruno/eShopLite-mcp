using System.Text.Json.Serialization;

namespace SearchEntities;

public class SearchResponse
{
    public SearchResponse()
    {
        Products = new List<DataEntities.Product>();
        Response = string.Empty;
        FunctionCallId = string.Empty;
        FunctionCallName = string.Empty;
        ServerInfoName = string.Empty;
    }

    [JsonPropertyName("Response")]
    public string Response { get; set; }

    [JsonPropertyName("FunctionCallName")]
    public string FunctionCallId { get; set; }

    [JsonPropertyName("FunctionCallName")]
    public string FunctionCallName { get; set; }

    [JsonPropertyName("ServerInfoName")]
    public string ServerInfoName { get; set; }

    [JsonPropertyName("Products")]
    public List<DataEntities.Product>? Products { get; set; }
}

[JsonSerializable(typeof(SearchResponse))]
public sealed partial class SearchResponseSerializerContext : JsonSerializerContext
{
}