using System.Net;
using System.Text;
using System.Text.Json;

namespace ResumeRag.Tests.TestData;

public sealed class ChatResponseHandler(string answerString, string finishReasonString = "stop") : HttpMessageHandler
{
    public List<string> RequestBodyStrings { get; } = [];

    // Replace HTTP only; production SDK serialization, agent execution, and lesson checks still run.
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage httpRequestMessage, CancellationToken cancellationToken)
    {
        RequestBodyStrings.Add(await httpRequestMessage.Content!.ReadAsStringAsync(cancellationToken));
        string jsonString = JsonSerializer.Serialize(new
        {
            id = "test-response",
            @object = "chat.completion",
            created = 0,
            model = "test-chat",
            choices = new[] { new { index = 0, message = new { role = "assistant", content = answerString }, finish_reason = finishReasonString } },
            usage = new
            {
                prompt_tokens = 30,
                completion_tokens = 12,
                total_tokens = 42
            }
        });
        return new(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonString, Encoding.UTF8, "application/json")
        };
    }
}
