using System.Text.RegularExpressions;

public static class LexicalSearch
{
    // Count each matching query word once. Both searches use only each project's original Text field.
    public static List<SearchResult> Rank(List<ProjectDescription> projectDescriptions, string queryString, int topKInt)
    {
        if (topKInt <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(topKInt));
        }

        HashSet<string> queryWordStringsHashSet = Words(queryString);
        return projectDescriptions.Select(projectDescription => new
        {
            ProjectDescription = projectDescription,
            ScoreInt = Words(projectDescription.TextString).Count(queryWordStringsHashSet.Contains)
        })
            .OrderByDescending(itemAnonymousObject => itemAnonymousObject.ScoreInt)
            .ThenBy(itemAnonymousObject => itemAnonymousObject.ProjectDescription.IdString, StringComparer.Ordinal)
            .Take(topKInt)
            .Select((itemAnonymousObject, indexInt) =>
                new SearchResult(indexInt + 1, itemAnonymousObject.ProjectDescription.IdString, itemAnonymousObject.ScoreInt,
                    itemAnonymousObject.ProjectDescription.TextString[..Math.Min(itemAnonymousObject.ProjectDescription.TextString.Length, 90)]))
            .ToList();
    }

    // Treat punctuation as word boundaries and compare letters without case differences.
    private static HashSet<string> Words(string textString)
    {
        return Regex.Matches(textString.ToLowerInvariant(), @"[\p{L}\p{N}]+")
        .Select(match => match.Value).ToHashSet(StringComparer.Ordinal);
    }
}
