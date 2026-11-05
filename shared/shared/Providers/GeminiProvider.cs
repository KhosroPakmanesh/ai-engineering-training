using System.ClientModel.Primitives;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;

internal sealed class GeminiProvider : IAiProviderStrategy
{
    public string NameString => "Gemini";
    public string SettingsPrefixString => "GEMINI";
    public Uri EndpointUri { get; } = new("https://generativelanguage.googleapis.com/v1beta/openai/");

    // Google's compatibility endpoint needs no OpenRouter routing options.
    public IChatClient CreateClient(
        ProviderSettings providerSettings,
        HttpClient? httpClient)
    {
        return OpenAiCompatibleClient.CreateClient(
            providerSettings,
            httpClient);
    }

    // Keep Google's omitted-zero convention below Microsoft's common embedding interface.
    public IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGenerator(
        ProviderSettings providerSettings,
        HttpClient? httpClient)
    {
        return OpenAiCompatibleClient.CreateEmbeddingGenerator(
            providerSettings,
            httpClient,
            new OmittedZeroIndexPolicy());
    }

    // Gemini can omit a numeric zero in its JSON. Restore only that unambiguous default, never guess document order.
    private sealed class OmittedZeroIndexPolicy : PipelinePolicy
    {
        // Keep the same wire convention when the SDK is called synchronously.
        public override void Process(
            PipelineMessage pipelineMessage,
            IReadOnlyList<PipelinePolicy> pipelinePolicies,
            int currentIndexInt)
        {
            ProcessNext(
                pipelineMessage,
                pipelinePolicies,
                currentIndexInt);
            RestoreZeroIndex(pipelineMessage);
        }

        // Apply the convention after HTTP completes and before the SDK reads the vectors.
        public override async ValueTask ProcessAsync(
            PipelineMessage pipelineMessage,
            IReadOnlyList<PipelinePolicy> pipelinePolicies,
            int currentIndexInt)
        {
            await ProcessNextAsync(
                pipelineMessage,
                pipelinePolicies,
                currentIndexInt);
            RestoreZeroIndex(pipelineMessage);
        }

        // All positive indexes must be present exactly once before one omitted index can mean zero.
        private static void RestoreZeroIndex(PipelineMessage pipelineMessage)
        {
            PipelineResponse? pipelineResponse = pipelineMessage.Response;
            if (pipelineResponse is null || pipelineResponse.Status != 200 || !pipelineMessage.BufferResponse)
            {
                return;
            }

            JsonNode? bodyJsonNode = JsonNode.Parse(pipelineResponse.Content);
            if (bodyJsonNode?["data"] is not JsonArray dataJsonArray || dataJsonArray.Count == 0)
            {
                return;
            }

            JsonObject? missingZeroJsonObject = null;
            var positiveIndexeIntsHashSet = new HashSet<int>();
            foreach (JsonNode? entryJsonNode in dataJsonArray)
            {
                if (entryJsonNode is not JsonObject itemJsonObject)
                {
                    return;
                }

                if (!itemJsonObject.ContainsKey("index"))
                {
                    if (missingZeroJsonObject is not null)
                    {
                        return;
                    }

                    missingZeroJsonObject = itemJsonObject;
                }
                else if (itemJsonObject["index"] is not JsonValue indexJsonValue
                    || !indexJsonValue.TryGetValue(out int positionInt) ||
                    positionInt <= 0 || positionInt >= dataJsonArray.Count
                    || !positiveIndexeIntsHashSet.Add(positionInt))
                {
                    return;
                }
            }
            if (missingZeroJsonObject is null || positiveIndexeIntsHashSet.Count != dataJsonArray.Count - 1)
            {
                return;
            }

            missingZeroJsonObject["index"] = 0;
            pipelineResponse.ContentStream?.Dispose();
            pipelineResponse.ContentStream = BinaryData.FromString(bodyJsonNode!.ToJsonString()).ToStream();
            pipelineResponse.BufferContent(pipelineMessage.CancellationToken);
        }
    }
}
