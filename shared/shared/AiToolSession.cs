using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.AI;

// Each facade request gets its own tools, counters, and execution results.
internal sealed class AiToolSession(AiToolCalling configuration)
{
    private readonly AiToolCalling _configuration = configuration;
    public int ModelCalls { get; private set; }
    public List<AiToolExecution> Executions { get; } = [];
    public string? FinalText { get; private set; }

    // Let the SDK build argument schemas from ordinary C# parameter types.
    public List<AITool> CreateTools()
    {
        if (_configuration.MaxModelCalls < 2 || _configuration.MaxToolCalls < 1 || _configuration.Tools.Count == 0)
            throw new ArgumentException("Tool calling needs tools, at least two model calls, and a positive tool limit.");
        if (_configuration.Tools.Select(tool => tool.Name).Distinct(StringComparer.Ordinal).Count() != _configuration.Tools.Count)
            throw new ArgumentException("Tool names must be unique.");

        return _configuration.Tools.Select(tool => (AITool)new TrackedFunction(
            AIFunctionFactory.Create(tool.Function, new AIFunctionFactoryOptions
            {
                Name = tool.Name,
                Description = tool.Description,
                SerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
                {
                    TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver(),
                    UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow
                }
            }), this)).ToList();
    }

    // Use the real function loop, with one deadline supplied by AiProvider for the entire run.
    public IChatClient CreateChatClient(IChatClient client) => new FunctionInvokingChatClient(new CountedChatClient(client, this))
    {
        MaximumIterationsPerRequest = _configuration.MaxModelCalls,
        MaximumConsecutiveErrorsPerRequest = 0,
        AllowConcurrentInvocation = false,
        TerminateOnUnknownCalls = true
    };

    // Reject an unusable batch before executing any of its functions or requesting another turn.
    private void CheckToolCalls(ChatResponse response)
    {
        if (response.FinishReason == ChatFinishReason.Length)
            throw new InvalidDataException("The provider truncated its response at the output token limit.");

        List<FunctionCallContent> calls = response.Messages.SelectMany(message => message.Contents)
            .OfType<FunctionCallContent>().ToList();
        if (calls.Count == 0) return;
        if (ModelCalls >= _configuration.MaxModelCalls)
            throw new InvalidDataException("The model requested another tool after the model-call budget was exhausted.");
        if (calls.Count > _configuration.MaxToolCalls - Executions.Count)
            throw new InvalidDataException("The model exceeded the local tool-call budget.");
        foreach (FunctionCallContent call in calls)
        {
            if (call.Exception is not null || call.Arguments is null)
                throw new InvalidDataException("The model returned malformed tool arguments.");
            if (!_configuration.Tools.Any(tool => tool.Name == call.Name))
                throw new InvalidDataException("The model requested an unknown tool.");
        }
    }

    // Count actual model turns beneath the function loop, rather than counting facade calls.
    private sealed class CountedChatClient(IChatClient innerClient, AiToolSession session) : DelegatingChatClient(innerClient)
    {
        public override async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            if (session.ModelCalls >= session._configuration.MaxModelCalls)
                throw new InvalidDataException("The model-call budget was exhausted.");
            session.ModelCalls++;
            ChatResponse response = await base.GetResponseAsync(messages, options, cancellationToken);
            session.CheckToolCalls(response);
            // Keep the provider's extra tool data so Gemini can recognize the next turn.
            if (response.RawRepresentation is OpenAI.Chat.ChatCompletion completion && completion.ToolCalls.Count > 0)
            {
                foreach (ChatMessage message in response.Messages.Where(message => message.Role == ChatRole.Assistant))
                    message.RawRepresentation = new OpenAI.Chat.AssistantChatMessage(completion);
            }
            // The function loop later appends earlier messages to this response object.
            session.FinalText = response.Text;
            return response;
        }
    }

    // Record only real executions; never log the model's arguments or an exception's private contents.
    private sealed class TrackedFunction(AIFunction innerFunction, AiToolSession session) : DelegatingAIFunction(innerFunction)
    {
        protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments,
            CancellationToken cancellationToken)
        {
            var timer = Stopwatch.StartNew();
            bool succeeded = false;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                object? result = await base.InvokeCoreAsync(arguments, cancellationToken);
                JsonElement jsonResult = JsonSerializer.SerializeToElement(result);
                session.Executions.Add(new(Name, jsonResult, timer.ElapsedMilliseconds));
                succeeded = true;
                return result;
            }
            finally
            {
                Console.WriteLine($"tool_name={Name} inputs=[redacted] duration_ms={timer.ElapsedMilliseconds} succeeded={succeeded}");
            }
        }
    }
}
