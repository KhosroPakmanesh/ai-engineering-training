using System.Text.RegularExpressions;

public static class ProjectSectionParser
{

    // Keep each project's full text so its quotes can be checked against the original.
    public static List<ProjectSection> ParseProjectSection(string markdownString)
    {
        MatchCollection headingsMatchCollection = Regex.Matches(markdownString, @"^## \[(?<id>P[1-9]\d*)\] (?<heading>[^\r\n]+)\r?$",
            RegexOptions.Multiline);
        if (headingsMatchCollection.Count == 0 || headingsMatchCollection.Count != Regex.Matches(markdownString, @"^## ", RegexOptions.Multiline).Count)
        {
            throw new InvalidDataException("Each project section needs a heading such as '## [P1] Inventory service'.");
        }

        var projectSections = new List<ProjectSection>();
        for (int indexInt = 0; indexInt < headingsMatchCollection.Count; indexInt++)
        {
            Match headingMatch = headingsMatchCollection[indexInt];
            int endInt = indexInt + 1 < headingsMatchCollection.Count ? headingsMatchCollection[indexInt + 1].Index : markdownString.Length;
            projectSections.Add(new(headingMatch.Groups["id"].Value, headingMatch.Groups["heading"].Value,
                markdownString[headingMatch.Index..endInt].Trim()));
        }

        if (projectSections.Select(projectSection => projectSection.IdString).Distinct(StringComparer.Ordinal).Count() != projectSections.Count)
        {
            throw new InvalidDataException("Project section IDs must be unique.");
        }

        return projectSections;
    }
}
