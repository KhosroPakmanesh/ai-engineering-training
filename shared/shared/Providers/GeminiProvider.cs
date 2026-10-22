using System.ClientModel.Primitives;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;

internal sealed class GeminiProvider : IAiProviderStrategy
{
    public string Name => "Gemini";
    public string SettingsPrefix => "GEMINI";
    public Uri Endpoint { get; } = new("https://generativelanguage.googleapis.com/v1beta/openai/");

    // Google's compatibility endpoint needs no OpenRouter routing options.
    public IChatClient CreateClient(ProviderSettings settings, HttpClient? httpClient) =>
        OpenAiCompatibleClient.CreateClient(settings, httpClient);

    // Keep Google's omitted-zero convention below Microsoft's common embedding interface.
    public IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGenerator(ProviderSettings settings, HttpClient? httpClient) =>
        OpenAiCompatibleClient.CreateEmbeddingGenerator(settings, httpClient, new OmittedZeroIndexPolicy());

    // Gemini can omit a numeric zero in its JSON. Restore only that unambiguous default, never guess document order.
    private sealed class OmittedZeroIndexPolicy : PipelinePolicy
    {
        // Keep the same wire convention when the SDK is called synchronously.
        public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
        {
            ProcessNext(message, pipeline, currentIndex);
            RestoreZeroIndex(message);
        }

        // Apply the convention after HTTP completes and before the SDK reads the vectors.
        public override async ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
        {
            await ProcessNextAsync(message, pipeline, currentIndex);
            RestoreZeroIndex(message);
        }

        // All positive indexes must be present exactly once before one omitted index can mean zero.
        private static void RestoreZeroIndex(PipelineMessage message)
        {
            PipelineResponse? response = message.Response;
            if (response is null || response.Status != 200 || !message.BufferResponse) return;
            JsonNode? body = JsonNode.Parse(response.Content);
            if (body?["data"] is not JsonArray data || data.Count == 0) return;
            JsonObject? missingZero = null;
            var positiveIndexes = new HashSet<int>();
            foreach (JsonNode? entry in data)
            {
                if (entry is not JsonObject item) return;
                if (!item.ContainsKey("index"))
                {
                    if (missingZero is not null) return;
                    missingZero = item;
                }
                else if (item["index"] is not JsonValue index || !index.TryGetValue(out int position) ||
                    position <= 0 || position >= data.Count || !positiveIndexes.Add(position))
                    return;
            }
            if (missingZero is null || positiveIndexes.Count != data.Count - 1) return;
            missingZero["index"] = 0;
            response.ContentStream?.Dispose();
            response.ContentStream = BinaryData.FromString(body!.ToJsonString()).ToStream();
            response.BufferContent(message.CancellationToken);
        }
    }
}
