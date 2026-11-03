
public static class InputValidator
{
    // Check the complete comparison before any prompt can reach a paid request.
    public static void Validate(ComparisonInputs comparisonInputs)
    {
        if (string.IsNullOrWhiteSpace(comparisonInputs.ResumeString))
        {
            throw new InvalidDataException("The resume is empty. No requests were made.");
        }

        if (comparisonInputs.PromptCases.Count == 0)
        {
            throw new InvalidDataException("Supply at least one prompt case. No requests were made.");
        }

        foreach (PromptCase promptCase in comparisonInputs.PromptCases)
        {
            if (string.IsNullOrWhiteSpace(promptCase.InstructionString))
            {
                throw new InvalidDataException("The prompt instruction is empty. No requests were made.");
            }
        }
    }
}
