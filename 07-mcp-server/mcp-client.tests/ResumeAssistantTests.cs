using System.Text.Json;
using Microsoft.Agents.AI;
using ModelContextProtocol.Client;
using Xunit;

namespace ResumeMcpClient.Tests;

public sealed class ResumeAssistantTests
{
    // The real agent keeps discovered schemas and obtains the known skills through the separate server.
    [Fact]
    public async Task Agent_calls_real_mcp_and_uses_its_skills_in_the_final_answer()
    {
        // Arrange
        await using McpTestSession mcpTestSession = await McpTestSession.StartAsync();
        (IList<McpClientTool> mcpClientTools,
            ResumeDraft resumeDraft) = await McpTestData.PrepareAsync(mcpTestSession.McpClient);
        using var mcpResponseHandler = new McpResponseHandler(
            McpTestData.CreateResponse(
                "I will look up the skills.",
                "list_skills"),
            McpTestData.CreateResponse(McpTestData.AnswerString));
        using var httpClient = new HttpClient(mcpResponseHandler);

        // Act
        AgentResponse agentResponse = await ResumeAssistant.RequestAsync(
            McpTestData.QuestionString,
            "alex",
            mcpClientTools,
            McpTestData.CreateSettings(),
            httpClient);
        SkillAnswer skillAnswer = SkillAnswerParser.Parse(agentResponse);
        SkillAnswerValidator.Validate(
            agentResponse,
            skillAnswer,
            resumeDraft.SkillStrings);

        // Assert
        Assert.Equal(
            McpTestData.AnswerString,
            skillAnswer.AnswerString);
        Assert.Equal(
            2,
            skillAnswer.ModelCallCountInt);
        Assert.Equal(
            1,
            skillAnswer.ToolExecutionCountInt);
        Assert.Equal(
            2,
            mcpResponseHandler.RequestBodyStrings.Count);
        using JsonDocument firstJsonDocument = JsonDocument.Parse(mcpResponseHandler.RequestBodyStrings[0]);
        JsonElement rootJsonElement = firstJsonDocument.RootElement;
        Assert.Equal(
            3,
            rootJsonElement.GetProperty("tools").GetArrayLength());
        string? promptString = rootJsonElement.GetProperty("messages")[1].GetProperty("content").GetString();
        Assert.Contains(
            McpTestData.QuestionString,
            promptString);
        Assert.DoesNotContain(
            "PostgreSQL",
            promptString);
        using JsonDocument secondJsonDocument = JsonDocument.Parse(mcpResponseHandler.RequestBodyStrings[1]);
        JsonElement toolJsonElement = secondJsonDocument.RootElement.GetProperty("messages").EnumerateArray()
            .Single(messageJsonElement => messageJsonElement.GetProperty("role").GetString() == "tool");
        Assert.Contains(
            "PostgreSQL",
            toolJsonElement.GetProperty("content").GetString());
        Assert.Equal(
            40,
            agentResponse.Usage?.InputTokenCount);
    }

    // Tool visibility cannot override client policy, even when the discovered server offers save_resume.
    [Theory]
    [InlineData(
        "save_resume",
        "alex",
        false)]
    [InlineData(
        "list_skills",
        "sam",
        false)]
    [InlineData(
        "list_skills",
        "alex",
        true)]
    public async Task Changed_scope_or_repeated_execution_stops_before_a_follow_up_request(
        string toolNameString,
        string profileIdString,
        bool duplicateCallBool)
    {
        // Arrange
        await using McpTestSession mcpTestSession = await McpTestSession.StartAsync();
        (IList<McpClientTool> mcpClientTools, _) = await McpTestData.PrepareAsync(mcpTestSession.McpClient);
        using var mcpResponseHandler = new McpResponseHandler(McpTestData.CreateResponse(
            null,
            toolNameString,
            profileIdString,
            duplicateCallBool: duplicateCallBool));
        using var httpClient = new HttpClient(mcpResponseHandler);

        // Act
        InvalidDataException invalidDataException = await Assert.ThrowsAsync<InvalidDataException>(() =>
            ResumeAssistant.RequestAsync(
                McpTestData.QuestionString,
                "alex",
                mcpClientTools,
                McpTestData.CreateSettings(),
                httpClient));

        // Assert
        Assert.Contains(
            duplicateCallBool ? "at most one" : "original profile",
            invalidDataException.Message);
        Assert.Single(mcpResponseHandler.RequestBodyStrings);
        Assert.Empty(Directory.GetFileSystemEntries(mcpTestSession.DirectoryString));
    }

