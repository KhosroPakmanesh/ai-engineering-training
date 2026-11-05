using System.Net;
using System.Text;

namespace ResumeRag.Tests.TestData;

public sealed class EmbeddingResponseHandler(
    string responseJsonString,
    bool holdResponseBool = false) : HttpMessageHandler
{
    private readonly TaskCompletionSource _requestStartedTaskCompletionSource =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public List<string> RequestBodyStrings { get; } = [];
    public bool HadCancelableTokenBool
    {
        get; private set;
    }
    public Task RequestStartedTask => _requestStartedTaskCompletionSource.Task;

    // Replace HTTP only. Waiting for cancellation lets tests coordinate an active native request without sleeps.
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage httpRequestMessage,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequestBodyStrings.Add(await httpRequestMessage.Content!.ReadAsStringAsync(cancellationToken));
        HadCancelableTokenBool = cancellationToken.CanBeCanceled;
        _requestStartedTaskCompletionSource.TrySetResult();
        if (holdResponseBool)
        {
            await Task.Delay(
                Timeout.InfiniteTimeSpan,
                cancellationToken);
        }

        return new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                responseJsonString,
                Encoding.UTF8,
                "application/json")
        };
    }
}
