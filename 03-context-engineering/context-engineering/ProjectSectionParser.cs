using System.Text.RegularExpressions;

public static class ProjectSectionParser
{

    // Keep complete source sections so selected context and exact quotes stay traceable.
    public static List<ProjectSection> ParseProjectSection(string markdown)
    {
        MatchCollection headings = Regex.Matches(markdown,
            @"^## \[(?<id>P[1-9]\d*)\] (?<heading>[^\r\n]+)\r?$", RegexOptions.Multiline);
        if (headings.Count == 0 || headings.Count != Regex.Matches(markdown, @"^## ", RegexOptions.Multiline).Count)
            throw new InvalidDataException("Each project section needs a heading such as '## [P1] Inventory service'.");

        var sections = new List<ProjectSection>();
        for (int index = 0; index < headings.Count; index++)
        {
            Match heading = headings[index];
            int end = index + 1 < headings.Count ? headings[index + 1].Index : markdown.Length;
            sections.Add(new(heading.Groups["id"].Value, heading.Groups["heading"].Value,
                markdown[heading.Index..end].Trim()));
        }

        List<ProjectSection> projectSections = sections.ToList();
        if (projectSections.Select(section => section.Id).Distinct(StringComparer.Ordinal).Count() != projectSections.Count)
            throw new InvalidDataException("Project section IDs must be unique.");

        return projectSections;
    }
}
