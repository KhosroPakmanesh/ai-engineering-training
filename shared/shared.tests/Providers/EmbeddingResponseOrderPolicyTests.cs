using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using Xunit;

public class EmbeddingResponseOrderPolicyTests
{
    private const string Response = """
        {"object":"list","model":"test-model","data":[
          {"object":"embedding","index":1,"embedding":[0.0,1.0]},
          {"object":"embedding","index":0,"embedding":[1.0,0.0]}],
         "usage":{"prompt_tokens":12,"total_tokens":12}}
        """;

    // The common Microsoft interface must return input order while preserving vector values, model, and usage.
    [Fact]
    public async Task Response_indexes_restore_input_order_before_conversion()
    {
        using var handler = new ResponseHandler(Response);
        using var httpClient = new HttpClient(handler);
        using IEmbeddingGenerator<string, Embedding<float>> generator = CreateGenerator(httpClient);

        GeneratedEmbeddings<Embedding<float>> embeddings = await generator.GenerateAsync(["First", "Second"]);

        Assert.Equal(new[] { 1f, 0f }, embeddings[0].Vector.ToArray());
        Assert.Equal(new[] { 0f, 1f }, embeddings[1].Vector.ToArray());
        Assert.All(embeddings, embedding => Assert.Equal("test-model", embedding.ModelId));
        Assert.Equal(12, embeddings.Usage?.InputTokenCount);
    }

    // Duplicate indexes cannot be repaired by assuming response order matches input order.
    [Fact]
    public async Task Duplicate_indexes_are_rejected()
    {
        JsonNode response = JsonNode.Parse(Response)!;
        response["data"]![0]!["index"] = 0;
        using var handler = new ResponseHandler(response.ToJsonString());
        using var httpClient = new HttpClient(handler);
        using IEmbeddingGenerator<string, Embedding<float>> generator = CreateGenerator(httpClient);

        InvalidDataException error = await Assert.ThrowsAsync<InvalidDataException>(async () =>
            await generator.GenerateAsync(["First", "Second"]));

        Assert.Contains("indexes", error.Message);
    }

    // OpenRouter has no omitted-zero convention; missing and invalid indexes must fail explicitly.
    [Theory]
    [InlineData("missing")]
    [InlineData("negative")]
    [InlineData("out-of-range")]
    public async Task Invalid_indexes_are_rejected(string invalidIndex)
    {
        JsonNode response = JsonNode.Parse(Response)!;
        JsonObject entry = response["data"]![1]!.AsObject();
        if (invalidIndex == "missing") entry.Remove("index");
        else if (invalidIndex == "negative") entry["index"] = -1;
        else entry["index"] = 2;
        using var handler = new ResponseHandler(response.ToJsonString());
        using var httpClient = new HttpClient(handler);
        using IEmbeddingGenerator<string, Embedding<float>> generator = CreateGenerator(httpClient);

        InvalidDataException error = await Assert.ThrowsAsync<InvalidDataException>(async () =>
            await generator.GenerateAsync(["First", "Second"]));

        Assert.Contains("indexes", error.Message);
    }

    private static IEmbeddingGenerator<string, Embedding<float>> CreateGenerator(HttpClient httpClient) =>
        new AiClientFactory().CreateEmbeddingGenerator(
            new("OpenRouter", "test-key", "test-model", new Uri("https://example.invalid/v1")), httpClient);

    // Replace HTTP while exercising the real SDK policy and Microsoft's standard embedding adapter.
    private sealed class ResponseHandler(string response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json")
            });
    }
}
