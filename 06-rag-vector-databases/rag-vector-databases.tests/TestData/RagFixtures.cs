namespace ResumeRag.Tests.TestData;

public static class RagFixtures
{
    // Return fresh lists so tests cannot change another test's source records or vectors.
    public static ChunkIndex CreateIndex()
    {
        return new(
            new(
                "OpenRouter",
                "test-embedding",
                2,
                650,
                80),
            [
                new(
            new(
                "project-a.md#chunk-01",
                "project-a.md",
                "Delivery",
                "Mira used Docker to package a C# API.",
                "hash-a"),
            [1, 0]),
                new(
            new(
                "project-b.md#chunk-01",
                "project-b.md",
                "Storage",
                "Mira used PostgreSQL for reservations.",
                "hash-b"),
            [0, 1])
            ]);
    }

    // Keep the complete question set valid before an acceptance-flow test makes any request.
    public static List<RagQuestion> CreateQuestions()
    {
        return
        [
            new(
            "Where did I use Docker?",
            "Docker"),
            new(
                "Where did I use PostgreSQL?",
                "PostgreSQL"),
            new(
            "Where did I use Kubernetes?",
            "Kubernetes")
        ];
    }

    // Use the application's copied source inputs for acceptance-flow and integration checks.
    public static async Task<List<DocumentChunk>> LoadChunksAsync()
    {
        List<SourceDocument> sourceDocuments = await DocumentsLoader.LoadAsync(Path.Combine(
            AppContext.BaseDirectory,
            "SampleData"));
        InputsValidator.Validate(
            sourceDocuments,
            CreateQuestions(),
            650,
            80,
            3,
            0.2,
            2400);
        return MarkdownChunker.Chunk(
            sourceDocuments,
            650,
            80);
    }
}
