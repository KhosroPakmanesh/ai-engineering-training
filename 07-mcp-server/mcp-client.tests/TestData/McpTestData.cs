using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace ResumeMcpClient.Tests;

internal static class McpTestData
{
    public const string QuestionString = "List my skills from the selected fictional profile.";
    public const string AnswerString = "Skills: C#, .NET, PostgreSQL, Docker [list_skills]";

    // Match Program.cs: discover, read both source tools, prepare the draft, and validate everything.
    public static async Task<(IList<McpClientTool> McpClientTools,
        ResumeDraft ResumeDraft)> PrepareAsync(McpClient mcpClient)
    {
        IList<McpClientTool> mcpClientTools = await mcpClient.ListToolsAsync();
        CallToolResult profileCallToolResult = await mcpClient.CallToolAsync(
            "get_profile",
            new Dictionary<string, object?>
            {
                ["profileIdString"] = "alex"
            });
        ResumeDraft resumeDraft = ResumePreparer.Prepare(
            ToolResultParser.Parse(profileCallToolResult),
            "alex");
        CallToolResult skillsCallToolResult = await mcpClient.CallToolAsync(
            "list_skills",
            new Dictionary<string, object?>
            {
                ["profileIdString"] = "alex"
            });
        List<string> directSkillStrings = ToolResultParser.Parse(skillsCallToolResult).Deserialize<List<string>>()!;
        InputsValidator.Validate(
            QuestionString,
            mcpClientTools,
            resumeDraft,
            directSkillStrings);
        return (mcpClientTools, resumeDraft);
    }

    // Only provider HTTP is scripted; discovered functions still cross real process boundaries.
    public static string CreateResponse(
        string? answerString,
        string? toolNameString = null,
        string profileIdString = "alex",
        string finishReasonString = "stop",
        bool duplicateCallBool = false)
    {
        var callObjects = new List<object>();
        if (toolNameString is not null)
        {
            callObjects.Add(new
            {
                id = "mcp-call",
                type = "function",
                function = new
                {
                    name = toolNameString,
                    arguments = JsonSerializer.Serialize(new
                    {
                        profileIdString
                    })
                }
            });
            if (duplicateCallBool)
            {
                callObjects.Add(new
                {
                    id = "second-mcp-call",
                    type = "function",
                    function = new
                    {
                        name = toolNameString,
                        arguments = JsonSerializer.Serialize(new
                        {
                            profileIdString
                        })
                    }
                });
            }
        }

        return JsonSerializer.Serialize(new
        {
            id = "mcp-test",
            @object = "chat.completion",
            created = 0,
            model = "test-model",
            choices = new[]
            {
                new
                {
                    index = 0,
                    message = new
                    {
                        role = "assistant",
                        content = answerString,
                        tool_calls = toolNameString is null ? null : callObjects
                    },
                    finish_reason = toolNameString is null ? finishReasonString : "tool_calls"
                }
            },
            usage = new
            {
                prompt_tokens = 20,
                completion_tokens = 8,
                total_tokens = 28
            }
        });
    }

    // Credentials are arbitrary because example.invalid is handled entirely in memory.
    public static ProviderSettings CreateSettings()
    {
        return new(
            "OpenRouter",
            "test-key",
            "test-model",
            new Uri("https://example.invalid/v1"));
    }
}
