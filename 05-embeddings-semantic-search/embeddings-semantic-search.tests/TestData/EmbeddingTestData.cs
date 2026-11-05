using System.Text.Json;

public static class EmbeddingTestData
{
    // Return fresh copied inputs so tests never share mutable projects or index files.
    public static Task<List<ProjectDescription>> LoadAsync()
    {
        return ProjectDescriptionsLoader.LoadAsync(Path.Combine(
            AppContext.BaseDirectory,
            "SampleData",
            "projects.json"));
    }

    // Keep vector values explicit in each test; this helper only writes the provider's JSON format.
    public static string Response(
        List<List<float>> vectorsLists,
        string modelString = "test-model",
        List<int>? indexeInts = null)
    {
        return JsonSerializer.Serialize(new
        {
            @object = "list",
            model = modelString,
            data = vectorsLists.Select((
                vectorFloats,
                positionInt) => new
                {
                    @object = "embedding",
                    index = indexeInts?[positionInt] ?? positionInt,
                    embedding = vectorFloats
                }),
            usage = new
            {
                prompt_tokens = 12,
                total_tokens = 12
            }
        });
    }
}
