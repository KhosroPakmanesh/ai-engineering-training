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
                score += (double)indexedProject.Vector[dimension] * normalizedQuery[dimension];
            scoredProjects.Add((indexedProject.Project, Math.Clamp(score, -1, 1)));
        }

        return scoredProjects.OrderByDescending(item => item.Score)
            .ThenBy(item => item.Project.Id, StringComparer.Ordinal)
            .Take(topK)
            .Select((item, index) => new SearchResult(index + 1, item.Project.Id, item.Score,
                item.Project.Text[..Math.Min(item.Project.Text.Length, 90)]))
            .ToList();
    }
}
