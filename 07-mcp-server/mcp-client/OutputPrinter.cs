using System.Text.Json;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using static ConsoleOutput;

namespace ResumeMcpClient;

public static class OutputPrinter
{
    // Show what the separate process actually advertised, including its argument schemas.
    public static void PrintDiscovery(IEnumerable<McpClientTool> mcpClientTools)
    {
        PrintHeading("MCP discovery: a separate local resume server");
        Console.WriteLine(
            "The server owns fictional profiles. STDIO carries JSON-RPC messages between the two processes.");
        Console.WriteLine("Discovery and direct tool calls below make zero model requests and need no AI credentials.");
        int toolNumberInt = 1;
        foreach (McpClientTool mcpClientTool in mcpClientTools)
        {
            PrintColoredLine(
                $"\n── Tool {toolNumberInt++} ──────────────────────────",
                ConsoleColor.White);
            PrintColoredLine(
                $"SOURCE | {mcpClientTool.Name}",
                ConsoleColor.Cyan);
            Console.WriteLine($"DESCRIPTION | {mcpClientTool.Description}");
            Console.WriteLine($"SCHEMA | {JsonSerializer.Serialize(mcpClientTool.JsonSchema)}");
        }
    }

    // Keep direct invocation visible before reading settings for the remote model.
    public static void PrintDirectResult(
        string profileIdString,
        List<string> skillStrings)
    {
        PrintHeading("\nDirect MCP invocation");
        PrintColoredLine(
            $"SOURCE | get_profile, list_skills | profile={profileIdString}",
            ConsoleColor.Cyan);
        Console.WriteLine("SKILLS | " + string.Join(
            ", ",
            skillStrings));
        Console.WriteLine("STATUS | Local tool result received without an LLM.");
    }

    // Explain costs before the first remote request; MCP is local but its results enter model context.
    public static void PrintIntroduction(
        string questionString,
        ProviderSettings providerSettings)
    {
        PrintHeading("\nAgent invocation of the same MCP tool");
        PrintColoredLine(
            $"QUESTION | {questionString}",
            ConsoleColor.Cyan);
        PrintColoredLine(
            $"Provider: {providerSettings.NameString}\nModel: {providerSettings.ModelString}",
            ConsoleColor.Gray);
        PrintColoredLine(
            $"Plan: one agent run, exactly {ResumeAssistant.MaxModelCallsInt} model calls and one list_skills execution on success.\n" +
            $"HTTP retries: up to {AiClientFactory.MaxRetries} per model call; " +
            $"at most {ResumeAssistant.MaxModelCallsInt * (AiClientFactory.MaxRetries + 1)} HTTP attempts.\n" +
            $"One {AiClientFactory.RequestTimeoutSeconds}-second deadline covers both turns, MCP execution, and retries.",
            ConsoleColor.Gray);
        Console.WriteLine(
            "The question, tool schemas, and returned fictional skills go to the selected remote provider and may incur charges.");
        Console.WriteLine(
            "The agent sees all three discovered tools; client policy permits only list_skills in this conversation.");
    }

    // Inputs and source text do not belong in routine execution logs.
    public static void PrintToolExecution(
        string nameString,
        long durationMillisecondsLong,
        bool succeededBool)
    {
        PrintColoredLine(
            $"tool_name={nameString} inputs=[redacted] duration_ms={durationMillisecondsLong} succeeded={succeededBool}",
            ConsoleColor.Gray);
    }

    // Keep model text in the terminal's default colour and print a separate accepted reference.
    public static void PrintResults(
        SkillAnswer skillAnswer,
        UsageDetails? usageDetails,
        long durationMillisecondsLong)
    {
        PrintHeading("\nAccepted agent answer");
        PrintColoredLine(
            "── Answer 1 ──────────────────────────",
            ConsoleColor.White);
        Console.WriteLine(skillAnswer.AnswerString);
        Console.WriteLine("STATUS | Real MCP execution and exact source skills checked.");
        PrintColoredLine(
            "\nAccepted citations:\n1. [list_skills] -> the selected fictional profile's skill list.",
            ConsoleColor.Cyan);
        PrintColoredLine(
            $"Model calls: {skillAnswer.ModelCallCountInt}; MCP tool executions: {skillAnswer.ToolExecutionCountInt}\n" +
            $"Request time: {durationMillisecondsLong} ms\n" +
            $"Tokens: input={usageDetails?.InputTokenCount?.ToString() ?? "unavailable"}; " +
            $"output={usageDetails?.OutputTokenCount?.ToString() ?? "unavailable"}",
            ConsoleColor.Gray);
    }

    // The user approves a concrete document and destination, never an open-ended model write.
    public static void PrintSavePreview(
        ResumeDraft resumeDraft,
        string versionString,
        string versionsDirectoryString)
    {
        PrintHeading("\nSave this fictional resume?");
        Console.WriteLine(resumeDraft.MarkdownString);
        PrintColoredLine(
            $"Destination: {Path.Combine(versionsDirectoryString, versionString + ".md")}",
            ConsoleColor.Gray);
        Console.WriteLine("This Markdown copies profile facts directly; saving makes no additional model calls.");
        PrintColoredLine(
            "Type SAVE and press Enter to authorize this one version. Any other response or end of input skips saving.",
            ConsoleColor.DarkYellow);
    }

    // A skipped save is a normal result, not evidence that a file was created.
    public static void PrintSaveResult(string? savedPathString)
    {
        if (savedPathString is null)
        {
            PrintColoredLine(
                "STATUS | Save skipped. No writable server was started.",
                ConsoleColor.DarkYellow);
        }
        else
        {
            PrintColoredLine(
                $"STATUS | Saved through save_resume.\nSaved path: {savedPathString}",
                ConsoleColor.Gray);
        }
    }

    // STDERR remains distinct from protocol output; the client may display these local diagnostics.
    public static void PrintServerLog(string errorString)
    {
        PrintColoredLine(
            $"SERVER STDERR | {errorString}",
            ConsoleColor.Gray);
    }

    // Separate protocol checks from judgments that only the learner can make.
    public static void PrintTakeaway()
    {
        PrintHeading("\nWhat to take away");
        Console.WriteLine(
            "An in-process function calls C# inside the agent application. MCP discovers a schema and calls a separate process.");
        Console.WriteLine(
            "Discovering save_resume grants no write permission. The client requires confirmation; the server checks its launch settings.");
        Console.WriteLine(
            "The checks prove tool execution and copying of fictional skills. They do not verify a person's qualifications or arbitrary prose.");
        Console.WriteLine(
            "Change profileIdString in Program.cs from alex to sam, rebuild, and inspect the skills returned from the other profile.");
    }
}
