using System.Text.RegularExpressions;

// Check observable source evidence; factual meaning still needs manual review.
public static class ContextEvidence
{
    // The supplied context determines which citations and quotations are allowed.
    public static string[] CheckEvidence(string answer, ProjectSection[] included, string[]? expectedIds = null)
    {
        // A decline is suspicious only when a fixture's supporting source was supplied.
        if (answer.Trim() == ContextComparison.UncertaintyResponse)
            return expectedIds is { Length: > 0 } && expectedIds.Any(id => included.Any(section => section.Id == id))
                ? ["Unexpected uncertainty: the fixture's supporting section was supplied."] : [];

        // Keep fixture, citation, and quotation failures in the original report order.
        var flags = new List<string>();
        if (string.IsNullOrWhiteSpace(answer)) flags.Add("Empty answer.");
        if (expectedIds is { Length: 0 }) flags.Add("Expected the explicit uncertainty response for this unsupported fixture question.");
        string[] cited = CitedIds(answer);
        if (cited.Length == 0) flags.Add("Missing section citation.");
        string[] quotes = ReadQuotes(answer);
        CheckCitations(cited, quotes, included, flags);
        return flags.ToArray();
    }

    // Accept both citation styles while checking each section ID only once.
    public static string[] CitedIds(string answer) => Regex.Matches(answer, @"\[(?<id>P\d+)\]|【(?<id>P\d+)】")
        .Select(match => match.Groups["id"].Value).Distinct(StringComparer.Ordinal).ToArray();

    // Match literal quotations rather than accepting paraphrases as source spans.
    private static string[] ReadQuotes(string answer) => Regex.Matches(answer, "[\"“](?<quote>[^\"”]+)[\"”]")
        .Select(match => match.Groups["quote"].Value).ToArray();

    // Every cited section must have been supplied and must contain an exact quoted span.
    private static void CheckCitations(string[] cited, string[] quotes, ProjectSection[] included, List<string> flags)
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
