namespace ResumeRag;

public static class DocumentsLoader
{
    // Read every source before remote work so a missing document cannot waste earlier requests.
    public static async Task<List<SourceDocument>> LoadAsync(
        string folderString,
        CancellationToken cancellationToken = default)
    {
        var sourceDocuments = new List<SourceDocument>();
        foreach (string pathString in Directory.EnumerateFiles(
            folderString,
            "*.md").Order(StringComparer.Ordinal))
        {
            sourceDocuments.Add(new(
                Path.GetFileName(pathString),
                await File.ReadAllTextAsync(
                    pathString,
                    cancellationToken)));
        }

        return sourceDocuments;
    }
}
