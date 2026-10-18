
public static class InputValidator
{
    // Find missing or empty input files before making any paid model calls.
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
    // A summary needs both resume text and an instruction.
    private static void CheckInputs(string resume, string instruction)
    {
        if (string.IsNullOrWhiteSpace(resume)) throw new InvalidDataException("The resume is empty. No requests were made.");
        if (string.IsNullOrWhiteSpace(instruction)) throw new InvalidDataException("The prompt instruction is empty. No requests were made.");
    }
}
