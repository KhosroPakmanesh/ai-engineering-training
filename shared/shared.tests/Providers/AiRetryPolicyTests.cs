using System.ClientModel;
using System.ClientModel.Primitives;
using System.Globalization;

using OpenAI;
using OpenAI.Chat;
using Xunit;

// Check retries using the real AI library, local HTTP responses, and no waiting.
public partial class AiRetryPolicyTests
{
    // Only temporary HTTP failures should result in another request.
    [Theory]
    [InlineData(408)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(504)]
    public async Task Temporary_failure_retries_the_same_request_then_returns_the_answer(int statusInt)
    {
        // Arrange
        using var responseHandler = new ResponseHandler(
            statusInt,
            200);
        using var httpClient = new HttpClient(responseHandler);
        var recordingRetryPolicy = new RecordingRetryPolicy();
        ChatClient chatClient = CreateClient(
            httpClient,
            recordingRetryPolicy);

        // Act
        ChatCompletion chatCompletion =
            await chatClient.CompleteChatAsync([new UserChatMessage("A fictional question.")]);

        // Assert
        Assert.Equal(
            "A complete answer.",
            chatCompletion.Content[0].Text);
        Assert.Equal(
            2,
            responseHandler.RequestBodyStrings.Count);
        Assert.Equal(
            responseHandler.RequestBodyStrings[0],
            responseHandler.RequestBodyStrings[1]);
        Assert.InRange(
            Assert.Single(recordingRetryPolicy.DelaysTimeSpans).TotalSeconds,
            2,
            2.5);
    }

    // A persistent failure must stop after the initial attempt and two retries.
    [Fact]
    public async Task Persistent_failure_returns_the_final_provider_error_after_three_attempts()
    {
        // Arrange
        using var responseHandler = new ResponseHandler(
            503,
            503,
            503,
            200);
        using var httpClient = new HttpClient(responseHandler);
        var recordingRetryPolicy = new RecordingRetryPolicy();
        ChatClient chatClient = CreateClient(
            httpClient,
            recordingRetryPolicy);

        // Act
        ClientResultException errorClientResultException = await Assert.ThrowsAsync<ClientResultException>(async () =>
            await chatClient.CompleteChatAsync([new UserChatMessage("A fictional question.")]));

        // Assert
        Assert.Equal(
            503,
            errorClientResultException.Status);
        Assert.Contains(
            "Temporary provider failure",
            errorClientResultException.GetRawResponse()!.Content.ToString());
        Assert.Equal(
            3,
            responseHandler.RequestBodyStrings.Count);
        Assert.Equal(
            2,
            recordingRetryPolicy.DelaysTimeSpans.Count);
        Assert.InRange(
            recordingRetryPolicy.DelaysTimeSpans[0].TotalSeconds,
            2,
            2.5);
        Assert.InRange(
            recordingRetryPolicy.DelaysTimeSpans[1].TotalSeconds,
            4,
            4.5);
    }

