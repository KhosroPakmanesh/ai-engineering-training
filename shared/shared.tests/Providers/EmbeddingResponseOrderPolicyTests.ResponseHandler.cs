using System.Net;
using System.Text;

public partial class EmbeddingResponseOrderPolicyTests
{
    // Replace HTTP while exercising the real SDK policy and Microsoft's standard embedding adapter.
    private sealed class ResponseHandler(string responseString) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage requestHttpRequestMessage,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    responseString,
                    Encoding.UTF8,
                    "application/json")
            });
        }
    }
}