    // The answer can be correct by coincidence; without a tool execution it must fail acceptance.
    [Fact]
    public async Task Guessed_skills_without_tool_use_are_rejected()
    {
        // Arrange
        await using McpTestSession mcpTestSession = await McpTestSession.StartAsync();
        (IList<McpClientTool> mcpClientTools,
            ResumeDraft resumeDraft) = await McpTestData.PrepareAsync(mcpTestSession.McpClient);
        using var mcpResponseHandler = new McpResponseHandler(McpTestData.CreateResponse(McpTestData.AnswerString));
        using var httpClient = new HttpClient(mcpResponseHandler);

        // Act
        AgentResponse agentResponse = await ResumeAssistant.RequestAsync(
            McpTestData.QuestionString,
            "alex",
            mcpClientTools,
            McpTestData.CreateSettings(),
            httpClient);
        SkillAnswer skillAnswer = SkillAnswerParser.Parse(agentResponse);

        // Assert
        Assert.Throws<InvalidDataException>(() => SkillAnswerValidator.Validate(
            agentResponse,
            skillAnswer,
            resumeDraft.SkillStrings));
    }

    // An assistant preamble cannot hide an empty or cut-short final answer.
    [Theory]
    [InlineData(
        null,
        "stop",
        "no final response")]
    [InlineData(
        McpTestData.AnswerString,
        "length",
        "truncated")]
    public async Task Invalid_final_text_is_rejected(
        string? answerString,
        string finishReasonString,
        string errorString)
    {
        // Arrange
        await using McpTestSession mcpTestSession = await McpTestSession.StartAsync();
        (IList<McpClientTool> mcpClientTools, _) = await McpTestData.PrepareAsync(mcpTestSession.McpClient);
        using var mcpResponseHandler = new McpResponseHandler(
            McpTestData.CreateResponse(
                "Looking up skills.",
                "list_skills"),
            McpTestData.CreateResponse(
                answerString,
                finishReasonString: finishReasonString));
        using var httpClient = new HttpClient(mcpResponseHandler);

        // Act
        InvalidDataException invalidDataException = await Assert.ThrowsAsync<InvalidDataException>(() =>
            ResumeAssistant.RequestAsync(
                McpTestData.QuestionString,
                "alex",
                mcpClientTools,
                McpTestData.CreateSettings(),
                httpClient));

        // Assert
        Assert.Contains(
            errorString,
            invalidDataException.Message);
        Assert.Equal(
            2,
            mcpResponseHandler.RequestBodyStrings.Count);
    }

    // Cancellation reaches the native HTTP boundary and ends the whole conversation.
    [Fact]
    public async Task Cancellation_stops_the_first_model_request()
    {
        // Arrange
        await using McpTestSession mcpTestSession = await McpTestSession.StartAsync();
        (IList<McpClientTool> mcpClientTools, _) = await McpTestData.PrepareAsync(mcpTestSession.McpClient);
        using var cancellationTokenSource = new CancellationTokenSource();
        using var mcpResponseHandler = new McpResponseHandler(McpTestData.CreateResponse(
            null,
            "list_skills"))
        {
            BeforeResponseAction = cancellationTokenSource.Cancel
        };
        using var httpClient = new HttpClient(mcpResponseHandler);

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ResumeAssistant.RequestAsync(
                McpTestData.QuestionString,
                "alex",
                mcpClientTools,
                McpTestData.CreateSettings(),
                httpClient,
                cancellationTokenSource.Token));

        // Assert
        Assert.Single(mcpResponseHandler.RequestBodyStrings);
    }

    // This provider smoke test follows all application preparation and acceptance steps but never saves a file.
    [Fact]
    [Trait(
        "Category",
        "Integration")]
    public async Task Selected_provider_answers_using_the_local_mcp_server()
    {
        // Arrange
        await using McpTestSession mcpTestSession = await McpTestSession.StartAsync();
        (IList<McpClientTool> mcpClientTools,
            ResumeDraft resumeDraft) = await McpTestData.PrepareAsync(mcpTestSession.McpClient);
        ProviderSettings providerSettings = await SettingsReader.ReadSettingsAsync(AppContext.BaseDirectory);

        // Act
        AgentResponse agentResponse = await ResumeAssistant.RequestAsync(
            McpTestData.QuestionString,
            "alex",
            mcpClientTools,
            providerSettings);
        SkillAnswer skillAnswer = SkillAnswerParser.Parse(agentResponse);
        SkillAnswerValidator.Validate(
            agentResponse,
            skillAnswer,
            resumeDraft.SkillStrings);

        // Assert
        Assert.Equal(
            McpTestData.AnswerString,
            skillAnswer.AnswerString);
    }
}
