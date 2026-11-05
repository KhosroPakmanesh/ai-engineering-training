using System.Diagnostics;
using System.Text.Json;
using Microsoft.Agents.AI;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ResumeMcpClient;

const string profileIdString = "alex";
const string questionString = "List my skills from the selected fictional profile.";

try
{
    //------------------------------------------------------------------------//
    //                     PREPARATION AND MCP DISCOVERY                      //
    //------------------------------------------------------------------------//

    // 1. Start a read-only process. These local stages need no AI key or model.
    string folderString = AppContext.BaseDirectory;
    string versionsDirectoryString = Path.Combine(
        folderString,
        "data",
        "versions");
    using var preparationCancellationTokenSource =
        new CancellationTokenSource(TimeSpan.FromSeconds(AiClientFactory.RequestTimeoutSeconds));
    await using McpClient readOnlyMcpClient = await McpConnection.ConnectAsync(
        folderString,
        allowWriteBool: false,
        versionsDirectoryString,
        preparationCancellationTokenSource.Token);
    IList<McpClientTool> mcpClientTools =
        await readOnlyMcpClient.ListToolsAsync(cancellationToken: preparationCancellationTokenSource.Token);
    OutputPrinter.PrintDiscovery(mcpClientTools);

    // 2. Read the selected source and prepare the exact Markdown before paid work.
    CallToolResult profileCallToolResult = await readOnlyMcpClient.CallToolAsync(
        "get_profile",
        new Dictionary<string, object?>
        {
            ["profileIdString"] = profileIdString
        },
        cancellationToken: preparationCancellationTokenSource.Token);
    ResumeDraft resumeDraft = ResumePreparer.Prepare(
        ToolResultParser.Parse(profileCallToolResult),
        profileIdString);

    // 3. Invoke the skills tool directly, then inspect the same source through an independent agent conversation.
    CallToolResult skillsCallToolResult = await readOnlyMcpClient.CallToolAsync(
        "list_skills",
        new Dictionary<string, object?>
        {
            ["profileIdString"] = profileIdString
        },
        cancellationToken: preparationCancellationTokenSource.Token);
    List<string> directSkillStrings = ToolResultParser.Parse(skillsCallToolResult).Deserialize<List<string>>()
        ?? throw new InvalidDataException("The skills tool returned no skill list.");
    InputsValidator.Validate(
        questionString,
        mcpClientTools,
        resumeDraft,
        directSkillStrings);
    OutputPrinter.PrintDirectResult(
        profileIdString,
        directSkillStrings);
    ProviderSettings providerSettings = await SettingsReader.ReadSettingsAsync(folderString);
    OutputPrinter.PrintIntroduction(
        questionString,
        providerSettings);

    //------------------------------------------------------------------------//
    //                      AGENT CONVERSATION AND CHECKS                      //
    //------------------------------------------------------------------------//

    // 1. The prompt includes the question and profile ID; the skills must come from MCP.
    var requestStopwatch = Stopwatch.StartNew();
    AgentResponse agentResponse = await ResumeAssistant.RequestAsync(
        questionString,
        profileIdString,
        mcpClientTools,
        providerSettings);
    requestStopwatch.Stop();

    // 2. Parse the final answer and prove that the real skills tool ran before accepting it.
    SkillAnswer skillAnswer = SkillAnswerParser.Parse(agentResponse);
    SkillAnswerValidator.Validate(
        agentResponse,
        skillAnswer,
        resumeDraft.SkillStrings);
    OutputPrinter.PrintResults(
        skillAnswer,
        agentResponse.Usage,
        requestStopwatch.ElapsedMilliseconds);

    //------------------------------------------------------------------------//
    //                       CONFIRMED SAVE AND TAKEAWAY                      //
    //------------------------------------------------------------------------//

    // 1. Each run offers a new version. Only an exact SAVE confirmation permits a writable process.
    string versionString = "v" + DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff");
    string? savedPathString = await ResumeSaver.SaveAsync(
        folderString,
        versionsDirectoryString,
        resumeDraft,
        versionString,
        Console.In);

    // 2. Report the real outcome, including a declined or unavailable console response.
    OutputPrinter.PrintSaveResult(savedPathString);
    OutputPrinter.PrintTakeaway();
    return 0;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("The operation was canceled or exceeded its 120-second time limit.");
    return 1;
}
catch (Exception errorException)
{
    Console.Error.WriteLine(errorException.Message);
    return 1;
}
