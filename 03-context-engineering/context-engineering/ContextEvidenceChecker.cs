using System.Text.RegularExpressions;

public static class ContextEvidenceChecker
{
    // Check project IDs and quotes in code; the learner still decides whether they support the answer.
    public static List<string> CheckEvidence(string answer, List<ProjectSection> included, List<string>? expectedIds = null)
    {
        if (answer.Trim() == ContextCreatorHelpers.UncertaintyResponse)
            return expectedIds is { Count: > 0 } && expectedIds.Any(id => included.Any(section => section.Id == id))
                ? ["Unexpected uncertainty: the fixture's supporting section was supplied."] : [];

        var flags = new List<string>();
        if (string.IsNullOrWhiteSpace(answer)) flags.Add("Empty answer.");
        if (expectedIds is { Count: 0 }) flags.Add("Expected the explicit uncertainty response for this unsupported fixture question.");
        List<string> cited = CitedIds(answer);
        if (cited.Count == 0) flags.Add("Missing section citation.");
        List<string> quotes = ReadQuotes(answer);
        CheckCitations(cited, quotes, included, flags);
        return flags;
    }
    // Accept references such as [P1] and 【P1】, and return each project ID once.
    public static List<string> CitedIds(string answer) => Regex.Matches(answer, @"\[(?<id>P\d+)\]|【(?<id>P\d+)】")
        .Select(match => match.Groups["id"].Value).Distinct(StringComparer.Ordinal).ToList();
    // Read quoted text between either straight or curly quotation marks.
    private static List<string> ReadQuotes(string answer) => Regex.Matches(answer, "[\"“](?<quote>[^\"”]+)[\"”]")
        .Select(match => match.Groups["quote"].Value).ToList();
    // A citation needs both a supplied source and an exact quote from that source.
    private static void CheckCitations(List<string> cited, List<string> quotes, List<ProjectSection> included, List<string> flags)
    {
        foreach (string id in cited)
        {
            ProjectSection? section = included.FirstOrDefault(section => section.Id == id);
            if (section is null) flags.Add($"Rejected citation [{id}]: this section was not supplied.");
            else if (!quotes.Any(quote => quote.Length > 0 && section.Text.Contains(quote, StringComparison.Ordinal)))
                flags.Add($"Missing exact evidence quote from [{id}].");
        }
    }
}
