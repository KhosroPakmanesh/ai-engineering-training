using System.ClientModel.Primitives;
using System.Text.Json.Nodes;

// Microsoft's adapter drops raw response indexes, so check their mapping before it reads the vectors.
internal sealed class EmbeddingResponseOrderPolicy : PipelinePolicy
{
    // Support the SDK's synchronous path with the same index checks and ordering.
    public override void Process(PipelineMessage pipelineMessage, IReadOnlyList<PipelinePolicy> pipelinePolicies, int currentIndexInt)
    {
        ProcessNext(pipelineMessage, pipelinePolicies, currentIndexInt);
        OrderByInputIndex(pipelineMessage);
    }

    // Apply the wire-format checks after HTTP completes and before the SDK converts the response.
    public override async ValueTask ProcessAsync(PipelineMessage pipelineMessage, IReadOnlyList<PipelinePolicy> pipelinePolicies,
        int currentIndexInt)
    {
        await ProcessNextAsync(pipelineMessage, pipelinePolicies, currentIndexInt);
        OrderByInputIndex(pipelineMessage);
    }

    // Preserve vector values and metadata; change only the order of an unambiguous collection.
    private static void OrderByInputIndex(PipelineMessage pipelineMessage)
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

        var indexeIntsHashSet = new HashSet<int>();
        bool alreadyOrderedBool = true;
        for (int positionInt = 0; positionInt < dataJsonArray.Count; positionInt++)
        {
            if (dataJsonArray[positionInt] is not JsonObject itemJsonObject || itemJsonObject["index"] is not JsonValue indexJsonValue ||
                !indexJsonValue.TryGetValue(out int inputIndexInt) || inputIndexInt < 0 || inputIndexInt >= dataJsonArray.Count || !indexeIntsHashSet.Add(inputIndexInt))
            {
                throw new InvalidDataException("Embedding indexes must identify each input exactly once, from zero to input count minus one.");
            }

            if (inputIndexInt != positionInt)
            {
                alreadyOrderedBool = false;
            }
        }
        if (alreadyOrderedBool)
        {
            return;
        }

        var orderedDataJsonArray = new JsonArray();
        foreach (JsonNode? entryJsonNode in dataJsonArray.OrderBy(entryJsonNode => entryJsonNode!["index"]!.GetValue<int>()))
        {
            orderedDataJsonArray.Add(entryJsonNode!.DeepClone());
        }

        bodyJsonNode!["data"] = orderedDataJsonArray;
        pipelineResponse.ContentStream?.Dispose();
        pipelineResponse.ContentStream = BinaryData.FromString(bodyJsonNode.ToJsonString()).ToStream();
        pipelineResponse.BufferContent(pipelineMessage.CancellationToken);
    }
}
