using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using Xunit;

public partial class EmbeddingResponseOrderPolicyTests
{
    private const string ResponseString = """
        {"object":"list","model":"test-model","data":[
          {"object":"embedding","index":1,"embedding":[0.0,1.0]},
          {"object":"embedding","index":0,"embedding":[1.0,0.0]}],
         "usage":{"prompt_tokens":12,"total_tokens":12}}
        """;

    // The common Microsoft interface must return input order while preserving vector values, model, and usage.
    [Fact]
    public async Task Response_indexes_restore_input_order_before_conversion()
    {
        // Arrange
        using var responseHandler = new ResponseHandler(ResponseString);
        using var httpClient = new HttpClient(responseHandler);
        using IEmbeddingGenerator<string, Embedding<float>> iEmbeddingGenerator = CreateGenerator(httpClient);

        // Act
        GeneratedEmbeddings<Embedding<float>> generatedEmbeddings = await iEmbeddingGenerator.GenerateAsync(
        [
            "First",
            "Second"
        ]);

        // Assert
        Assert.Equal(
            new[] { 1f, 0f },
            generatedEmbeddings[0].Vector.ToArray());
        Assert.Equal(
            new[] { 0f, 1f },
            generatedEmbeddings[1].Vector.ToArray());
        Assert.All(
            generatedEmbeddings,
            embedding => Assert.Equal(
                "test-model",
                embedding.ModelId));
        Assert.Equal(
            12,
            generatedEmbeddings.Usage?.InputTokenCount);
    }

    // Duplicate indexes cannot be repaired by assuming response order matches input order.
    [Fact]
    public async Task Duplicate_indexes_are_rejected()
    {
        // Arrange
        JsonNode responseJsonNode = JsonNode.Parse(ResponseString)!;
        responseJsonNode["data"]![0]!["index"] = 0;
        using var responseHandler = new ResponseHandler(responseJsonNode.ToJsonString());
        using var httpClient = new HttpClient(responseHandler);
        using IEmbeddingGenerator<string, Embedding<float>> iEmbeddingGenerator = CreateGenerator(httpClient);

        // Act
        InvalidDataException errorInvalidDataException = await Assert.ThrowsAsync<InvalidDataException>(async () =>
            await iEmbeddingGenerator.GenerateAsync(["First", "Second"]));

        // Assert
        Assert.Contains(
            "indexes",
            errorInvalidDataException.Message);
    }

    // OpenRouter has no omitted-zero convention; missing and invalid indexes must fail explicitly.
    [Theory]
    [InlineData("missing")]
    [InlineData("negative")]
    [InlineData("out-of-range")]
    public async Task Invalid_indexes_are_rejected(string invalidIndexString)
    {
        // Arrange
        JsonNode responseJsonNode = JsonNode.Parse(ResponseString)!;
        JsonObject entryJsonObject = responseJsonNode["data"]![1]!.AsObject();
        if (invalidIndexString == "missing")
        {
            entryJsonObject.Remove("index");
        }
        else if (invalidIndexString == "negative")
        {
            entryJsonObject["index"] = -1;
        }
        else
        {
            entryJsonObject["index"] = 2;
        }

        using var responseHandler = new ResponseHandler(responseJsonNode.ToJsonString());
        using var httpClient = new HttpClient(responseHandler);
        using IEmbeddingGenerator<string, Embedding<float>> iEmbeddingGenerator = CreateGenerator(httpClient);

        // Act
        InvalidDataException errorInvalidDataException = await Assert.ThrowsAsync<InvalidDataException>(async () =>
            await iEmbeddingGenerator.GenerateAsync(["First", "Second"]));

        // Assert
        Assert.Contains(
            "indexes",
            errorInvalidDataException.Message);
    }

    private static IEmbeddingGenerator<string, Embedding<float>> CreateGenerator(HttpClient httpClient)
    {
        return new AiClientFactory().CreateEmbeddingGenerator(
            new(
                "OpenRouter",
                "test-key",
                "test-model",
                new Uri("https://example.invalid/v1")),
            httpClient);
    }
}
