using System.Net;
using System.Text;

namespace ResumeMcpClient.Tests;

internal sealed class McpResponseHandler(params string[] responseStrings) : HttpMessageHandler
{
    public List<string> RequestBodyStrings { get; } = [];
    public Action? BeforeResponseAction
    {
        get; init;
    }

    // An unexpected third model call fails instead of receiving another convenient fake answer.
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage requestHttpRequestMessage,
        CancellationToken cancellationToken)
    {
        RequestBodyStrings.Add(await requestHttpRequestMessage.Content!.ReadAsStringAsync(cancellationToken));
        BeforeResponseAction?.Invoke();
        cancellationToken.ThrowIfCancellationRequested();
        if (RequestBodyStrings.Count > responseStrings.Length)
        {
            throw new InvalidOperationException("No scripted response remains; the model-call budget was exceeded.");
        }

        return new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                responseStrings[RequestBodyStrings.Count - 1],
                Encoding.UTF8,
                "application/json")
        };
    }
}

