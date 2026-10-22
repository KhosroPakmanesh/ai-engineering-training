using System.Text.RegularExpressions;

public static class LexicalSearch
{
    // Count each matching query word once. Both searches use only each project's original Text field.
    public static List<SearchResult> Rank(List<ProjectDescription> projects, string query, int topK)
    {
        if (topK <= 0) throw new ArgumentOutOfRangeException(nameof(topK));

        HashSet<string> queryWords = Words(query);
        return projects.Select(project => new
            {
                Project = project,
                Score = Words(project.Text).Count(queryWords.Contains)
            })
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Project.Id, StringComparer.Ordinal)
            .Take(topK)
            .Select((item, index) => 
                new SearchResult(index + 1, 
                item.Project.Id, 
                item.Score,
                item.Project.Text[..Math.Min(item.Project.Text.Length, 90)]))
            .ToList();
    }

    // Treat punctuation as word boundaries and compare letters without case differences.
    private static HashSet<string> Words(string text) => Regex.Matches(text.ToLowerInvariant(), @"[\p{L}\p{N}]+")
        .Select(match => match.Value).ToHashSet(StringComparer.Ordinal);
}
