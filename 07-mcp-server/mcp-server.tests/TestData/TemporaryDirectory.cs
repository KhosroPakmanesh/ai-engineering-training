namespace ResumeMcpServer.Tests;

internal sealed class TemporaryDirectory : IDisposable
{
    public string PathString
    {
        get;
    } = Path.Combine(
        Path.GetTempPath(),
        "resume-mcp-" + Guid.NewGuid().ToString("N"));

    // Every operation owns a different directory; no test changes process-wide settings.
    public TemporaryDirectory()
    {
        Directory.CreateDirectory(PathString);
    }

    // Only the directory created by this fixture is removed.
    public void Dispose()
    {
        Directory.Delete(
            PathString,
            recursive: true);
    }
}
