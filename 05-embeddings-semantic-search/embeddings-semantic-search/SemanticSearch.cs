using Microsoft.Extensions.VectorData;

public static class SemanticSearch
{
    // Validated index and query vectors have length one; their dot product is exact cosine search over every row.
    public static List<SearchResult> Rank(
        SearchIndex searchIndex,
        List<float> normalizedQueryFloats,
        int topKInt)
    {
        if (topKInt <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(topKInt));
        }

        var scoredProjectValueTuples = new List<(ProjectDescription ProjectDescription, double ScoreDouble)>();

        foreach (IndexedProject indexedProject in searchIndex.IndexedProjects)
        {
            double scoreDouble = 0;
            for (int dimensionInt = 0; dimensionInt < searchIndex.DimensionsInt; dimensionInt++)
            {
                // We show each coordinate's contribution below. The double sum can round differently from a float
                // result.
                scoreDouble += (double)indexedProject.VectorFloats[dimensionInt] * normalizedQueryFloats[dimensionInt];
            }

            // Microsoft alternative: replace the entire coordinate loop above with one of these calls.
            // TensorPrimitives.Dot needs normalized vectors here to give cosine similarity.
            //scoreDouble = TensorPrimitives.Dot(
            //    CollectionsMarshal.AsSpan(indexedProject.VectorFloats),
            //    CollectionsMarshal.AsSpan(normalizedQueryFloats));

            // TensorPrimitives.CosineSimilarity also handles vectors that have not been normalized.
            //scoreDouble = TensorPrimitives.CosineSimilarity(
            //    CollectionsMarshal.AsSpan(indexedProject.VectorFloats),
            //    CollectionsMarshal.AsSpan(normalizedQueryFloats));

            scoredProjectValueTuples.Add((indexedProject.ProjectDescription, Math.Clamp(
                scoreDouble,
                -1,
                1)));
        }

        return scoredProjectValueTuples
            .OrderByDescending(scoredProjectValueTuple =>
            scoredProjectValueTuple.ScoreDouble)
            .ThenBy(
                scoredProjectValueTuple => scoredProjectValueTuple.ProjectDescription.IdString,
                StringComparer.Ordinal)
            .Take(topKInt)
            .Select((
                scoredProjectValueTuple,
                indexInt) => new SearchResult(
                    indexInt + 1,
                    scoredProjectValueTuple.ProjectDescription.IdString,
                    scoredProjectValueTuple.ScoreDouble,
                    scoredProjectValueTuple.ProjectDescription.TextString[..Math.Min(
                        scoredProjectValueTuple.ProjectDescription.TextString.Length,
                        90)]))
            .ToList();
    }

    // Search a collection configured for cosine distance and convert its distances into similarity scores.
    public static async Task<List<SearchResult>> RankAsync(
        VectorStoreCollection<string, VectorProject> vectorStoreCollection,
        SearchIndex searchIndex,
        ReadOnlyMemory<float> queryVectorReadOnlyMemory,
        int topKInt,
        CancellationToken cancellationToken = default)
    {
        if (topKInt <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(topKInt));
        }

        var vectorSearchOptions = new VectorSearchOptions<VectorProject>
        {
            Filter = vectorProject => vectorProject.ProviderString == searchIndex.ProviderString &&
                vectorProject.ModelString == searchIndex.ModelString
        };
        var scoredVectorProjectValueTuples = new List<(VectorProject VectorProject, double ScoreDouble)>();
        await foreach (VectorSearchResult<VectorProject> matchVectorSearchResult in
            vectorStoreCollection.SearchAsync(
                queryVectorReadOnlyMemory,
                topKInt,
                vectorSearchOptions,
                cancellationToken))
        {
            double? returnedScoreDouble = matchVectorSearchResult.Score;
            double distanceDouble = returnedScoreDouble
                ?? throw new InvalidDataException("The store omitted its distance.");
            scoredVectorProjectValueTuples.Add((matchVectorSearchResult.Record, Math.Clamp(
                1 - distanceDouble,
                -1,
                1)));
        }

        // Sort the returned candidates; equal scores at the top-k boundary may differ from the full scan.
        return scoredVectorProjectValueTuples
            .OrderByDescending(scoredVectorProjectValueTuple =>
            scoredVectorProjectValueTuple.ScoreDouble)
            .ThenBy(
                scoredVectorProjectValueTuple => scoredVectorProjectValueTuple.VectorProject.IdString,
                StringComparer.Ordinal)
            .Select((
                scoredVectorProjectValueTuple,
                indexInt) => new SearchResult(
                    indexInt + 1,
                    scoredVectorProjectValueTuple.VectorProject.IdString,
                    scoredVectorProjectValueTuple.ScoreDouble,
                    scoredVectorProjectValueTuple.VectorProject.TextString[..Math.Min(
                        scoredVectorProjectValueTuple.VectorProject.TextString.Length,
                        90)]))
            .ToList();
    }
}
