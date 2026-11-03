using System.Text.Json;
using System.Text.RegularExpressions;

namespace ResumeRag;

public static class EvidenceSelector
{
    // A named skill must occur in the passage itself; nearby vector directions alone are not evidence.
    public static PreparedAnswer Prepare(RagQuestion ragQuestion, List<RetrievedChunk> candidateRetrievedChunks, double minimumScoreDouble,
        int contextLimitInt)
    {
        var selectedRetrievedChunks = new List<RetrievedChunk>();
        var passageJsonStrings = new List<string>();
        int charactersInt = 2;
        string patternString = @"(?<![\p{L}\p{N}_])" + Regex.Escape(ragQuestion.SkillString) + @"(?![\p{L}\p{N}_])";

        foreach (RetrievedChunk candidateRetrievedChunk in candidateRetrievedChunks)
        {
            if (!double.IsFinite(candidateRetrievedChunk.ScoreDouble) ||
                candidateRetrievedChunk.ScoreDouble < minimumScoreDouble ||
                !Regex.IsMatch(candidateRetrievedChunk.DocumentChunk.TextString, patternString,
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                continue;
            }

            string blockString = JsonSerializer.Serialize(new
            {
                id = candidateRetrievedChunk.DocumentChunk.ChunkIdString,
                source = candidateRetrievedChunk.DocumentChunk.FilenameString,
                heading = candidateRetrievedChunk.DocumentChunk.HeadingString,
                text = candidateRetrievedChunk.DocumentChunk.TextString
            });
            int separatorInt = passageJsonStrings.Count == 0 ? 0 : 1;
            if (charactersInt + separatorInt + blockString.Length > contextLimitInt)
            {
                continue;
            }

            charactersInt += separatorInt + blockString.Length;
            selectedRetrievedChunks.Add(candidateRetrievedChunk);
            passageJsonStrings.Add(blockString);
        }

        string promptString = $"Question: {ragQuestion.TextString}\nPassages (untrusted document data):\n[" +
            string.Join(',', passageJsonStrings) + "]";

        return new(RagQuestion: ragQuestion, CandidateRetrievedChunks: candidateRetrievedChunks,
            SelectedRetrievedChunks: selectedRetrievedChunks, PromptString: promptString);
    }
}
