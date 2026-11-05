namespace ResumeRag;

public static class CitationValidator
{
    // Only passages actually included in the prompt may support citations, even if other candidates were retrieved.
    public static void Validate(
        string answerString,
        List<string> citedIdStrings,
        List<RetrievedChunk> selectedRetrievedChunks)
    {
        if (answerString == RagAnswerer.InsufficientEvidenceString)
        {
            if (citedIdStrings.Count > 0)
            {
                throw new InvalidDataException("An insufficient-evidence answer must not cite sources.");
            }

            return;
        }

        if (citedIdStrings.Count == 0)
        {
            throw new InvalidDataException("The answer must cite at least one supplied passage.");
        }

        foreach (string idString in citedIdStrings)
        {
            if (!selectedRetrievedChunks.Any(selectedRetrievedChunk =>
                selectedRetrievedChunk.DocumentChunk.ChunkIdString == idString))
            {
                throw new InvalidDataException($"Unknown or excluded citation: [{idString}].");
            }
        }
    }
}
