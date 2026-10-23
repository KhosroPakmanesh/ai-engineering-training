using Microsoft.Extensions.VectorData;
using System.Numerics.Tensors;
using System.Runtime.InteropServices;

public static class SemanticSearch
{
    // Validated index and query vectors have length one; their dot product is exact cosine search over every row.
    public static List<SearchResult> Rank(SearchIndex searchIndex, List<float> normalizedQuery, int topK)
    {
        if (topK <= 0) throw new ArgumentOutOfRangeException(nameof(topK));

        var scoredProjects = new List<(ProjectDescription Project, double Score)>();

        foreach (IndexedProject indexedProject in searchIndex.Projects)
        {
            double score = 0;
            for (int dimension = 0; dimension < searchIndex.Dimensions; dimension++)
            {
                // We show each coordinate's contribution below. The double sum can round differently from a float result.
                score += (double)indexedProject.Vector[dimension] * normalizedQuery[dimension];
            }

            // Microsoft alternative: replace the entire coordinate loop above with one of these calls.
            // TensorPrimitives.Dot needs normalized vectors here to give cosine similarity.
            //score = TensorPrimitives.Dot(
            //    CollectionsMarshal.AsSpan(indexedProject.Vector),
            //    CollectionsMarshal.AsSpan(normalizedQuery));

            // TensorPrimitives.CosineSimilarity also handles vectors that have not been normalized.
            //score = TensorPrimitives.CosineSimilarity(
            //    CollectionsMarshal.AsSpan(indexedProject.Vector),
            //    CollectionsMarshal.AsSpan(normalizedQuery));

            scoredProjects.Add((indexedProject.Project, Math.Clamp(score, -1, 1)));
        }

        return scoredProjects.OrderByDescending(item => item.Score)
            .ThenBy(item => item.Project.Id, StringComparer.Ordinal)
            .Take(topK)
            .Select((item, index) => new SearchResult(index + 1, item.Project.Id, item.Score,
                item.Project.Text[..Math.Min(item.Project.Text.Length, 90)]))
            .ToList();
    }

    // Search a collection configured for cosine distance and convert its distances into similarity scores.
    public static async Task<List<SearchResult>> RankAsync(
        VectorStoreCollection<string, VectorProject> collection, SearchIndex searchIndex,
        ReadOnlyMemory<float> queryVector, int topK, CancellationToken cancellationToken = default)
    {
        if (topK <= 0) throw new ArgumentOutOfRangeException(nameof(topK));

        var options = new VectorSearchOptions<VectorProject>
        {
            Filter = project => project.Provider == searchIndex.Provider &&
                project.Model == searchIndex.Model
        };
        var scoredProjects = new List<(VectorProject Project, double Score)>();
        await foreach (VectorSearchResult<VectorProject> match in
            collection.SearchAsync(queryVector, topK, options, cancellationToken))
        {
            double? returnedScore = match.Score;
            double distance = returnedScore ?? throw new InvalidDataException("The store omitted its distance.");
            scoredProjects.Add((match.Record, Math.Clamp(1 - distance, -1, 1)));
        }

        // Sort the returned candidates; equal scores at the top-k boundary may differ from the full scan.
        return scoredProjects.OrderByDescending(item => item.Score)
            .ThenBy(item => item.Project.Id, StringComparer.Ordinal)
            .Select((item, index) => new SearchResult(index + 1, item.Project.Id, item.Score,
                item.Project.Text[..Math.Min(item.Project.Text.Length, 90)]))
            .ToList();
    }

    // Keep each stored vector beside its source text and embedding model metadata.
    public sealed class VectorProject
    {
        public required string Id { get; set; }
        public required string Text { get; set; }
        public required string Provider { get; set; }
        public required string Model { get; set; }
        public ReadOnlyMemory<float> Vector { get; set; }
    }
}
