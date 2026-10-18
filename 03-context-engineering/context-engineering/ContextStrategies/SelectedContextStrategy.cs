using System.Text.RegularExpressions;

public static class SelectedContextStrategy
{
    private const int SelectedSectionLimit = 2;
    private static readonly HashSet<string> StopWords = new(
        "a an and are as at be by did do does for from has have how in is it of on or that the their this to use used uses using was were what when where which who with project projects".Split(' '),
        StringComparer.OrdinalIgnoreCase);
    public const string StrategyDescription = "Distinct keyword matches: heading=3, body=1; ties use document order; take up to two sections with a positive score.";

    // Choose projects by matching question words, without making another model call.
    public static ContextStrategy Prepare(List<ProjectSection> sections, string question,
        int characterCap = ContextCreatorHelpers.ContextCharacterCap)
    {
        if (characterCap <= 0) throw new ArgumentOutOfRangeException(nameof(characterCap));
        List<string> keywords = Words(question).Where(word => !StopWords.Contains(word)).ToList();
        List<ProjectSection> included = RankSections(sections, keywords);
        return ContextCreator.CreateContext(included, "SELECTED", characterCap);
    }
    // Take the highest-scoring sections; keep document order when scores are equal.
    private static List<ProjectSection> RankSections(List<ProjectSection> sections, List<string> keywords) => sections
        .Select((section, index) => new { Section = section, Index = index, Score = ScoreSection(section, keywords) })
        .Where(item => item.Score > 0)
        .OrderByDescending(item => item.Score)
        .ThenBy(item => item.Index)
        .Take(SelectedSectionLimit)
        .Select(item => item.Section)
        .ToList();
    // Give 3 points for a heading match, otherwise 1 for a text match; count each keyword once.
    private static int ScoreSection(ProjectSection section, List<string> keywords)
    {
        int score = 0;
        foreach (string keyword in keywords)
        {
            if (Words(section.Heading).Contains(keyword)) score += 3;
            else if (Words(section.Text).Contains(keyword)) score += 1;
        }
        return score;
    }
    // Ignore letter case and punctuation, and count repeated words only once.
    private static HashSet<string> Words(string text) => Regex.Matches(text.ToLowerInvariant(), @"[a-z0-9]+")
        .Select(match => match.Value).ToHashSet(StringComparer.Ordinal);
}
