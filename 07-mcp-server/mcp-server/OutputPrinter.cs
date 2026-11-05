namespace ResumeMcpServer;

public static class OutputPrinter
{
    // Terminal headings belong to the client. This process must reserve STDOUT for MCP.
    public static void PrintIntroduction(bool allowWriteBool)
    {
        Console.Error.WriteLine($"Local resume MCP server | write permission: {allowWriteBool}");
    }
}
