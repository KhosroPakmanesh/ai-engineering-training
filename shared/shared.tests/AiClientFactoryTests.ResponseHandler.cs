using System.Net;
using System.Text;

public partial class AiClientFactoryTests
{
    // Replace only HTTP; the real SDK and framework still serialize requests and invoke tools.
    private sealed class ResponseHandler(
        string responseBodyString = SuccessfulResponseString,
        int statusInt = 200,
        Action? beforeResponseAction = null,
        List<string>? responseBodyStrings = null) : HttpMessageHandler
    {
        private readonly Queue<string> _responseStringsQueue = new(responseBodyStrings ?? [responseBodyString]);

        public List<Uri> RequestUris { get; } = [];
        public List<string> RequestBodyStrings { get; } = [];
        public bool HadCancelableTokenBool
        {
            get; private set;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage requestHttpRequestMessage,
            CancellationToken cancellationToken)
        {
            RequestUris.Add(requestHttpRequestMessage.RequestUri!);
            RequestBodyStrings.Add(await requestHttpRequestMessage.Content!.ReadAsStringAsync(cancellationToken));
            HadCancelableTokenBool = cancellationToken.CanBeCanceled;
            beforeResponseAction?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();
            return new HttpResponseMessage((HttpStatusCode)statusInt)
            {
                Content = new StringContent(
                    _responseStringsQueue.Count > 1 ? _responseStringsQueue.Dequeue() : _responseStringsQueue.Peek(),
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }
}

