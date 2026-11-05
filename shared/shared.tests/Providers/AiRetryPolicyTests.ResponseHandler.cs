using System.Net;
using System.Text;

public partial class AiRetryPolicyTests
{
    // Return responses from the test itself so no real provider or API key is needed.
    private sealed class ResponseHandler(params int[] statuseInts) : HttpMessageHandler
    {
        private const string SuccessfulResponseString = """
            {"id":"test-completion","object":"chat.completion","created":0,"model":"test-model",
             "choices":[{"index":0,"message":{"role":"assistant","content":"A complete answer."},"finish_reason":"stop"}]}
            """;

        public List<string> RequestBodyStrings { get; } = [];
        public string? RetryAfterString
        {
            get; init;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage requestHttpRequestMessage,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Send(
                requestHttpRequestMessage,
                cancellationToken));
        }

        protected override HttpResponseMessage Send(
            HttpRequestMessage requestHttpRequestMessage,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int statusInt = statuseInts[Math.Min(
                RequestBodyStrings.Count,
                statuseInts.Length - 1)];
            RequestBodyStrings.Add(requestHttpRequestMessage.Content!.ReadAsStringAsync(cancellationToken).GetAwaiter(
                ).GetResult());
            string bodyString =
                statusInt == 200 ? SuccessfulResponseString : """{"error":{"message":"Temporary provider failure"}}""";
            var responseHttpResponseMessage = new HttpResponseMessage((HttpStatusCode)statusInt)
            {
                Content = new StringContent(
                    bodyString,
                    Encoding.UTF8,
                    "application/json")
            };
            if (RetryAfterString is not null)
            {
                responseHttpResponseMessage.Headers.TryAddWithoutValidation(
                    "Retry-After",
                    RetryAfterString);
            }

            return responseHttpResponseMessage;
        }
    }
}

