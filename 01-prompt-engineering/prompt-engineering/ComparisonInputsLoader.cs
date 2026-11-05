public static class ComparisonInputsLoader
{
    // Read each copied file once so every case uses the same source snapshot.
    public static async Task<ComparisonInputs> LoadAsync(
        List<PromptFile> promptFiles,
        CancellationToken cancellationToken = default)
    {
        string folderString = AppContext.BaseDirectory;
        string resumeString = await File.ReadAllTextAsync(
            Path.Combine(
                folderString,
                "resume.md"),
            cancellationToken);

        var promptCases = new List<PromptCase>();
        foreach (PromptFile promptFile in promptFiles)
        {
            string instructionString = await File.ReadAllTextAsync(
                Path.Combine(
                    folderString,
                    promptFile.PathString),
                cancellationToken);
            promptCases.Add(new(
                promptFile.NameString,
                promptFile.PathString,
                instructionString));
        }

        return new(
            resumeString,
            promptCases);
    }
}
