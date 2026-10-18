using System.ClientModel;
using System.ClientModel.Primitives;
using System.Globalization;
using System.Net;
using System.Text;
using OpenAI;
using OpenAI.Chat;
using Xunit;

// Exercise the real SDK pipeline with local responses and no elapsed retry waits.
public class AiRetryPolicyTests
{
    // Only temporary HTTP failures should result in another request.
    [Theory]
    [InlineData(408)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(504)]
    public async Task Temporary_failure_retries_the_same_request_then_returns_the_answer(int status)
    {
        using var handler = new ResponseHandler(status, 200);
        using var httpClient = new HttpClient(handler);
        var policy = new RecordingRetryPolicy();
        ChatClient client = CreateClient(httpClient, policy);

        ChatCompletion completion = await client.CompleteChatAsync([new UserChatMessage("A fictional question.")]);

        Assert.Equal("A complete answer.", completion.Content[0].Text);
        Assert.Equal(2, handler.RequestBodies.Count);
        Assert.Equal(handler.RequestBodies[0], handler.RequestBodies[1]);
        Assert.InRange(Assert.Single(policy.Delays).TotalSeconds, 2, 2.5);
    }

    // A persistent failure must stop after the initial attempt and two retries.
    [Fact]
    public async Task Persistent_failure_returns_the_final_provider_error_after_three_attempts()
    {
        using var handler = new ResponseHandler(503, 503, 503, 200);
        using var httpClient = new HttpClient(handler);
        var policy = new RecordingRetryPolicy();
        ChatClient client = CreateClient(httpClient, policy);

        ClientResultException error = await Assert.ThrowsAsync<ClientResultException>(async () =>
            await client.CompleteChatAsync([new UserChatMessage("A fictional question.")]));

        Assert.Equal(503, error.Status);
        Assert.Contains("Temporary provider failure", error.GetRawResponse()!.Content.ToString());
        Assert.Equal(3, handler.RequestBodies.Count);
        Assert.Equal(2, policy.Delays.Count);
        Assert.InRange(policy.Delays[0].TotalSeconds, 2, 2.5);
        Assert.InRange(policy.Delays[1].TotalSeconds, 4, 4.5);
    }