    // Do not retry errors that require changing settings, access, or the requested operation.
    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(402)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(422)]
    [InlineData(501)]
    public async Task Permanent_failure_is_not_retried(int statusInt)
    {
        // Arrange
        using var responseHandler = new ResponseHandler(
            statusInt,
            200);
        using var httpClient = new HttpClient(responseHandler);
        var recordingRetryPolicy = new RecordingRetryPolicy();
        ChatClient chatClient = CreateClient(
            httpClient,
            recordingRetryPolicy);

        // Act
        ClientResultException errorClientResultException = await Assert.ThrowsAsync<ClientResultException>(async () =>
            await chatClient.CompleteChatAsync([new UserChatMessage("A fictional question.")]));

        // Assert
        Assert.Equal(
            statusInt,
            errorClientResultException.Status);
        Assert.Single(responseHandler.RequestBodyStrings);
        Assert.Empty(recordingRetryPolicy.DelaysTimeSpans);
    }

    // Wait as long as the provider asks; use the usual delay if its wait value is invalid or past.
    [Theory]
    [InlineData(
        "7",
        7)]
    [InlineData(
        "0",
        2)]
    [InlineData(
        "invalid",
        2)]
    [InlineData(
        "Sun, 06 Nov 1994 08:49:37 GMT",
        2)]
    public async Task Retry_after_header_sets_the_minimum_delay(
        string retryAfterString,
        double minimumSecondsDouble)
    {
        // Arrange
        using var responseHandler = new ResponseHandler(
            503,
            200)
        {
            RetryAfterString = retryAfterString
        };
        using var httpClient = new HttpClient(responseHandler);
        var recordingRetryPolicy = new RecordingRetryPolicy();
        ChatClient chatClient = CreateClient(
            httpClient,
            recordingRetryPolicy);

        // Act
        await chatClient.CompleteChatAsync([new UserChatMessage("A fictional question.")]);

        // Assert
        Assert.InRange(
            Assert.Single(recordingRetryPolicy.DelaysTimeSpans).TotalSeconds,
            minimumSecondsDouble,
            minimumSecondsDouble + 0.5);
    }

    // The provider can ask us to wait until a date and time, not just for a number of seconds.
    [Fact]
    public async Task Retry_after_http_date_is_respected()
    {
        // Arrange
        DateTimeOffset beforeDateTimeOffset = DateTimeOffset.UtcNow;
        string retryAfterString = beforeDateTimeOffset.AddMinutes(1).ToString(
            "r",
            CultureInfo.InvariantCulture);
        DateTimeOffset retryAtDateTimeOffset = DateTimeOffset.ParseExact(
            retryAfterString,
            "r",
            CultureInfo.InvariantCulture);
        using var responseHandler = new ResponseHandler(
            503,
            200)
        {
            RetryAfterString = retryAfterString
        };
        using var httpClient = new HttpClient(responseHandler);
        var recordingRetryPolicy = new RecordingRetryPolicy();
        ChatClient chatClient = CreateClient(
            httpClient,
            recordingRetryPolicy);

        // Act
        await chatClient.CompleteChatAsync([new UserChatMessage("A fictional question.")]);

        // Assert
        Assert.InRange(
            Assert.Single(recordingRetryPolicy.DelaysTimeSpans).TotalSeconds,
            (retryAtDateTimeOffset - DateTimeOffset.UtcNow).TotalSeconds,
            (retryAtDateTimeOffset - beforeDateTimeOffset).TotalSeconds + 0.5);
    }

    // Canceling the call must also stop the wait before the next attempt.
    [Fact]
    public async Task Cancellation_during_the_retry_wait_prevents_another_attempt()
    {
        // Arrange
        using var deadlineCancellationTokenSource = new CancellationTokenSource();
        using var responseHandler = new ResponseHandler(
            503,
            200);
        using var httpClient = new HttpClient(responseHandler);
        var recordingRetryPolicy = new RecordingRetryPolicy(deadlineCancellationTokenSource.Cancel);
        ChatClient chatClient = CreateClient(
            httpClient,
            recordingRetryPolicy);

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await chatClient.CompleteChatAsync(
                [new UserChatMessage("A fictional question.")],
                cancellationToken: deadlineCancellationTokenSource.Token));

        // Assert
        Assert.Single(responseHandler.RequestBodyStrings);
        Assert.Single(recordingRetryPolicy.DelaysTimeSpans);
    }

    // Successful requests must return immediately without a delay or duplicate request.
    [Fact]
    public async Task Success_is_not_retried()
    {
        // Arrange
        using var responseHandler = new ResponseHandler(200);
        using var httpClient = new HttpClient(responseHandler);
        var recordingRetryPolicy = new RecordingRetryPolicy();
        ChatClient chatClient = CreateClient(
            httpClient,
            recordingRetryPolicy);

        // Act
        ChatCompletion chatCompletion =
            await chatClient.CompleteChatAsync([new UserChatMessage("A fictional question.")]);

        // Assert
        Assert.Equal(
            "A complete answer.",
            chatCompletion.Content[0].Text);
        Assert.Single(responseHandler.RequestBodyStrings);
        Assert.Empty(recordingRetryPolicy.DelaysTimeSpans);
    }

    // Calls that wait for a result directly must follow the same retry rules as calls using await.
    [Fact]
    public void Synchronous_requests_use_the_retry_policy()
    {
        // Arrange
        using var responseHandler = new ResponseHandler(
            503,
            200);
        using var httpClient = new HttpClient(responseHandler);
        var recordingRetryPolicy = new RecordingRetryPolicy();
        ChatClient chatClient = CreateClient(
            httpClient,
            recordingRetryPolicy);

        // Act
        ChatCompletion chatCompletion = chatClient.CompleteChat([new UserChatMessage("A fictional question.")]);

        // Assert
        Assert.Equal(
            "A complete answer.",
            chatCompletion.Content[0].Text);
        Assert.Equal(
            2,
            responseHandler.RequestBodyStrings.Count);
        Assert.Single(recordingRetryPolicy.DelaysTimeSpans);
    }

    // Use local HTTP responses while the real AI library builds requests and retries failures.
    private static ChatClient CreateClient(
        HttpClient httpClient,
        ClientRetryPolicy clientRetryPolicy)
    {
        return new(
            "test-model",
            new ApiKeyCredential("test-key"),
            new OpenAIClientOptions
            {
                Endpoint = new Uri("https://example.invalid/v1"),
                Transport = new HttpClientPipelineTransport(httpClient),
                RetryPolicy = clientRetryPolicy,
                NetworkTimeout = TimeSpan.FromSeconds(AiClientFactory.RequestTimeoutSeconds)
            });
    }
}
