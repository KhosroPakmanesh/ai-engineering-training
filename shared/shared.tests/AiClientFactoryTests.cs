using System.ClientModel;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Xunit;

public class AiClientFactoryTests
{
    private const string SuccessfulResponse = """
        {"id":"test-completion","object":"chat.completion","created":0,"model":"test-model",
         "choices":[{"index":0,"message":{"role":"assistant","content":"  A complete response.\n"},"finish_reason":"stop"}],
         "usage":{"prompt_tokens":20,"completion_tokens":7,"total_tokens":27}}
        """;

    // Native framework calls must retain the selected endpoint, model, prompts, and generation settings.
    [Theory]
    [InlineData("openrouter", "OpenRouter", "https://openrouter.ai/api/v1")]
    [InlineData("gemini", "Gemini", "https://generativelanguage.googleapis.com/v1beta/openai/")]
    [InlineData("GEMINI", "Gemini", "https://generativelanguage.googleapis.com/v1beta/openai/")]
    public async Task Selected_provider_controls_the_endpoint_without_changing_the_request(
        string provider, string name, string endpoint)
    {
        ProviderSettings settings = SettingsReader.ParseSettings(
        [
            $"AI_PROVIDER={provider}",
            "OPENROUTER_API_KEY=openrouter-key", "OPENROUTER_MODEL=vendor/model",
            "GEMINI_API_KEY=gemini-key", "GEMINI_MODEL=gemini-model"
        ]);
        using var handler = new ResponseHandler();
        using var httpClient = new HttpClient(handler);
        using IChatClient client = new AiClientFactory().CreateClient(settings, httpClient);
        ChatOptions options = CreateOptions();
        ChatClientAgent agent = new(client, new ChatClientAgentOptions { Name = "TestAgent", ChatOptions = options });
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(AiClientFactory.RequestTimeoutSeconds));

        AgentResponse response = await agent.RunAsync("A fictional question.", cancellationToken: deadline.Token);

