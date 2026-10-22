using System.ClientModel.Primitives;
using System.Text.Json.Nodes;

// Microsoft's adapter drops raw response indexes, so check their mapping before it reads the vectors.
internal sealed class EmbeddingResponseOrderPolicy : PipelinePolicy
{
    // Support the SDK's synchronous path with the same index checks and ordering.
    public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        ProcessNext(message, pipeline, currentIndex);
        OrderByInputIndex(message);
    }

    // Apply the wire-format checks after HTTP completes and before the SDK converts the response.
    public override async ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        await ProcessNextAsync(message, pipeline, currentIndex);
        OrderByInputIndex(message);
    }

    // Preserve vector values and metadata; change only the order of an unambiguous collection.
    private static void OrderByInputIndex(PipelineMessage message)
    {
        PipelineResponse? response = message.Response;
        if (response is null || response.Status != 200 || !message.BufferResponse) return;
        JsonNode? body = JsonNode.Parse(response.Content);
        if (body?["data"] is not JsonArray data || data.Count == 0) return;

        var indexes = new HashSet<int>();
        bool alreadyOrdered = true;
        for (int position = 0; position < data.Count; position++)
        {
            if (data[position] is not JsonObject item || item["index"] is not JsonValue index ||
                !index.TryGetValue(out int inputIndex) || inputIndex < 0 || inputIndex >= data.Count || !indexes.Add(inputIndex))
                throw new InvalidDataException("Embedding indexes must identify each input exactly once, from zero to input count minus one.");
            if (inputIndex != position) alreadyOrdered = false;
        }
        if (alreadyOrdered) return;

        var orderedData = new JsonArray();
        foreach (JsonNode? entry in data.OrderBy(entry => entry!["index"]!.GetValue<int>()))
            orderedData.Add(entry!.DeepClone());
        body!["data"] = orderedData;
        response.ContentStream?.Dispose();
        response.ContentStream = BinaryData.FromString(body.ToJsonString()).ToStream();
        response.BufferContent(message.CancellationToken);
    }
}
