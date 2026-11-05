using System.Net;
using System.Text;
using System.Text.Json;

public sealed class ContextResponseHandler(
    string answerString,
    string finishReasonString = "stop",
    Action? beforeResponseAction = null) : HttpMessageHandler
{
    public List<string> RequestBodyStrings { get; } = [];

    // The actual agent serializes the prepared context; only its remote response is scripted.
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage httpRequestMessage,
        CancellationToken cancellationToken)
    {
        RequestBodyStrings.Add(await httpRequestMessage.Content!.ReadAsStringAsync(cancellationToken));
        beforeResponseAction?.Invoke();
        cancellationToken.ThrowIfCancellationRequested();
        string responseString = JsonSerializer.Serialize(new
        {
            id = "context-test",
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
                        content = answerString
                    },
                    finish_reason = finishReasonString
                }
            }
        });
        return new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                responseString,
                Encoding.UTF8,
                "application/json")
        };
    }
}
