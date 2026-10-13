using System.Text.RegularExpressions;

// Stable IDs connect document sections, model citations, and expected-source fixtures.
public record ProjectSection(string Id, string Heading, string Text);

// Keep the selected sections beside the text used for context-size checks.
public record SelectedContext(string Strategy, ProjectSection[] Sections, string Text);

public static class ContextSelector
{
    public const int ContextCharacterCap = 8_000;
    public const int SelectedSectionLimit = 2;
    public const string FullStrategy = "All sections in document order; reject if the context exceeds the character cap.";
    public const string SelectedStrategy = "Distinct keyword matches: heading=3, body=1; ties use document order; take up to two sections with a positive score.";

    // Common question words should not make unrelated sections appear relevant.
    private static readonly HashSet<string> StopWords = new(
        "a an and are as at be by did do does for from has have how in is it of on or that the their this to use used uses using was were what when where which who with project projects".Split(' '),
        StringComparer.OrdinalIgnoreCase);

    // Parsing gives each heading and its following text one inspectable data record.
    public static ProjectSection[] Parse(string markdown)
    {
        MatchCollection headings = FindHeadings(markdown);
        ProjectSection[] sections = ReadSections(markdown, headings);
        CheckUniqueIds(sections);
        return sections;
    }

    // Reject malformed headings so citations always have a predictable section ID.
    private static MatchCollection FindHeadings(string markdown)
    {
        MatchCollection headings = Regex.Matches(markdown,
            @"^## \[(?<id>P[1-9]\d*)\] (?<heading>[^\r\n]+)\r?$", RegexOptions.Multiline);
        if (headings.Count == 0 || headings.Count != Regex.Matches(markdown, @"^## ", RegexOptions.Multiline).Count)
            throw new InvalidDataException("Each project section needs a heading such as '## [P1] Inventory service'.");
        return headings;
    }

    // Preserve each complete section, including its heading, without rewriting the source text.
    private static ProjectSection[] ReadSections(string markdown, MatchCollection headings)
    {
        var sections = new List<ProjectSection>();
        for (int index = 0; index < headings.Count; index++)
        {
            Match heading = headings[index];
            int end = index + 1 < headings.Count ? headings[index + 1].Index : markdown.Length;
            sections.Add(new(heading.Groups["id"].Value, heading.Groups["heading"].Value,
                markdown[heading.Index..end].Trim()));
        }
        return sections.ToArray();
    }

    // Duplicate IDs would make a citation ambiguous.
    private static void CheckUniqueIds(ProjectSection[] sections)
    {
        if (sections.Select(section => section.Id).Distinct(StringComparer.Ordinal).Count() != sections.Length)
            throw new InvalidDataException("Project section IDs must be unique.");
    }

    // FULL includes every section; SELECTED applies the same local ranking rule each time.
    public static SelectedContext Select(ProjectSection[] sections, string question, bool full,
        int characterCap = ContextCharacterCap)
    {
        if (characterCap <= 0) throw new ArgumentOutOfRangeException(nameof(characterCap));
        string[] keywords = Words(question).Where(word => !StopWords.Contains(word)).ToArray();
        ProjectSection[] included = full ? sections : RankSections(sections, keywords);
        return CreateContext(included, full, characterCap);
    }

    // A stable document-order tie break makes equal scores predictable to the learner.
    private static ProjectSection[] RankSections(ProjectSection[] sections, string[] keywords) => sections
        .Select((section, index) => new { Section = section, Index = index, Score = ScoreSection(section, keywords) })
        .Where(item => item.Score > 0)
        .OrderByDescending(item => item.Score)
        .ThenBy(item => item.Index)
        .Take(SelectedSectionLimit)
        .Select(item => item.Section)
        .ToArray();

    // Each distinct keyword contributes once: heading=3, otherwise body=1.
    private static int ScoreSection(ProjectSection section, string[] keywords) => keywords.Sum(keyword =>
        Words(section.Heading).Contains(keyword) ? 3 : Words(section.Text).Contains(keyword) ? 1 : 0);

    // Reject an oversized context instead of silently removing potentially useful evidence.
    private static SelectedContext CreateContext(ProjectSection[] included, bool full, int characterCap)
    {
        string context = string.Join("\n\n", included.Select(section => section.Text));
        if (context.Length > characterCap)
            throw new InvalidDataException($"{(full ? "FULL" : "SELECTED")} context has {context.Length} characters; the cap is {characterCap}. Shorten the source document. No content was clipped.");
        return new(full ? "FULL" : "SELECTED", included, context);
    }

    // Lowercasing and deduplication make scoring independent of casing and word repetition.
    private static HashSet<string> Words(string text) => Regex.Matches(text.ToLowerInvariant(), @"[a-z0-9]+")
        .Select(match => match.Value).ToHashSet(StringComparer.Ordinal);
}
