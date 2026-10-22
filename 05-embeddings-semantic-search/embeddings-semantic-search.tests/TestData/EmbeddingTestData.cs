using System.Net;
using System.Text;
using System.Text.Json;

public static class EmbeddingTestData
{
    // Return fresh copied inputs so tests never share mutable projects or index files.
    public static Task<List<ProjectDescription>> LoadAsync() => ProjectDescriptionsLoader.LoadAsync(
        Path.Combine(AppContext.BaseDirectory, "SampleData", "projects.json"));

    // Keep vector values explicit in each test; this helper only writes the provider's JSON format.
    public static string Response(List<List<float>> vectors, string model = "test-model", List<int>? indexes = null) =>
        JsonSerializer.Serialize(new
        {
            @object = "list", model,
            data = vectors.Select((vector, position) => new
            {
                @object = "embedding", index = indexes?[position] ?? position, embedding = vector
            }),
            usage = new { prompt_tokens = 12, total_tokens = 12 }
        });
}

// Replace HTTP only; the production SDK still serializes requests and reads responses.
public sealed class EmbeddingResponseHandler(string response, Action? beforeResponse = null) : HttpMessageHandler
{
    public List<string> RequestBodies { get; } = [];
    public bool HadCancelableToken { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
        HadCancelableToken = cancellationToken.CanBeCanceled;
        beforeResponse?.Invoke();
        cancellationToken.ThrowIfCancellationRequested();
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(response, Encoding.UTF8, "application/json")
        };
    }
}
