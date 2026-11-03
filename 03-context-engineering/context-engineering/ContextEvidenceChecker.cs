using System.Text.RegularExpressions;

public static class ContextEvidenceChecker
{
    // Check project IDs and quotes in code; the learner still decides whether they support the answer.
    public static List<string> CheckEvidence(string answerString, List<ProjectSection> includedProjectSections,
        List<string>? expectedIdStrings = null)
    {
        if (answerString.Trim() == ContextCreatorHelpers.UncertaintyResponseString)
        {
            return expectedIdStrings is { Count: > 0 } && expectedIdStrings.Any(idString => includedProjectSections.Any(projectSection => projectSection.IdString == idString))
                        ? ["Unexpected uncertainty: the fixture's supporting section was supplied."] : [];
        }

        var flagStrings = new List<string>();
        if (string.IsNullOrWhiteSpace(answerString))
        {
            flagStrings.Add("Empty answer.");
        }

        if (expectedIdStrings is { Count: 0 })
        {
            flagStrings.Add("Expected the explicit uncertainty response for this unsupported fixture question.");
        }

        List<string> citedStrings = CitedIds(answerString);
        if (citedStrings.Count == 0)
        {
            flagStrings.Add("Missing section citation.");
        }

        List<string> quoteStrings = ReadQuotes(answerString);
        CheckCitations(citedStrings, quoteStrings, includedProjectSections, flagStrings);
        return flagStrings;
    }

    // Accept references such as [P1] and 【P1】, and return each project ID once.
    public static List<string> CitedIds(string answerString)
    {
        return Regex.Matches(answerString, @"\[(?<id>P\d+)\]|【(?<id>P\d+)】")
        .Select(match => match.Groups["id"].Value).Distinct(StringComparer.Ordinal).ToList();
    }

    // Read quoted text between either straight or curly quotation marks.
    private static List<string> ReadQuotes(string answerString)
    {
        return Regex.Matches(answerString, "[\"“](?<quote>[^\"”]+)[\"”]")
        .Select(match => match.Groups["quote"].Value).ToList();
    }

    // A citation needs both a supplied source and an exact quote from that source.
    private static void CheckCitations(List<string> citedStrings, List<string> quoteStrings, List<ProjectSection> includedProjectSections,
        List<string> flagStrings)
    {
        foreach (string idString in citedStrings)
        {
            ProjectSection? projectSection = includedProjectSections.FirstOrDefault(projectSection => projectSection.IdString == idString);
            if (projectSection is null)
            {
                flagStrings.Add($"Rejected citation [{idString}]: this section was not supplied.");
            }
            else if (!quoteStrings.Any(quoteString => quoteString.Length > 0 && projectSection.TextString.Contains(quoteString, StringComparison.Ordinal)))
            {
                flagStrings.Add($"Missing exact evidence quote from [{idString}].");
            }
        }
    }
}
