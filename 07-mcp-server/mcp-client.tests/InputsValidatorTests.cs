using ModelContextProtocol.Client;
using Xunit;

namespace ResumeMcpClient.Tests;

public sealed class InputsValidatorTests
{
    // Invalid local inputs must stop the normal preparation sequence before any paid transport call.
    [Theory]
    [InlineData(
        "question",
        "question")]
    [InlineData(
        "tools",
        "expose")]
    [InlineData(
        "skills",
        "skills")]
    public async Task Invalid_inputs_prevent_model_requests(
        string invalidFieldString,
        string expectedErrorString)
    {
        // Arrange
        await using McpTestSession mcpTestSession = await McpTestSession.StartAsync();
        (IList<McpClientTool> mcpClientTools,
            ResumeDraft resumeDraft) = await McpTestData.PrepareAsync(mcpTestSession.McpClient);
        using var mcpResponseHandler = new McpResponseHandler();
        using var httpClient = new HttpClient(mcpResponseHandler);
        string questionString = invalidFieldString == "question" ? " " : McpTestData.QuestionString;
        IEnumerable<McpClientTool> selectedMcpClientTools =
            invalidFieldString == "tools" ? mcpClientTools.Take(1) : mcpClientTools;
        List<string> directSkillStrings =
            invalidFieldString == "skills" ? ["Invented skill"] : resumeDraft.SkillStrings;

        // Act
        InvalidDataException invalidDataException = await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            InputsValidator.Validate(
                questionString,
                selectedMcpClientTools,
                resumeDraft,
                directSkillStrings);
            await ResumeAssistant.RequestAsync(
                questionString,
                "alex",
                selectedMcpClientTools,
                McpTestData.CreateSettings(),
                httpClient);
        });

        // Assert
        Assert.Contains(
            expectedErrorString,
            invalidDataException.Message);
        Assert.Empty(mcpResponseHandler.RequestBodyStrings);
    }
}
