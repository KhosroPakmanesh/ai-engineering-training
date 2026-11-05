using System.Net;
using System.Text;
using System.Text.Json;

public sealed class SummaryResponseHandler(
    string answerString = "A complete summary.",
    string finishReasonString = "stop",
    Action? beforeResponseAction = null) : HttpMessageHandler
{
    public List<string> RequestBodyStrings { get; } = [];

    // Replace only HTTP so the real agent still serializes each independent conversation.
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage httpRequestMessage,
        CancellationToken cancellationToken)
    {
        RequestBodyStrings.Add(await httpRequestMessage.Content!.ReadAsStringAsync(cancellationToken));
        beforeResponseAction?.Invoke();
        cancellationToken.ThrowIfCancellationRequested();
        string responseString = JsonSerializer.Serialize(new
        {
            id = "summary-test",
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
