using System.Text.RegularExpressions;

public static class SelectedContextStrategy
{
    private const int SelectedSectionLimit = 2;
    private static readonly HashSet<string> StopWords = new(
        "a an and are as at be by did do does for from has have how in is it of on or that the their this to use used uses using was were what when where which who with project projects".Split(' '),
        StringComparer.OrdinalIgnoreCase);
    public const string StrategyDescription = "Distinct keyword matches: heading=3, body=1; ties use document order; take up to two sections with a positive score.";

    // Choose likely evidence locally without spending another model request.
    public static ContextStrategy Prepare(List<ProjectSection> sections, string question,
        int characterCap = ContextCreatorHelpers.ContextCharacterCap)
    {
        if (characterCap <= 0) throw new ArgumentOutOfRangeException(nameof(characterCap));
        List<string> keywords = Words(question).Where(word => !StopWords.Contains(word)).ToList();
        List<ProjectSection> included = RankSections(sections, keywords);
        return ContextCreator.CreateContext(included, "SELECTED", characterCap);
    }
    // Stronger matches come first; source order breaks ties within the section limit.
    private static List<ProjectSection> RankSections(List<ProjectSection> sections, List<string> keywords) => sections
        .Select((section, index) => new { Section = section, Index = index, Score = ScoreSection(section, keywords) })
        .Where(item => item.Score > 0)
        .OrderByDescending(item => item.Score)
        .ThenBy(item => item.Index)
        .Take(SelectedSectionLimit)
        .Select(item => item.Section)
        .ToList();
    // A heading match is stronger than a body match, and repeated words add no weight.
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
    // Match distinct words without allowing case or punctuation to change their score.
    private static HashSet<string> Words(string text) => Regex.Matches(text.ToLowerInvariant(), @"[a-z0-9]+")
        .Select(match => match.Value).ToHashSet(StringComparer.Ordinal);
}