        Assert.Equal(name, settings.Name);
        Assert.Equal(new Uri(endpoint.TrimEnd('/') + "/chat/completions"), Assert.Single(handler.RequestUris));
        using JsonDocument document = JsonDocument.Parse(Assert.Single(handler.RequestBodies));
        JsonElement body = document.RootElement;
        Assert.Equal(name == "Gemini" ? "gemini-model" : "vendor/model", body.GetProperty("model").GetString());
        Assert.Equal(0.2f, body.GetProperty("temperature").GetSingle());
        Assert.Equal(2048, body.GetProperty("max_completion_tokens").GetInt32());
        Assert.False(body.TryGetProperty("provider", out _));
        Assert.True(handler.HadCancelableToken);
        Assert.Equal("  A complete response.\n", response.Messages.Last(message => message.Role == ChatRole.Assistant).Text);
        Assert.Equal(20, response.Usage?.InputTokenCount);
        Assert.Equal(7, response.Usage?.OutputTokenCount);
        Assert.Equal(27, response.Usage?.TotalTokenCount);
    }

    // The lesson supplies the native JSON schema; only OpenRouter receives its routing extension.
    [Theory]
    [InlineData("OpenRouter", true)]
    [InlineData("Gemini", false)]
    public async Task Structured_requests_preserve_the_schema_and_do_not_mutate_lesson_options(
        string provider, bool requiresParameters)
    {
        const string schema = """
            {"type":"object","properties":{"answer":{"type":"string"}},"required":["answer"],"additionalProperties":false}
            """;
        using var handler = new ResponseHandler();
        using var httpClient = new HttpClient(handler);
        using IChatClient client = new AiClientFactory().CreateClient(TestSettings(provider), httpClient);
        ChatOptions options = CreateOptions();
        options.ResponseFormat = ChatResponseFormat.ForJsonSchema(JsonElement.Parse(schema), "TestAnswer");
        options.AdditionalProperties = new() { ["strict"] = true };

        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "A fictional question.")], options);

        using JsonDocument document = JsonDocument.Parse(Assert.Single(handler.RequestBodies));
        JsonElement body = document.RootElement;
        JsonElement format = body.GetProperty("response_format");
        Assert.Equal("json_schema", format.GetProperty("type").GetString());
        JsonElement jsonSchema = format.GetProperty("json_schema");
        Assert.Equal("TestAnswer", jsonSchema.GetProperty("name").GetString());
        Assert.True(jsonSchema.GetProperty("strict").GetBoolean());
        Assert.True(JsonElement.DeepEquals(JsonElement.Parse(schema), jsonSchema.GetProperty("schema")));
        Assert.Equal(requiresParameters, body.TryGetProperty("provider", out JsonElement routing));
        if (requiresParameters) Assert.True(routing.GetProperty("require_parameters").GetBoolean());
        Assert.Null(options.RawRepresentationFactory);
    }

    // Fresh native agent runs must not inherit previous questions, answers, or tool state.
    [Fact]
    public async Task Independent_agent_runs_send_only_their_own_input()
    {
        using var handler = new ResponseHandler();
        using var httpClient = new HttpClient(handler);
        using IChatClient client = new AiClientFactory().CreateClient(TestSettings(), httpClient);
        ChatClientAgent agent = new(client, new ChatClientAgentOptions { ChatOptions = CreateOptions() });
        List<string> inputs = ["First fictional question.", "Second fictional question."];

        foreach (string input in inputs)
            await agent.RunAsync(input);

        Assert.Equal(2, handler.RequestBodies.Count);
        for (int index = 0; index < inputs.Count; index++)
        {
            using JsonDocument document = JsonDocument.Parse(handler.RequestBodies[index]);
            JsonElement messages = document.RootElement.GetProperty("messages");
            Assert.Equal(2, messages.GetArrayLength());
            Assert.Equal("system", messages[0].GetProperty("role").GetString());
            Assert.Equal("Use only the fictional source facts.", ReadMessageText(messages[0]));
            Assert.Equal("user", messages[1].GetProperty("role").GetString());
            Assert.Equal(inputs[index], ReadMessageText(messages[1]));
        }
    }

    // Reusing a factory and native options must not carry OpenRouter routing into Gemini requests.
    [Fact]
    public async Task One_factory_selects_both_strategies_without_leaking_provider_options()
    {
        var factory = new AiClientFactory();
        using var handler = new ResponseHandler();
        using var httpClient = new HttpClient(handler);
        ChatOptions options = CreateOptions();
        options.ResponseFormat = ChatResponseFormat.ForJsonSchema(JsonElement.Parse("""{"type":"object"}"""), "Answer");
        using IChatClient openRouter = factory.CreateClient(TestSettings("OpenRouter"), httpClient);
        using IChatClient gemini = factory.CreateClient(TestSettings("Gemini"), httpClient);

        await openRouter.GetResponseAsync("First question.", options);
        await gemini.GetResponseAsync("Second question.", options);

        Assert.Equal(2, handler.RequestBodies.Count);
        using JsonDocument first = JsonDocument.Parse(handler.RequestBodies[0]);
        using JsonDocument second = JsonDocument.Parse(handler.RequestBodies[1]);
        Assert.True(first.RootElement.GetProperty("provider").GetProperty("require_parameters").GetBoolean());
        Assert.False(second.RootElement.TryGetProperty("provider", out _));
        Assert.Null(options.RawRepresentationFactory);
    }

    // Unsupported providers must stop before any HTTP request reaches the transport.
    [Fact]
    public void Unknown_strategies_are_rejected_before_a_request()
    {
        var factory = new AiClientFactory();
        using var handler = new ResponseHandler();
        using var httpClient = new HttpClient(handler);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            factory.CreateClient(TestSettings("Unknown"), httpClient));

        Assert.Contains("AI_PROVIDER", error.Message);
        Assert.Contains("openrouter", error.Message);
        Assert.Contains("gemini", error.Message);
        Assert.Empty(handler.RequestBodies);
    }

    // Provider compatibility must work with native tools on every turn, including opaque Gemini metadata.
    [Theory]
    [InlineData("OpenRouter")]
    [InlineData("Gemini")]
    public async Task Native_tool_loop_preserves_declarations_results_and_thought_signatures(string provider)
    {
        int executions = 0;
        JsonNode toolResponse = JsonNode.Parse(SuccessfulResponse)!;
        toolResponse["choices"]![0]!["finish_reason"] = "tool_calls";
        toolResponse["choices"]![0]!["message"]!["content"] = "I will calculate this.";
        toolResponse["choices"]![0]!["message"]!["tool_calls"] = JsonSerializer.SerializeToNode(new[]
        {
            new
            {
                id = "calculation-0", type = "function",
                function = new { name = "Calculate", arguments = "{\"months\":3}" },
                extra_content = new { google = new { thought_signature = "opaque-test-signature" } }
            }
        });
        using var handler = new ResponseHandler(responseBodies: [toolResponse.ToJsonString(), SuccessfulResponse]);
        using var httpClient = new HttpClient(handler);
        using IChatClient client = new FunctionInvokingChatClient(new AiClientFactory().CreateClient(TestSettings(provider), httpClient));
        ChatOptions options = CreateOptions();
        options.Tools = [AIFunctionFactory.Create((int months) => { executions++; return new { totalMonths = months }; }, "Calculate")];
        options.AllowMultipleToolCalls = false;
        ChatClientAgent agent = new(client, new ChatClientAgentOptions { ChatOptions = options, UseProvidedChatClientAsIs = true });

        AgentResponse response = await agent.RunAsync("A fictional question.");

        Assert.Equal(1, executions);
        Assert.Equal("  A complete response.\n", response.Messages.Last(message => message.Role == ChatRole.Assistant).Text);
        FunctionResultContent execution = Assert.Single(response.Messages.SelectMany(message => message.Contents).OfType<FunctionResultContent>());
        Assert.Equal(3, JsonSerializer.SerializeToElement(execution.Result).GetProperty("totalMonths").GetInt32());
        Assert.Equal(2, handler.RequestBodies.Count);
        foreach (string requestBody in handler.RequestBodies)
        {
            using JsonDocument request = JsonDocument.Parse(requestBody);
            JsonElement function = Assert.Single(request.RootElement.GetProperty("tools").EnumerateArray()).GetProperty("function");
            Assert.Equal("Calculate", function.GetProperty("name").GetString());
            Assert.Equal("integer", function.GetProperty("parameters").GetProperty("properties").GetProperty("months").GetProperty("type").GetString());
            Assert.Equal(provider == "OpenRouter", request.RootElement.TryGetProperty("provider", out JsonElement routing));
            if (provider == "OpenRouter") Assert.True(routing.GetProperty("require_parameters").GetBoolean());
        }
        using JsonDocument second = JsonDocument.Parse(handler.RequestBodies[1]);
        JsonElement assistant = second.RootElement.GetProperty("messages").EnumerateArray()
            .Single(message => message.GetProperty("role").GetString() == "assistant");
        Assert.Equal("opaque-test-signature", assistant.GetProperty("tool_calls")[0]
            .GetProperty("extra_content").GetProperty("google").GetProperty("thought_signature").GetString());
        JsonElement toolMessage = second.RootElement.GetProperty("messages").EnumerateArray()
            .Single(message => message.GetProperty("role").GetString() == "tool");
        Assert.Equal("calculation-0", toolMessage.GetProperty("tool_call_id").GetString());
        using JsonDocument result = JsonDocument.Parse(ReadMessageText(toolMessage));
        Assert.Equal(3, result.RootElement.GetProperty("totalMonths").GetInt32());
        Assert.Null(options.RawRepresentationFactory);
    }

    // Missing usage remains unknown in the native response instead of being filled with zeroes.
    [Fact]
    public async Task Missing_usage_keeps_token_counts_unknown()
    {
        JsonNode response = JsonNode.Parse(SuccessfulResponse)!;
        response.AsObject().Remove("usage");
        using var handler = new ResponseHandler(response.ToJsonString());
        using var httpClient = new HttpClient(handler);
        using IChatClient client = new AiClientFactory().CreateClient(TestSettings(), httpClient);

        ChatResponse result = await client.GetResponseAsync("A fictional question.");

        Assert.Null(result.Usage?.InputTokenCount);
        Assert.Null(result.Usage?.OutputTokenCount);
        Assert.Null(result.Usage?.TotalTokenCount);
    }

    // Native SDK exceptions keep their HTTP status and response details.
    [Fact]
    public async Task Api_errors_preserve_the_native_status_and_provider_details()
    {
        const string details = """{"error":{"message":"Unsupported model parameter"}}""";
        using var handler = new ResponseHandler(details, status: 400);
        using var httpClient = new HttpClient(handler);
        using IChatClient client = new AiClientFactory().CreateClient(TestSettings(), httpClient);

        ClientResultException error = await Assert.ThrowsAsync<ClientResultException>(() =>
            client.GetResponseAsync("A fictional question."));

        Assert.Equal(400, error.Status);
        Assert.Equal(details, error.GetRawResponse()?.Content.ToString());
        Assert.Contains("Unsupported model parameter", error.Message);
        Assert.Single(handler.RequestBodies);
    }

    // Cancellation must stop an in-flight request and prevent requests with an already canceled token.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Caller_cancellation_stops_requests(bool cancelBeforeRequest)
    {
        using var cancellation = new CancellationTokenSource();
        using var handler = new ResponseHandler(beforeResponse: cancellation.Cancel);
        using var httpClient = new HttpClient(handler);
        using IChatClient client = new AiClientFactory().CreateClient(TestSettings(), httpClient);
        if (cancelBeforeRequest) cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.GetResponseAsync("A fictional question.", cancellationToken: cancellation.Token));

        Assert.Equal(cancelBeforeRequest ? 0 : 1, handler.RequestBodies.Count);
    }

    // These fixtures use no real credentials or provider connections.
    private static ProviderSettings TestSettings(string provider = "OpenRouter") =>
        new(provider, "test-key", "test-model", new Uri("https://example.invalid/v1"));

    private static ChatOptions CreateOptions() => new()
    {
        Instructions = "Use only the fictional source facts.", Temperature = 0.2f, MaxOutputTokens = 2048
    };

    // The SDK can encode message text as one string or a list of content parts.
    private static string ReadMessageText(JsonElement message)
    {
        JsonElement content = message.GetProperty("content");
        return content.ValueKind == JsonValueKind.String ? content.GetString()! :
            string.Concat(content.EnumerateArray().Select(part => part.GetProperty("text").GetString()));
    }

    // Replace only HTTP; the real SDK and framework still serialize requests and invoke tools.
    private sealed class ResponseHandler(string responseBody = SuccessfulResponse, int status = 200,
        Action? beforeResponse = null, List<string>? responseBodies = null) : HttpMessageHandler
    {
        private readonly Queue<string> _responses = new(responseBodies ?? [responseBody]);
        public List<Uri> RequestUris { get; } = [];
        public List<string> RequestBodies { get; } = [];
        public bool HadCancelableToken { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUris.Add(request.RequestUri!);
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            HadCancelableToken = cancellationToken.CanBeCanceled;
            beforeResponse?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();
            return new HttpResponseMessage((HttpStatusCode)status)
            {
                Content = new StringContent(_responses.Count > 1 ? _responses.Dequeue() : _responses.Peek(), Encoding.UTF8, "application/json")
            };
        }
    }
}
