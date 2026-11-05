namespace ResumeMcpServer;

public sealed record ServerSettings(
    bool AllowWriteBool,
    string VersionsDirectoryString)
{
    // Permission comes from the process owner, never from a tool's arguments.
    public static ServerSettings Read()
    {
        string? permissionString = Environment.GetEnvironmentVariable("RESUME_MCP_ALLOW_WRITE");
        if (permissionString is not null and not "true" and not "false")
        {
            throw new InvalidDataException("RESUME_MCP_ALLOW_WRITE must be true or false.");
        }

        string directoryString = Environment.GetEnvironmentVariable("RESUME_MCP_VERSIONS_DIRECTORY")
            ?? Path.Combine(
                AppContext.BaseDirectory,
                "data",
                "versions");
        if (!Path.IsPathFullyQualified(directoryString))
        {
            throw new InvalidDataException("RESUME_MCP_VERSIONS_DIRECTORY must be an absolute path.");
        }

        return new(
            permissionString == "true",
            Path.GetFullPath(directoryString));
    }
}
