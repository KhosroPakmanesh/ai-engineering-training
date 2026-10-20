using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

public class AiProviderTests
{
    private const string SuccessfulResponse = """
        {"id":"test-completion","object":"chat.completion","created":0,"model":"test-model",
         "choices":[{"index":0,"message":{"role":"assistant","content":"  A complete response.\n"},"finish_reason":"stop"}],
         "usage":{"prompt_tokens":20,"completion_tokens":7,"total_tokens":27}}
        """;

    // Check the request sent by the AI library without contacting a real provider.
    [Theory]
    [InlineData("openrouter", "OpenRouter", "https://openrouter.ai/api/v1")]
    [InlineData("gemini", "Gemini", "https://generativelanguage.googleapis.com/v1beta/openai/")]
    [InlineData("GEMINI", "Gemini", "https://generativelanguage.googleapis.com/v1beta/openai/")]
    public async Task Selected_provider_controls_the_request_endpoint_model_and_generation_settings(string provider, string name, string endpoint)
    {
        ProviderSettings settings = SettingsReader.ParseSettings(
        [
            $"AI_PROVIDER={provider}",
            "OPENROUTER_API_KEY=openrouter-key",
            "OPENROUTER_MODEL=vendor/model",
            "GEMINI_API_KEY=gemini-key",
            "GEMINI_MODEL=gemini-model"
        ]);

        Assert.Equal(name, settings.Name);
        Assert.Equal(new Uri(endpoint), settings.Endpoint);
        Assert.Equal(name == "Gemini" ? "gemini-key" : "openrouter-key", settings.Key);
        Assert.Equal(name == "Gemini" ? "gemini-model" : "vendor/model", settings.Model);
        using var handler = new ResponseHandler();
        using var httpClient = new HttpClient(handler);
        AiRequest request = CreateRequest();

        AiResponse response = await AiProvider.RequestAsync(settings, request, httpClient);

        Assert.Equal(new Uri(endpoint.TrimEnd('/') + "/chat/completions"), Assert.Single(handler.RequestUris));
        using JsonDocument document = JsonDocument.Parse(Assert.Single(handler.RequestBodies));
        JsonElement body = document.RootElement;
        Assert.Equal(settings.Model, body.GetProperty("model").GetString());
        Assert.Equal(request.Temperature, body.GetProperty("temperature").GetSingle());
        Assert.Equal(request.MaxOutputTokens, body.GetProperty("max_completion_tokens").GetInt32());
        Assert.False(body.TryGetProperty("provider", out _));
        Assert.True(handler.HadCancelableToken);

        Assert.Equal("  A complete response.\n", response.Text);
        Assert.Equal(new AiTokenUsage(20, 7, 27), response.Usage);
        Assert.True(response.LatencyMilliseconds >= 0);
    }

    // Send the same required JSON format to both providers, with routing settings only for OpenRouter.
    [Theory]
    [InlineData("OpenRouter", true)]
    [InlineData("Gemini", false)]
    public async Task Structured_requests_preserve_the_schema_and_apply_only_the_selected_providers_options(
        string provider, bool requiresParameters)
    {
        const string schema = """
            {"type":"object","properties":{"answer":{"type":"string"}},"required":["answer"],"additionalProperties":false}
            """;
        ProviderSettings settings = new(provider, "test-key", "test-model", new Uri("https://example.invalid/v1"));
        AiRequest request = CreateRequest() with { ResponseSchema = new("TestAnswer", schema) };
        using var handler = new ResponseHandler();
        using var httpClient = new HttpClient(handler);

        await AiProvider.RequestAsync(settings, request, httpClient);

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
    }

