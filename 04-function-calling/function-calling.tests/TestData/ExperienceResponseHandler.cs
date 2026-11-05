using System.Net;
using System.Text;

internal sealed class ExperienceResponseHandler(params string[] responseStrings) : HttpMessageHandler
{
    public List<string> RequestBodyStrings { get; } = [];
    public Action? BeforeResponseAction
    {
        get; init;
    }

    // Unexpected extra requests fail locally instead of concealing an unbounded agent loop.
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage requestHttpRequestMessage,
        CancellationToken cancellationToken)
    {
        RequestBodyStrings.Add(await requestHttpRequestMessage.Content!.ReadAsStringAsync(cancellationToken));
        BeforeResponseAction?.Invoke();
        cancellationToken.ThrowIfCancellationRequested();
        if (RequestBodyStrings.Count > responseStrings.Length)
        {
            throw new InvalidOperationException("The test did not provide another model response.");
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

