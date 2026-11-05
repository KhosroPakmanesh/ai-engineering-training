using System.Text.RegularExpressions;

namespace ResumeRag;

public static class CitationParser
{
    // Read all bracket references so a fabricated filename cannot escape the membership check.
    public static List<string> Parse(string answerString)
    {
        return Regex.Matches(
            answerString,
            @"\[([^\[\]\r\n]+)\]")
            .Select(match => match.Groups[1].Value).ToList();
    }
}