    // Each call must send only its own instructions and input, without earlier questions or answers.
    [Fact]
    public async Task Each_request_sends_only_its_own_instructions_and_input()
    {
        ProviderSettings settings = new("OpenRouter", "test-key", "test-model", new Uri("https://example.invalid/v1"));
        using var handler = new ResponseHandler();
        using var httpClient = new HttpClient(handler);
        string[] inputs = ["First fictional question.", "Second fictional question."];

        foreach (string input in inputs)
            await AiProvider.RequestAsync(settings, CreateRequest() with { Input = input }, httpClient);

        Assert.Equal(2, handler.RequestBodies.Count);
        for (int index = 0; index < inputs.Length; index++)
        {
            using JsonDocument document = JsonDocument.Parse(handler.RequestBodies[index]);
            JsonElement messages = document.RootElement.GetProperty("messages");
            Assert.Equal(2, messages.GetArrayLength());
            Assert.Equal("system", messages[0].GetProperty("role").GetString());
            Assert.Equal(CreateRequest().Instructions, ReadMessageText(messages[0]));
            Assert.Equal("user", messages[1].GetProperty("role").GetString());
            Assert.Equal(inputs[index], ReadMessageText(messages[1]));
        }
    }

    // Even a successful HTTP request must fail the check if its answer is empty or cut short.
    [Theory]
    [InlineData("", "stop")]
    [InlineData("  ", "stop")]
    [InlineData("Incomplete but nonempty.", "length")]
    public async Task Empty_or_truncated_model_responses_are_rejected(string text, string finishReason)
    {
        JsonNode response = JsonNode.Parse(SuccessfulResponse)!;
        response["choices"]![0]!["message"]!["content"] = text;
        response["choices"]![0]!["finish_reason"] = finishReason;
        ProviderSettings settings = new("OpenRouter", "test-key", "test-model", new Uri("https://example.invalid/v1"));
        using var handler = new ResponseHandler(response.ToJsonString());
        using var httpClient = new HttpClient(handler);

        await Assert.ThrowsAsync<InvalidDataException>(() => AiProvider.RequestAsync(settings, CreateRequest(), httpClient));
        Assert.Single(handler.RequestBodies);
    }

    // Missing token counts must stay unknown, not become zero.
    [Fact]
    public async Task Missing_usage_returns_null()
    {
        JsonNode response = JsonNode.Parse(SuccessfulResponse)!;
        response.AsObject().Remove("usage");
        ProviderSettings settings = new("Gemini", "test-key", "test-model", new Uri("https://example.invalid/v1"));
        using var handler = new ResponseHandler(response.ToJsonString());
        using var httpClient = new HttpClient(handler);

        AiResponse result = await AiProvider.RequestAsync(settings, CreateRequest(), httpClient);

        Assert.Null(result.Usage);
    }

    // Lessons must receive the shared error type, with the API status and details preserved.
    [Fact]
    public async Task Api_errors_keep_the_status_and_details_in_a_shared_exception()
    {
        const string details = """{"error":{"message":"Unsupported model parameter"}}""";
        ProviderSettings settings = new("Gemini", "test-key", "test-model", new Uri("https://example.invalid/v1"));
        using var handler = new ResponseHandler(details, status: 400);
        using var httpClient = new HttpClient(handler);

        AiRequestException error = await Assert.ThrowsAsync<AiRequestException>(() =>
            AiProvider.RequestAsync(settings, CreateRequest(), httpClient));

        Assert.Equal(400, error.Status);
        Assert.Equal(details, error.ResponseDetails);
        Assert.Contains("Unsupported model parameter", error.Message);
        Assert.Single(handler.RequestBodies);
    }

    // Canceling a call must stop the request that is already running.
    [Fact]
    public async Task Caller_cancellation_stops_the_request()
    {
        using var cancellation = new CancellationTokenSource();
        using var handler = new ResponseHandler(beforeResponse: cancellation.Cancel);
        using var httpClient = new HttpClient(handler);
        ProviderSettings settings = new("OpenRouter", "test-key", "test-model", new Uri("https://example.invalid/v1"));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            AiProvider.RequestAsync(settings, CreateRequest(), httpClient, cancellation.Token));

