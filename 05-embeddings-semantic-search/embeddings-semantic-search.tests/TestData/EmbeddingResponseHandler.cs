using System.Net;
using System.Text;

// Replace HTTP only; the production SDK still serializes requests and reads responses.
public sealed class EmbeddingResponseHandler(
    string responseString,
    Action? beforeResponseAction = null) : HttpMessageHandler
{
    public List<string> RequestBodyStrings { get; } = [];
    public bool HadCancelableTokenBool
    {
        get; private set;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage requestHttpRequestMessage,
        CancellationToken cancellationToken)
    {
        RequestBodyStrings.Add(await requestHttpRequestMessage.Content!.ReadAsStringAsync(cancellationToken));
        HadCancelableTokenBool = cancellationToken.CanBeCanceled;
        beforeResponseAction?.Invoke();
        cancellationToken.ThrowIfCancellationRequested();
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                responseString,
                Encoding.UTF8,
                "application/json")
        };
    }
}

