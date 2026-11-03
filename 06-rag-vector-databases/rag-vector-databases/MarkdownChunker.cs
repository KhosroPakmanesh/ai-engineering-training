using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ResumeRag;

public static class MarkdownChunker
{
    // Keep paragraphs together when possible; long paragraphs are split so every chunk obeys the cap.
    public static List<DocumentChunk> Chunk(List<SourceDocument> sourceDocuments, int maximumCharactersInt, int overlapInt)
    {
        if (maximumCharactersInt <= 0 || overlapInt < 0 || overlapInt >= maximumCharactersInt)
        {
            throw new ArgumentOutOfRangeException(nameof(overlapInt), "Use a positive cap and overlap smaller than the cap.");
        }

        var documentChunks = new List<DocumentChunk>();
        foreach (SourceDocument sourceDocument in sourceDocuments)
        {
            string normalizedString = sourceDocument.TextString.Replace("\r\n", "\n").Replace('\r', '\n');
            string hashString = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedString)));
            string headingString = "";
            var sectionLinesStrings = new List<string>();
            int numberInt = 0;

            foreach (string lineString in normalizedString.Split('\n').Append("# "))
            {
                Match match = Regex.Match(lineString, @"^#{1,6}\s+(.*)$");
                if (match.Success)
                {
                    // Overlap stays within a heading so a chunk never acquires another section's label.
                    string sectionString = string.Join("\n", sectionLinesStrings).Trim();
                    foreach (string textString in SplitSection(sectionString, maximumCharactersInt, overlapInt))
                    {
                        documentChunks.Add(new(ChunkIdString: $"{sourceDocument.FilenameString}#chunk-{++numberInt:00}",
                            FilenameString: sourceDocument.FilenameString, HeadingString: headingString, TextString: textString,
                            DocumentHashString: hashString));
                    }

                    sectionLinesStrings.Clear();
                    headingString = match.Groups[1].Value.Trim();
                }
                else
                {
                    sectionLinesStrings.Add(lineString);
                }
            }
        }

        if (documentChunks.Count == 0 || documentChunks.Count > 500)
        {
            throw new InvalidDataException("Documents must produce 1-500 nonempty chunks before embedding.");
        }

        return documentChunks;
    }

    // Prefer a paragraph end, then a word boundary; a hard cut also handles a single oversized word.
    private static List<string> SplitSection(string textString, int maximumCharactersInt, int overlapInt)
    {
        var partsStrings = new List<string>();
        int startInt = 0;
        while (startInt < textString.Length)
        {
            int endInt = Math.Min(startInt + maximumCharactersInt, textString.Length);
            if (endInt < textString.Length)
            {
                int paragraphInt = textString.LastIndexOf("\n\n", endInt - 1, endInt - startInt, StringComparison.Ordinal);
                int spaceInt = textString.LastIndexOf(' ', endInt - 1, endInt - startInt);
                if (paragraphInt > startInt + overlapInt)
                {
                    endInt = paragraphInt;
                }
                else if (spaceInt > startInt + overlapInt)
                {
                    endInt = spaceInt;
                }
            }

            string partString = textString[startInt..endInt].Trim();
            if (partString.Length > 0)
            {
                partsStrings.Add(partString);
            }

            if (endInt == textString.Length)
            {
                break;
            }

            startInt = Math.Max(startInt + 1, endInt - overlapInt);
        }

        return partsStrings;
    }
}
