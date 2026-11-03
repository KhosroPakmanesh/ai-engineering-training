using System.Text.RegularExpressions;

public static class SelectedContextStrategy
{
    private const int SelectedSectionLimitInt = 2;
    private static readonly HashSet<string> StopWordStringsHashSet = new(
        "a an and are as at be by did do does for from has have how in is it of on or that the their this to use used uses using was were what when where which who with project projects".Split(' '),
        StringComparer.OrdinalIgnoreCase);
    public const string StrategyDescriptionString = "Distinct keyword matches: heading=3, body=1; ties use document order; take up to two sections with a positive score.";

    // Choose projects by matching question words, without making another model call.
    public static ContextStrategy Prepare(List<ProjectSection> projectSections, string questionString,
        int characterCapInt = ContextCreatorHelpers.ContextCharacterCapInt)
    {
        if (characterCapInt <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(characterCapInt));
        }

        List<string> keywordStrings = Words(questionString).Where(wordString => !StopWordStringsHashSet.Contains(wordString)).ToList();
        List<ProjectSection> includedProjectSections = RankSections(projectSections, keywordStrings);
        return ContextCreator.CreateContext(includedProjectSections, "SELECTED", characterCapInt);
    }

    // Take the highest-scoring sections; keep document order when scores are equal.
    private static List<ProjectSection> RankSections(List<ProjectSection> projectSections, List<string> keywordStrings)
    {
        return projectSections
            .Select((projectSection, indexInt) => new
            {
                ProjectSection = projectSection,
                IndexInt = indexInt,
                ScoreInt = ScoreSection(projectSection, keywordStrings)
            })
            .Where(scoredSectionAnonymousObject => scoredSectionAnonymousObject.ScoreInt > 0)
            .OrderByDescending(scoredSectionAnonymousObject => scoredSectionAnonymousObject.ScoreInt)
            .ThenBy(scoredSectionAnonymousObject => scoredSectionAnonymousObject.IndexInt)
            .Take(SelectedSectionLimitInt)
            .Select(scoredSectionAnonymousObject => scoredSectionAnonymousObject.ProjectSection)
            .ToList();
    }

    // Give 3 points for a heading match, otherwise 1 for a text match; count each keyword once.
    private static int ScoreSection(ProjectSection projectSection, List<string> keywordStrings)
    {
        int scoreInt = 0;
        foreach (string keywordString in keywordStrings)
        {
            if (Words(projectSection.HeadingString).Contains(keywordString))
            {
                scoreInt += 3;
            }
            else if (Words(projectSection.TextString).Contains(keywordString))
            {
                scoreInt += 1;
            }
        }
        return scoreInt;
    }

    // Ignore letter case and punctuation, and count repeated words only once.
    private static HashSet<string> Words(string textString)
    {
        return Regex.Matches(textString.ToLowerInvariant(), @"[a-z0-9]+")
            .Select(match => match.Value).ToHashSet(StringComparer.Ordinal);
    }
}
