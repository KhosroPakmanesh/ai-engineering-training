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
    private const string SuccessfulResponseString = """
        {"id":"test-completion","object":"chat.completion","created":0,"model":"test-model",
         "choices":[{"index":0,"message":{"role":"assistant","content":"  A complete response.\n"},"finish_reason":"stop"}],
         "usage":{"prompt_tokens":20,"completion_tokens":7,"total_tokens":27}}
        """;

    // Native framework calls must retain the selected endpoint, model, prompts, and generation settings.
    [Theory]
    [InlineData("openrouter", "OpenRouter", "https://openrouter.ai/api/v1")]
    [InlineData("gemini", "Gemini", "https://generativelanguage.googleapis.com/v1beta/openai/")]
    [InlineData("GEMINI", "Gemini", "https://generativelanguage.googleapis.com/v1beta/openai/")]
    public async Task Selected_provider_controls_the_endpoint_without_changing_the_request(string providerString, string nameString,
        string endpointString)
    {
        ProviderSettings providerSettings = SettingsReader.ParseSettings(
        [
            $"AI_PROVIDER={providerString}",
            "OPENROUTER_API_KEY=openrouter-key", "OPENROUTER_MODEL=vendor/model",
            "GEMINI_API_KEY=gemini-key", "GEMINI_MODEL=gemini-model"
        ]);
        using var responseHandler = new ResponseHandler();
        using var httpClient = new HttpClient(responseHandler);
        using IChatClient iChatClient = new AiClientFactory().CreateClient(providerSettings, httpClient);
        ChatOptions chatOptions = CreateOptions();
        ChatClientAgent chatClientAgent = new(iChatClient, new ChatClientAgentOptions { Name = "TestAgent", ChatOptions = chatOptions });
        using var deadlineCancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(AiClientFactory.RequestTimeoutSeconds));

        AgentResponse agentResponse = await chatClientAgent.RunAsync("A fictional question.", cancellationToken: deadlineCancellationTokenSource.Token);

        Assert.Equal(nameString, providerSettings.NameString);
        Assert.Equal(new Uri(endpointString.TrimEnd('/') + "/chat/completions"), Assert.Single(responseHandler.RequestUris));
        using JsonDocument jsonDocument = JsonDocument.Parse(Assert.Single(responseHandler.RequestBodyStrings));
        JsonElement bodyJsonElement = jsonDocument.RootElement;
        Assert.Equal(nameString == "Gemini" ? "gemini-model" : "vendor/model", bodyJsonElement.GetProperty("model").GetString());
        Assert.Equal(0.2f, bodyJsonElement.GetProperty("temperature").GetSingle());
        Assert.Equal(2048, bodyJsonElement.GetProperty("max_completion_tokens").GetInt32());
        Assert.False(bodyJsonElement.TryGetProperty("provider", out _));
        Assert.True(responseHandler.HadCancelableTokenBool);
        Assert.Equal("  A complete response.\n", agentResponse.Messages.Last(chatMessage => chatMessage.Role == ChatRole.Assistant).Text);
        Assert.Equal(20, agentResponse.Usage?.InputTokenCount);
        Assert.Equal(7, agentResponse.Usage?.OutputTokenCount);
        Assert.Equal(27, agentResponse.Usage?.TotalTokenCount);
    }

    // The lesson supplies the native JSON schema; only OpenRouter receives its routing extension.
    [Theory]
    [InlineData("OpenRouter", true)]
    [InlineData("Gemini", false)]
    public async Task Structured_requests_preserve_the_schema_and_do_not_mutate_lesson_options(string providerString,
        bool requiresParametersBool)
    {
        const string schemaString = """
            {"type":"object","properties":{"answer":{"type":"string"}},"required":["answer"],"additionalProperties":false}
            """;
        using var responseHandler = new ResponseHandler();
        using var httpClient = new HttpClient(responseHandler);
        using IChatClient iChatClient = new AiClientFactory().CreateClient(TestSettings(providerString), httpClient);
        ChatOptions chatOptions = CreateOptions();
        chatOptions.ResponseFormat = ChatResponseFormat.ForJsonSchema(JsonElement.Parse(schemaString), "TestAnswer");
        chatOptions.AdditionalProperties = new()
        {
            ["strict"] = true
        };

        await iChatClient.GetResponseAsync([new ChatMessage(ChatRole.User, "A fictional question.")], chatOptions);

        using JsonDocument jsonDocument = JsonDocument.Parse(Assert.Single(responseHandler.RequestBodyStrings));
        JsonElement bodyJsonElement = jsonDocument.RootElement;
        JsonElement formatJsonElement = bodyJsonElement.GetProperty("response_format");
        Assert.Equal("json_schema", formatJsonElement.GetProperty("type").GetString());
        JsonElement jsonSchemaJsonElement = formatJsonElement.GetProperty("json_schema");
        Assert.Equal("TestAnswer", jsonSchemaJsonElement.GetProperty("name").GetString());
        Assert.True(jsonSchemaJsonElement.GetProperty("strict").GetBoolean());
        Assert.True(JsonElement.DeepEquals(JsonElement.Parse(schemaString), jsonSchemaJsonElement.GetProperty("schema")));
        Assert.Equal(requiresParametersBool, bodyJsonElement.TryGetProperty("provider", out JsonElement routingJsonElement));
        if (requiresParametersBool)
        {
            Assert.True(routingJsonElement.GetProperty("require_parameters").GetBoolean());
        }

        Assert.Null(chatOptions.RawRepresentationFactory);
    }

    // Fresh native agent runs must not inherit previous questions, answers, or tool state.
    [Fact]
    public async Task Independent_agent_runs_send_only_their_own_input()
    {
        using var responseHandler = new ResponseHandler();
        using var httpClient = new HttpClient(responseHandler);
        using IChatClient iChatClient = new AiClientFactory().CreateClient(TestSettings(), httpClient);
        ChatClientAgent chatClientAgent = new(iChatClient, new ChatClientAgentOptions { ChatOptions = CreateOptions() });
        List<string> inputStrings = ["First fictional question.", "Second fictional question."];

        foreach (string inputString in inputStrings)
        {
            await chatClientAgent.RunAsync(inputString);
        }

        Assert.Equal(2, responseHandler.RequestBodyStrings.Count);
        for (int indexInt = 0; indexInt < inputStrings.Count; indexInt++)
        {
            using JsonDocument jsonDocument = JsonDocument.Parse(responseHandler.RequestBodyStrings[indexInt]);
            JsonElement messagesJsonElement = jsonDocument.RootElement.GetProperty("messages");
            Assert.Equal(2, messagesJsonElement.GetArrayLength());
            Assert.Equal("system", messagesJsonElement[0].GetProperty("role").GetString());
            Assert.Equal("Use only the fictional source facts.", ReadMessageText(messagesJsonElement[0]));
            Assert.Equal("user", messagesJsonElement[1].GetProperty("role").GetString());
            Assert.Equal(inputStrings[indexInt], ReadMessageText(messagesJsonElement[1]));
        }
    }

    // Reusing a factory and native options must not carry OpenRouter routing into Gemini requests.
    [Fact]
    public async Task One_factory_selects_both_strategies_without_leaking_provider_options()
    {
        var aiClientFactory = new AiClientFactory();
        using var responseHandler = new ResponseHandler();
        using var httpClient = new HttpClient(responseHandler);
        ChatOptions chatOptions = CreateOptions();
        chatOptions.ResponseFormat = ChatResponseFormat.ForJsonSchema(JsonElement.Parse("""{"type":"object"}"""), "Answer");
        using IChatClient openRouterIChatClient = aiClientFactory.CreateClient(TestSettings("OpenRouter"), httpClient);
        using IChatClient geminIChatClient = aiClientFactory.CreateClient(TestSettings("Gemini"), httpClient);

        await openRouterIChatClient.GetResponseAsync("First question.", chatOptions);
        await geminIChatClient.GetResponseAsync("Second question.", chatOptions);

        Assert.Equal(2, responseHandler.RequestBodyStrings.Count);
        using JsonDocument firstJsonDocument = JsonDocument.Parse(responseHandler.RequestBodyStrings[0]);
        using JsonDocument secondJsonDocument = JsonDocument.Parse(responseHandler.RequestBodyStrings[1]);
        Assert.True(firstJsonDocument.RootElement.GetProperty("provider").GetProperty("require_parameters").GetBoolean());
        Assert.False(secondJsonDocument.RootElement.TryGetProperty("provider", out _));
        Assert.Null(chatOptions.RawRepresentationFactory);
    }

    // Unsupported providers must stop before any HTTP request reaches the transport.
    [Fact]
    public void Unknown_strategies_are_rejected_before_a_request()
    {
        var aiClientFactory = new AiClientFactory();
        using var responseHandler = new ResponseHandler();
        using var httpClient = new HttpClient(responseHandler);

        InvalidOperationException errorInvalidOperationException = Assert.Throws<InvalidOperationException>(() =>
            aiClientFactory.CreateClient(TestSettings("Unknown"), httpClient));

        Assert.Contains("AI_PROVIDER", errorInvalidOperationException.Message);
        Assert.Contains("openrouter", errorInvalidOperationException.Message);
        Assert.Contains("gemini", errorInvalidOperationException.Message);
        Assert.Empty(responseHandler.RequestBodyStrings);
    }

    // Provider compatibility must work with native tools on every turn, including opaque Gemini metadata.
    [Theory]
    [InlineData("OpenRouter")]
    [InlineData("Gemini")]
    public async Task Native_tool_loop_preserves_declarations_results_and_thought_signatures(string providerString)
    {
        int executionsInt = 0;
        JsonNode toolResponseJsonNode = JsonNode.Parse(SuccessfulResponseString)!;
        toolResponseJsonNode["choices"]![0]!["finish_reason"] = "tool_calls";
        toolResponseJsonNode["choices"]![0]!["message"]!["content"] = "I will calculate this.";
        toolResponseJsonNode["choices"]![0]!["message"]!["tool_calls"] = JsonSerializer.SerializeToNode(new[]
        {
            new
            {
                id = "calculation-0", type = "function",
                function = new { name = "Calculate", arguments = "{\"months\":3}" },
                extra_content = new { google = new { thought_signature = "opaque-test-signature" } }
            }
        });
        using var responseHandler = new ResponseHandler(responseBodyStrings: [toolResponseJsonNode.ToJsonString(), SuccessfulResponseString]);
        using var httpClient = new HttpClient(responseHandler);
        using IChatClient iChatClient = new FunctionInvokingChatClient(new AiClientFactory().CreateClient(TestSettings(providerString), httpClient));
        ChatOptions chatOptions = CreateOptions();
        chatOptions.Tools = [AIFunctionFactory.Create((int months) => { executionsInt++; return new { totalMonths = months }; }, "Calculate")];
        chatOptions.AllowMultipleToolCalls = false;
        ChatClientAgent chatClientAgent = new(iChatClient,
            new ChatClientAgentOptions { ChatOptions = chatOptions, UseProvidedChatClientAsIs = true });

        AgentResponse agentResponse = await chatClientAgent.RunAsync("A fictional question.");

        Assert.Equal(1, executionsInt);
        Assert.Equal("  A complete response.\n", agentResponse.Messages.Last(chatMessage => chatMessage.Role == ChatRole.Assistant).Text);
        FunctionResultContent executionFunctionResultContent = Assert.Single(agentResponse.Messages.SelectMany(chatMessage => chatMessage.Contents).OfType<FunctionResultContent>());
        Assert.Equal(3, JsonSerializer.SerializeToElement(executionFunctionResultContent.Result).GetProperty("totalMonths").GetInt32());
        Assert.Equal(2, responseHandler.RequestBodyStrings.Count);
        foreach (string requestBodyString in responseHandler.RequestBodyStrings)
        {
            using JsonDocument requestJsonDocument = JsonDocument.Parse(requestBodyString);
            JsonElement functionJsonElement = Assert.Single(requestJsonDocument.RootElement.GetProperty("tools").EnumerateArray()).GetProperty("function");
            Assert.Equal("Calculate", functionJsonElement.GetProperty("name").GetString());
            Assert.Equal("integer",
                functionJsonElement.GetProperty("parameters").GetProperty("properties").GetProperty("months").GetProperty("type").GetString());
            Assert.Equal(providerString == "OpenRouter",
                requestJsonDocument.RootElement.TryGetProperty("provider", out JsonElement routingJsonElement));
            if (providerString == "OpenRouter")
            {
                Assert.True(routingJsonElement.GetProperty("require_parameters").GetBoolean());
            }
        }
        using JsonDocument secondJsonDocument = JsonDocument.Parse(responseHandler.RequestBodyStrings[1]);
        JsonElement assistantJsonElement = secondJsonDocument.RootElement.GetProperty("messages").EnumerateArray()
            .Single(messageJsonElement => messageJsonElement.GetProperty("role").GetString() == "assistant");
        Assert.Equal(
            "opaque-test-signature",
            assistantJsonElement.GetProperty("tool_calls")[0]
            .GetProperty("extra_content").GetProperty("google").GetProperty("thought_signature").GetString());
        JsonElement toolMessageJsonElement = secondJsonDocument.RootElement.GetProperty("messages").EnumerateArray()
            .Single(messageJsonElement => messageJsonElement.GetProperty("role").GetString() == "tool");
        Assert.Equal("calculation-0", toolMessageJsonElement.GetProperty("tool_call_id").GetString());
        using JsonDocument resultJsonDocument = JsonDocument.Parse(ReadMessageText(toolMessageJsonElement));
        Assert.Equal(3, resultJsonDocument.RootElement.GetProperty("totalMonths").GetInt32());
        Assert.Null(chatOptions.RawRepresentationFactory);
    }

    // Missing usage remains unknown in the native response instead of being filled with zeroes.
    [Fact]
    public async Task Missing_usage_keeps_token_counts_unknown()
    {
        JsonNode responseJsonNode = JsonNode.Parse(SuccessfulResponseString)!;
        responseJsonNode.AsObject().Remove("usage");
        using var responseHandler = new ResponseHandler(responseJsonNode.ToJsonString());
        using var httpClient = new HttpClient(responseHandler);
        using IChatClient iChatClient = new AiClientFactory().CreateClient(TestSettings(), httpClient);

        ChatResponse resultChatResponse = await iChatClient.GetResponseAsync("A fictional question.");

        Assert.Null(resultChatResponse.Usage?.InputTokenCount);
        Assert.Null(resultChatResponse.Usage?.OutputTokenCount);
        Assert.Null(resultChatResponse.Usage?.TotalTokenCount);
    }

    // Native SDK exceptions keep their HTTP status and response details.
    [Fact]
    public async Task Api_errors_preserve_the_native_status_and_provider_details()
    {
        const string detailsString = """{"error":{"message":"Unsupported model parameter"}}""";
        using var responseHandler = new ResponseHandler(detailsString, statusInt: 400);
        using var httpClient = new HttpClient(responseHandler);
        using IChatClient iChatClient = new AiClientFactory().CreateClient(TestSettings(), httpClient);

        ClientResultException errorClientResultException = await Assert.ThrowsAsync<ClientResultException>(() =>
            iChatClient.GetResponseAsync("A fictional question."));

        Assert.Equal(400, errorClientResultException.Status);
        Assert.Equal(detailsString, errorClientResultException.GetRawResponse()?.Content.ToString());
        Assert.Contains("Unsupported model parameter", errorClientResultException.Message);
        Assert.Single(responseHandler.RequestBodyStrings);
    }

    // Cancellation must stop an in-flight request and prevent requests with an already canceled token.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Caller_cancellation_stops_requests(bool cancelBeforeRequestBool)
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        using var responseHandler = new ResponseHandler(beforeResponseAction: cancellationTokenSource.Cancel);
        using var httpClient = new HttpClient(responseHandler);
        using IChatClient iChatClient = new AiClientFactory().CreateClient(TestSettings(), httpClient);
        if (cancelBeforeRequestBool)
        {
            cancellationTokenSource.Cancel();
        }

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            iChatClient.GetResponseAsync("A fictional question.", cancellationToken: cancellationTokenSource.Token));

        Assert.Equal(cancelBeforeRequestBool ? 0 : 1, responseHandler.RequestBodyStrings.Count);
    }

    // Both strategies must send Microsoft embedding requests through their selected endpoints.
    [Theory]
    [InlineData("openrouter", "OPENROUTER", "https://openrouter.ai/api/v1/embeddings")]
    [InlineData("gemini", "GEMINI", "https://generativelanguage.googleapis.com/v1beta/openai/embeddings")]
    public async Task Embedding_requests_use_selected_provider_model_and_native_usage(string providerString, string prefixString, string endpointString)
    {
        ProviderSettings providerSettings = SettingsReader.ParseSettings(
            [$"AI_PROVIDER={providerString}", $"{prefixString}_API_KEY=test-key", $"{prefixString}_EMBEDDING_MODEL=embedding-model"],
            useEmbeddingModelBool: true);
        const string responseBodyString = """
            {"object":"list","model":"embedding-model","data":[
              {"object":"embedding","index":0,"embedding":[1.0,0.0]},
              {"object":"embedding","index":1,"embedding":[0.0,1.0]}],
             "usage":{"prompt_tokens":12,"total_tokens":12}}
            """;
        using var responseHandler = new ResponseHandler(responseBodyString);
        using var httpClient = new HttpClient(responseHandler);
        using IEmbeddingGenerator<string, Embedding<float>> iEmbeddingGenerator = new AiClientFactory().CreateEmbeddingGenerator(providerSettings, httpClient);
        using var cancellationTokenSource = new CancellationTokenSource();

        GeneratedEmbeddings<Embedding<float>> generatedEmbeddings = await iEmbeddingGenerator.GenerateAsync(
            ["Docker services", "Garden budget"], cancellationToken: cancellationTokenSource.Token);

        Assert.Equal(new Uri(endpointString), Assert.Single(responseHandler.RequestUris));
        using JsonDocument jsonDocument = JsonDocument.Parse(Assert.Single(responseHandler.RequestBodyStrings));
        Assert.Equal("embedding-model", jsonDocument.RootElement.GetProperty("model").GetString());
        Assert.Equal(new[] { "Docker services", "Garden budget" },
            jsonDocument.RootElement.GetProperty("input").EnumerateArray().Select(valueJsonElement => valueJsonElement.GetString()));
        Assert.True(responseHandler.HadCancelableTokenBool);
        Assert.Equal(2, generatedEmbeddings.Count);
        Assert.All(generatedEmbeddings, embedding => Assert.Equal("embedding-model", embedding.ModelId));
        Assert.Equal(12, generatedEmbeddings.Usage?.InputTokenCount);
        Assert.Equal(12, generatedEmbeddings.Usage?.TotalTokenCount);
    }

    // The common interface must preserve native API errors and avoid retrying permanent failures.
    [Fact]
    public async Task Embedding_errors_preserve_native_provider_details()
    {
        using var responseHandler = new ResponseHandler("""{"error":{"message":"Embedding model is unavailable"}}""", statusInt: 404);
        using var httpClient = new HttpClient(responseHandler);
        using IEmbeddingGenerator<string, Embedding<float>> iEmbeddingGenerator = new AiClientFactory().CreateEmbeddingGenerator(TestSettings(), httpClient);

        ClientResultException errorClientResultException = await Assert.ThrowsAsync<ClientResultException>(async () =>
            await iEmbeddingGenerator.GenerateAsync(["Docker service"]));

        Assert.Equal(404, errorClientResultException.Status);
        Assert.Contains("Embedding model is unavailable", errorClientResultException.Message);
        Assert.Single(responseHandler.RequestBodyStrings);
    }

    // Restore Gemini's omitted zero before ordering and converting vectors into Microsoft's embedding values.
    [Fact]
    public async Task Gemini_restores_omitted_zero_before_returning_embeddings_in_input_order()
    {
        const string responseBodyString = """
            {"object":"list","model":"test-model","data":[
              {"object":"embedding","index":1,"embedding":[0.0,1.0]},
              {"object":"embedding","embedding":[1.0,0.0]}],
             "usage":{"prompt_tokens":12,"total_tokens":12}}
            """;
        using var responseHandler = new ResponseHandler(responseBodyString);
        using var httpClient = new HttpClient(responseHandler);
        using IEmbeddingGenerator<string, Embedding<float>> iEmbeddingGenerator = new AiClientFactory().CreateEmbeddingGenerator(TestSettings("Gemini"), httpClient);

        GeneratedEmbeddings<Embedding<float>> generatedEmbeddings = await iEmbeddingGenerator.GenerateAsync(["First", "Second"]);

        Assert.Equal(new[] { 1f, 0f }, generatedEmbeddings[0].Vector.ToArray());
        Assert.Equal(new[] { 0f, 1f }, generatedEmbeddings[1].Vector.ToArray());
        Assert.All(generatedEmbeddings, embedding => Assert.Equal("test-model", embedding.ModelId));
    }

    // Multiple omissions are ambiguous and must be rejected before Microsoft's adapter drops the indexes.
    [Fact]
    public async Task Gemini_does_not_repair_ambiguous_embedding_indexes()
    {
        const string responseBodyString = """
            {"object":"list","model":"test-model","data":[
              {"object":"embedding","embedding":[0.0,1.0]},
              {"object":"embedding","embedding":[1.0,0.0]}],
             "usage":{"prompt_tokens":12,"total_tokens":12}}
            """;
        using var responseHandler = new ResponseHandler(responseBodyString);
        using var httpClient = new HttpClient(responseHandler);
        using IEmbeddingGenerator<string, Embedding<float>> iEmbeddingGenerator = new AiClientFactory().CreateEmbeddingGenerator(TestSettings("Gemini"), httpClient);

        InvalidDataException errorInvalidDataException = await Assert.ThrowsAsync<InvalidDataException>(async () =>
            await iEmbeddingGenerator.GenerateAsync(["First", "Second"]));

        Assert.Contains("indexes", errorInvalidDataException.Message);
        Assert.Single(responseHandler.RequestBodyStrings);
    }

    // These fixtures use no real credentials or provider connections.
    private static ProviderSettings TestSettings(string providerString = "OpenRouter")
    {
        return new(providerString, "test-key", "test-model", new Uri("https://example.invalid/v1"));
    }

    private static ChatOptions CreateOptions()
    {
        return new()
        {
            Instructions = "Use only the fictional source facts.",
            Temperature = 0.2f,
            MaxOutputTokens = 2048
        };
    }

    // The SDK can encode message text as one string or a list of content parts.
    private static string ReadMessageText(JsonElement messageJsonElement)
    {
        JsonElement contentJsonElement = messageJsonElement.GetProperty("content");
        return contentJsonElement.ValueKind == JsonValueKind.String ? contentJsonElement.GetString()! :
            string.Concat(contentJsonElement.EnumerateArray().Select(partJsonElement => partJsonElement.GetProperty("text").GetString()));
    }

    // Replace only HTTP; the real SDK and framework still serialize requests and invoke tools.
    private sealed class ResponseHandler(string responseBodyString = SuccessfulResponseString, int statusInt = 200,
        Action? beforeResponseAction = null, List<string>? responseBodyStrings = null) : HttpMessageHandler
    {
        private readonly Queue<string> _responseStringsQueue = new(responseBodyStrings ?? [responseBodyString]);
        public List<Uri> RequestUris { get; } = [];
        public List<string> RequestBodyStrings { get; } = [];
        public bool HadCancelableTokenBool
        {
            get; private set;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage requestHttpRequestMessage, CancellationToken cancellationToken)
        {
            RequestUris.Add(requestHttpRequestMessage.RequestUri!);
            RequestBodyStrings.Add(await requestHttpRequestMessage.Content!.ReadAsStringAsync(cancellationToken));
            HadCancelableTokenBool = cancellationToken.CanBeCanceled;
            beforeResponseAction?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();
            return new HttpResponseMessage((HttpStatusCode)statusInt)
            {
                Content = new StringContent(
                    _responseStringsQueue.Count > 1 ? _responseStringsQueue.Dequeue() : _responseStringsQueue.Peek(), Encoding.UTF8,
                    "application/json")
            };
        }
    }
}
