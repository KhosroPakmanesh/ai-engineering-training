
public static class InputValidator
{
    // Find missing or empty inputs before starting the paid comparison.
    public static async Task ValidateInputsAsync(IEnumerable<string> promptFiles, CancellationToken cancellationToken = default)
    {
        string folder = AppContext.BaseDirectory;
        string resume = await File.ReadAllTextAsync(Path.Combine(folder, "resume.md"), cancellationToken);
        foreach (string promptFile in promptFiles)
        {
            string instruction = await File.ReadAllTextAsync(Path.Combine(folder, promptFile), cancellationToken);
            CheckInputs(resume, instruction);
        }
    }
    // Empty source text cannot demonstrate how prompt instructions affect a summary.
    private static void CheckInputs(string resume, string instruction)
    {
        if (string.IsNullOrWhiteSpace(resume)) throw new InvalidDataException("The resume is empty. No requests were made.");
        if (string.IsNullOrWhiteSpace(instruction)) throw new InvalidDataException("The prompt instruction is empty. No requests were made.");
    }
}
