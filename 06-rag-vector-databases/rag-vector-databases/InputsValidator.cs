namespace ResumeRag;

public static class InputsValidator
{
    // Collect independent input errors before any document or question is sent to the provider.
    public static void Validate(List<SourceDocument> sourceDocuments, List<RagQuestion> ragQuestions,
        int chunkSizeInt, int overlapInt, int topKInt, double minimumScoreDouble, int contextLimitInt)
    {
        var errorStrings = new List<string>();

        ValidateDocuments(sourceDocuments, errorStrings);
        ValidateQuestions(ragQuestions, errorStrings);
        ValidateChunkSize(chunkSizeInt, errorStrings);
        ValidateOverlap(overlapInt, chunkSizeInt, errorStrings);
        ValidateTopK(topKInt, errorStrings);
        ValidateMinimumScore(minimumScoreDouble, errorStrings);
        ValidateContextLimit(contextLimitInt, chunkSizeInt, errorStrings);

        if (errorStrings.Count > 0)
        {
            throw new InvalidDataException(string.Join(Environment.NewLine, errorStrings));
        }
    }

    // Keep the source collection small and require distinct, readable Markdown documents.
    private static void ValidateDocuments(List<SourceDocument> sourceDocuments, List<string> errorStrings)
    {
        if (sourceDocuments.Count is < 1 or > 20)
        {
            errorStrings.Add("Documents: provide 1-20 Markdown files.");
        }

        var filenameStringsHashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (SourceDocument sourceDocument in sourceDocuments)
        {
            if (!filenameStringsHashSet.Add(sourceDocument.FilenameString) || !System.Text.RegularExpressions.Regex.IsMatch(
                sourceDocument.FilenameString, @"^[a-z0-9][a-z0-9-]*\.md$"))
            {
                errorStrings.Add($"Documents[{sourceDocument.FilenameString}]: use unique lowercase filenames such as project-a.md.");
            }

            if (string.IsNullOrWhiteSpace(sourceDocument.TextString) || sourceDocument.TextString.Length > 20_000)
            {
                errorStrings.Add($"Documents[{sourceDocument.FilenameString}]: text must contain 1-20000 characters.");
            }

            if (!sourceDocument.TextString.StartsWith("# ", StringComparison.Ordinal))
            {
                errorStrings.Add($"Documents[{sourceDocument.FilenameString}]: start with a Markdown heading.");
            }
        }
    }

    // Each question must name its skill so the later evidence check knows what to look for.
    private static void ValidateQuestions(List<RagQuestion> ragQuestions, List<string> errorStrings)
    {
        if (ragQuestions.Count is < 1 or > 3)
        {
            errorStrings.Add("Questions: provide 1-3 skill questions.");
        }

        foreach (RagQuestion ragQuestion in ragQuestions)
        {
            if (string.IsNullOrWhiteSpace(ragQuestion.SkillString) || ragQuestion.SkillString.Length > 60 ||
                ragQuestion.TextString != $"Where did I use {ragQuestion.SkillString}?")
            {
                errorStrings.Add("Questions: use 'Where did I use {skill}?' and a skill name of 1-60 characters.");
            }
        }
    }

    // Bound passage sizes before splitting the source documents.
    private static void ValidateChunkSize(int chunkSizeInt, List<string> errorStrings)
    {
        if (chunkSizeInt is < 100 or > 2_000)
        {
            errorStrings.Add("ChunkSize: use 100-2000 characters.");
        }
    }

    // Overlap must leave room for new text in each passage.
    private static void ValidateOverlap(int overlapInt, int chunkSizeInt, List<string> errorStrings)
    {
        if (overlapInt < 0 || overlapInt >= chunkSizeInt)
        {
            errorStrings.Add("Overlap: use 0 through ChunkSize minus 1.");
        }
    }

    // Limit how many candidate passages retrieval can return for each question.
    private static void ValidateTopK(int topKInt, List<string> errorStrings)
    {
        if (topKInt is < 1 or > 10)
        {
            errorStrings.Add("TopK: use 1-10 passages.");
        }
    }

    // Cosine scores are finite numbers between -1 and 1, so the threshold must be too.
    private static void ValidateMinimumScore(double minimumScoreDouble, List<string> errorStrings)
    {
        if (!double.IsFinite(minimumScoreDouble) || minimumScoreDouble is < -1 or > 1)
        {
            errorStrings.Add("MinimumScore: use a finite cosine score between -1 and 1.");
        }
    }

    // Leave room for a full passage and its metadata while bounding the total context.
    private static void ValidateContextLimit(int contextLimitInt, int chunkSizeInt, List<string> errorStrings)
    {
        if (contextLimitInt < chunkSizeInt + 300 || contextLimitInt > 20_000)
        {
            errorStrings.Add("ContextLimit: allow ChunkSize plus 300 through 20000 characters.");
        }
    }
}