    // Invalid settings, permissions, and unsupported operations need a local correction.
    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(402)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(422)]
    [InlineData(501)]
    public async Task Permanent_failure_is_not_retried(int status)
    {
        using var handler = new ResponseHandler(status, 200);
        using var httpClient = new HttpClient(handler);
        var policy = new RecordingRetryPolicy();
        ChatClient client = CreateClient(httpClient, policy);

        ClientResultException error = await Assert.ThrowsAsync<ClientResultException>(async () =>
            await client.CompleteChatAsync([new UserChatMessage("A fictional question.")]));

        Assert.Equal(status, error.Status);
        Assert.Single(handler.RequestBodies);
        Assert.Empty(policy.Delays);
    }

    // A server's minimum wait takes priority; invalid or expired values use the normal backoff.
    [Theory]
    [InlineData("7", 7)]
    [InlineData("0", 2)]
    [InlineData("invalid", 2)]
    [InlineData("Sun, 06 Nov 1994 08:49:37 GMT", 2)]
    public async Task Retry_after_header_sets_the_minimum_delay(string retryAfter, double minimumSeconds)
    {
        using var handler = new ResponseHandler(503, 200) { RetryAfter = retryAfter };
        using var httpClient = new HttpClient(handler);
        var policy = new RecordingRetryPolicy();
        ChatClient client = CreateClient(httpClient, policy);

        await client.CompleteChatAsync([new UserChatMessage("A fictional question.")]);

        Assert.InRange(Assert.Single(policy.Delays).TotalSeconds, minimumSeconds, minimumSeconds + 0.5);
    }

    // HTTP-date Retry-After values must also postpone the next attempt.
    [Fact]
    public async Task Retry_after_http_date_is_respected()
    {
        DateTimeOffset before = DateTimeOffset.UtcNow;
        string retryAfter = before.AddMinutes(1).ToString("r", CultureInfo.InvariantCulture);
        DateTimeOffset retryAt = DateTimeOffset.ParseExact(retryAfter, "r", CultureInfo.InvariantCulture);
        using var handler = new ResponseHandler(503, 200) { RetryAfter = retryAfter };
        using var httpClient = new HttpClient(handler);
        var policy = new RecordingRetryPolicy();
        ChatClient client = CreateClient(httpClient, policy);

        await client.CompleteChatAsync([new UserChatMessage("A fictional question.")]);

        Assert.InRange(Assert.Single(policy.Delays).TotalSeconds,
            (retryAt - DateTimeOffset.UtcNow).TotalSeconds, (retryAt - before).TotalSeconds + 0.5);
    }

    // The SDK passes the lesson's deadline token into the wait between attempts.
    [Fact]
    public async Task Cancellation_during_the_retry_wait_prevents_another_attempt()
    {
        using var deadline = new CancellationTokenSource();
        using var handler = new ResponseHandler(503, 200);
        using var httpClient = new HttpClient(handler);
        var policy = new RecordingRetryPolicy(deadline.Cancel);
        ChatClient client = CreateClient(httpClient, policy);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await client.CompleteChatAsync([new UserChatMessage("A fictional question.")], cancellationToken: deadline.Token));

        Assert.Single(handler.RequestBodies);
        Assert.Single(policy.Delays);
    }

    // Successful requests must return immediately without a delay or duplicate request.
    [Fact]
    public async Task Success_is_not_retried()
    {
        using var handler = new ResponseHandler(200);
        using var httpClient = new HttpClient(handler);
        var policy = new RecordingRetryPolicy();
        ChatClient client = CreateClient(httpClient, policy);

        ChatCompletion completion = await client.CompleteChatAsync([new UserChatMessage("A fictional question.")]);

        Assert.Equal("A complete answer.", completion.Content[0].Text);
        Assert.Single(handler.RequestBodies);
        Assert.Empty(policy.Delays);
    }

    // Synchronous SDK calls use the same policy and attempt limit as asynchronous calls.
    [Fact]
    public void Synchronous_requests_use_the_retry_policy()
    {
        using var handler = new ResponseHandler(503, 200);
        using var httpClient = new HttpClient(handler);
        var policy = new RecordingRetryPolicy();
        ChatClient client = CreateClient(httpClient, policy);

        ChatCompletion completion = client.CompleteChat([new UserChatMessage("A fictional question.")]);

        Assert.Equal("A complete answer.", completion.Content[0].Text);
        Assert.Equal(2, handler.RequestBodies.Count);
        Assert.Single(policy.Delays);
    }

    // Only the HTTP handler is replaced; request serialization and retry execution use the installed SDK.
    private static ChatClient CreateClient(HttpClient httpClient, ClientRetryPolicy policy) => new(
        "test-model", new ApiKeyCredential("test-key"), new OpenAIClientOptions
        {
            Endpoint = new Uri("https://example.invalid/v1"),
            Transport = new HttpClientPipelineTransport(httpClient),
            RetryPolicy = policy,
            NetworkTimeout = TimeSpan.FromSeconds(AiProvider.RequestTimeoutSeconds)
        });

    // Record the scheduled delays without sleeping, and allow a test to cancel during a wait.
    private sealed class RecordingRetryPolicy(Action? beforeWait = null) : AiRetryPolicy
    {
        public List<TimeSpan> Delays { get; } = [];

        protected override Task WaitAsync(TimeSpan time, CancellationToken cancellationToken)
        {
            Wait(time, cancellationToken);
            return Task.CompletedTask;
        }

        protected override void Wait(TimeSpan time, CancellationToken cancellationToken)
        {
            Delays.Add(time);
            beforeWait?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    // All responses stay in memory, so these tests cannot contact a provider or use real credentials.
    private sealed class ResponseHandler(params int[] statuses) : HttpMessageHandler
    {
        private const string SuccessfulResponse = """
            {"id":"test-completion","object":"chat.completion","created":0,"model":"test-model",
             "choices":[{"index":0,"message":{"role":"assistant","content":"A complete answer."},"finish_reason":"stop"}]}
            """;

        public List<string> RequestBodies { get; } = [];
        public string? RetryAfter { get; init; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(Send(request, cancellationToken));

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int status = statuses[Math.Min(RequestBodies.Count, statuses.Length - 1)];
            RequestBodies.Add(request.Content!.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult());
            string body = status == 200 ? SuccessfulResponse : """{"error":{"message":"Temporary provider failure"}}""";
            var response = new HttpResponseMessage((HttpStatusCode)status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            if (RetryAfter is not null) response.Headers.TryAddWithoutValidation("Retry-After", RetryAfter);
            return response;
        }
    }
}