        Assert.Single(handler.RequestBodies);
    }

    // Both providers must receive real tool declarations and the result of the C# execution.
    [Theory]
    [InlineData("OpenRouter")]
    [InlineData("Gemini")]
    public async Task Tool_requests_execute_real_functions_and_return_their_results(string provider)
    {
        int executions = 0;
        AiRequest request = CreateToolRequest((int months) =>
        {
            executions++;
            return new ToolResult(months);
        });
        ProviderSettings settings = new(provider, "test-key", "test-model", new Uri("https://example.invalid/v1"));
        using var handler = new ResponseHandler(responseBodies: [CreateToolResponse(), SuccessfulResponse]);
        using var httpClient = new HttpClient(handler);

        AiResponse response = await AiProvider.RequestAsync(settings, request, httpClient);

        Assert.Equal(1, executions);
        Assert.Equal(2, response.ModelCalls);
        AiToolExecution execution = Assert.Single(response.ToolExecutions);
        Assert.Equal("Calculate", execution.ToolName);
        Assert.Equal(3, execution.Result.GetProperty("totalMonths").GetInt32());
        Assert.True(execution.DurationMilliseconds >= 0);
        Assert.Equal("  A complete response.\n", response.Text);
        using JsonDocument first = JsonDocument.Parse(handler.RequestBodies[0]);
        JsonElement function = Assert.Single(first.RootElement.GetProperty("tools").EnumerateArray()).GetProperty("function");
        Assert.Equal("Calculate", function.GetProperty("name").GetString());
        Assert.Equal("integer", function.GetProperty("parameters").GetProperty("properties").GetProperty("months").GetProperty("type").GetString());
        Assert.False(first.RootElement.GetProperty("parallel_tool_calls").GetBoolean());
        Assert.Equal(provider == "OpenRouter", first.RootElement.TryGetProperty("provider", out JsonElement routing));
        if (provider == "OpenRouter") Assert.True(routing.GetProperty("require_parameters").GetBoolean());
        using JsonDocument second = JsonDocument.Parse(handler.RequestBodies[1]);
        Assert.Single(second.RootElement.GetProperty("tools").EnumerateArray());
        Assert.Equal(4, second.RootElement.GetProperty("messages").GetArrayLength());
        JsonElement toolMessage = second.RootElement.GetProperty("messages").EnumerateArray()
            .Single(message => message.GetProperty("role").GetString() == "tool");
        Assert.Equal("calculation-0", toolMessage.GetProperty("tool_call_id").GetString());
        JsonElement assistantMessage = second.RootElement.GetProperty("messages").EnumerateArray()
            .Single(message => message.GetProperty("role").GetString() == "assistant");
        Assert.Equal("opaque-test-signature", assistantMessage.GetProperty("tool_calls")[0]
            .GetProperty("extra_content").GetProperty("google").GetProperty("thought_signature").GetString());
        using JsonDocument result = JsonDocument.Parse(ReadMessageText(toolMessage));
        Assert.Equal(3, result.RootElement.GetProperty("totalMonths").GetInt32());
    }

    // Invalid batches must fail before any function runs or another paid turn is sent.
    [Theory]
    [InlineData("Unknown", "{\"months\":3}", 1, "tool_calls")]
    [InlineData("Calculate", "not-json", 1, "tool_calls")]
    [InlineData("Calculate", "{\"months\":3}", 2, "tool_calls")]
    [InlineData("Calculate", "{\"months\":3}", 1, "length")]
    public async Task Unknown_malformed_excessive_or_truncated_tool_calls_stop_immediately(
        string name, string arguments, int callCount, string finishReason)
    {
        int executions = 0;
        AiRequest request = CreateToolRequest((int months) => { executions++; return new ToolResult(months); });
        ProviderSettings settings = new("OpenRouter", "test-key", "test-model", new Uri("https://example.invalid/v1"));
        using var handler = new ResponseHandler(CreateToolResponse(name, arguments, callCount, finishReason));
        using var httpClient = new HttpClient(handler);

        await Assert.ThrowsAsync<InvalidDataException>(() => AiProvider.RequestAsync(settings, request, httpClient));
        Assert.Equal(0, executions);
        Assert.Single(handler.RequestBodies);
    }

    // A function failure must not be hidden by letting the model fabricate an answer afterwards.
    [Fact]
    public async Task Function_errors_stop_without_a_followup_model_call()
    {
        Func<int, ToolResult> function = months => throw new InvalidDataException("The source dates are invalid.");
        using var handler = new ResponseHandler(CreateToolResponse());
        using var httpClient = new HttpClient(handler);
        ProviderSettings settings = new("OpenRouter", "test-key", "test-model", new Uri("https://example.invalid/v1"));

        await Assert.ThrowsAsync<InvalidDataException>(() => AiProvider.RequestAsync(settings, CreateToolRequest(function), httpClient));
        Assert.Single(handler.RequestBodies);
    }

    // Even if the provider requests tools again, the second turn cannot start another calculation.
    [Fact]
    public async Task A_repeated_tool_request_cannot_exceed_the_model_or_tool_budget()
    {
        int executions = 0;
        AiRequest request = CreateToolRequest((int months) => { executions++; return new ToolResult(months); });
        using var handler = new ResponseHandler(CreateToolResponse());
        using var httpClient = new HttpClient(handler);
        ProviderSettings settings = new("OpenRouter", "test-key", "test-model", new Uri("https://example.invalid/v1"));

        await Assert.ThrowsAsync<InvalidDataException>(() => AiProvider.RequestAsync(settings, request, httpClient));
        Assert.Equal(1, executions);
        Assert.Equal(2, handler.RequestBodies.Count);
    }

    // Separate facade runs must not share their tool traces, budgets, or conversation history.
    [Fact]
    public async Task Tool_sessions_keep_independent_counters_results_and_messages()
    {
        using var handler = new ResponseHandler(responseBodies:
            [CreateToolResponse(), SuccessfulResponse, CreateToolResponse(), SuccessfulResponse]);
        using var httpClient = new HttpClient(handler);
        ProviderSettings settings = new("OpenRouter", "test-key", "test-model", new Uri("https://example.invalid/v1"));
        AiRequest request = CreateToolRequest((int months) => new ToolResult(months));
        AiResponse first = await AiProvider.RequestAsync(settings, request, httpClient);
        AiResponse second = await AiProvider.RequestAsync(settings, request with { Input = "Another independent question." }, httpClient);

        Assert.Single(first.ToolExecutions);
        Assert.Single(second.ToolExecutions);
        Assert.NotSame(first.ToolExecutions, second.ToolExecutions);
        Assert.Equal(2, first.ModelCalls);
        Assert.Equal(2, second.ModelCalls);
        using JsonDocument thirdRequest = JsonDocument.Parse(handler.RequestBodies[2]);
        JsonElement messages = thirdRequest.RootElement.GetProperty("messages");
        Assert.Equal(2, messages.GetArrayLength());
        Assert.Equal("Another independent question.", ReadMessageText(messages[1]));
    }

    // These settings deliberately allow only one local calculation and its final explanation.
    private static AiRequest CreateToolRequest(Delegate function) => CreateRequest() with
    {
        ToolCalling = new([new("Calculate", "Calculate a month total.", function)], MaxModelCalls: 2, MaxToolCalls: 1)
    };

    // Include a preamble to check that only the final model answer becomes AiResponse.Text.
    private static string CreateToolResponse(string name = "Calculate", string arguments = "{\"months\":3}",
        int callCount = 1, string finishReason = "tool_calls")
    {
        JsonNode response = JsonNode.Parse(SuccessfulResponse)!;
        response["choices"]![0]!["finish_reason"] = finishReason;
        response["choices"]![0]!["message"]!["content"] = "I will calculate this.";
        response["choices"]![0]!["message"]!["tool_calls"] = JsonSerializer.SerializeToNode(
            Enumerable.Range(0, callCount).Select(index => new
            {
                id = $"calculation-{index}", type = "function", function = new { name, arguments },
                extra_content = new { google = new { thought_signature = "opaque-test-signature" } }
            }));
        return response.ToJsonString();
    }

    private sealed record ToolResult(int TotalMonths);

    // Give each test a new request so it can change the input without affecting other tests.
    private static AiRequest CreateRequest() => new(
        Name: "TestAgent",
        Instructions: "Use only the fictional source facts.",
        Input: "A fictional question.",
        Temperature: 0.2f,
        MaxOutputTokens: 2048);

    // The AI library can send message text as one string or as a list of text pieces.
    private static string ReadMessageText(JsonElement message)
    {
        JsonElement content = message.GetProperty("content");
        return content.ValueKind == JsonValueKind.String ? content.GetString()! :
            string.Concat(content.EnumerateArray().Select(part => part.GetProperty("text").GetString()));
    }

    // Return local HTTP responses while the real agent and AI library build and read the messages.
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
