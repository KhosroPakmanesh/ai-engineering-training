using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Xunit;
using Xunit.Abstractions;

public class ExperienceAssistantTests(ITestOutputHelper outputITestOutputHelper)
{
    // The complete tool conversation uses one caller token, including the first HTTP boundary.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Caller_cancellation_stops_the_tool_conversation(bool cancelBeforeBool)
    {
        ExperienceInputs experienceInputs = await ExperienceTestData.ReadInputsAsync();
        ExperienceInputsValidator.ValidateInputs(experienceInputs);
        using var cancellationTokenSource = new CancellationTokenSource();
        using var cancelingExperienceResponseHandler = new ExperienceResponseHandler(ExperienceTestData.CreateResponse("Unused answer."))
        {
            BeforeResponseAction = cancellationTokenSource.Cancel
        };
        using var httpClient = new HttpClient(cancelingExperienceResponseHandler);
        if (cancelBeforeBool)
        {
            cancellationTokenSource.Cancel();
        }

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ExperienceAssistant.AskAsync(experienceInputs, TestSettings(), httpClient, cancellationTokenSource.Token));
        Assert.Equal(cancelBeforeBool ? 0 : 1, cancelingExperienceResponseHandler.RequestBodyStrings.Count);
    }

    // A single provider response can request two calls despite the request's one-tool preference.
    [Fact]
    public async Task A_second_tool_invocation_is_rejected_before_follow_up_requests()
    {
        ExperienceInputs experienceInputs = await ExperienceTestData.ReadInputsAsync();
        ExperienceInputsValidator.ValidateInputs(experienceInputs);
        string argumentsString = JsonSerializer.Serialize(
            new
            {
                intervals = experienceInputs.WorkIntervals,
                evaluationDate = experienceInputs.EvaluationDateString
            },
            JsonSerializerOptions.Web);
        JsonNode responseJsonNode = JsonNode.Parse(ExperienceTestData.CreateResponse(null, argumentsString))!;
        JsonArray callsJsonArray = responseJsonNode["choices"]![0]!["message"]!["tool_calls"]!.AsArray();
        JsonNode secondCallJsonNode = callsJsonArray[0]!.DeepClone();
        secondCallJsonNode["id"] = "second-experience-call";
        callsJsonArray.Add(secondCallJsonNode);
        using var experienceResponseHandler = new ExperienceResponseHandler(responseJsonNode.ToJsonString(),
            ExperienceTestData.CreateResponse("36 full calendar months [CalculateExperience.totalMonths]."));
        using var httpClient = new HttpClient(experienceResponseHandler);
        InvalidDataException invalidDataException = await Assert.ThrowsAsync<InvalidDataException>(() =>
            ExperienceAssistant.AskAsync(experienceInputs, TestSettings(), httpClient));
        Assert.Contains("at most once", invalidDataException.Message);
        Assert.Single(experienceResponseHandler.RequestBodyStrings);
    }

    // The real agent processes a provider tool call, executes C#, and sends its result back to the model.
    [Fact]
    public async Task Agent_executes_the_calculator_and_grounds_its_final_answer()
    {
        ExperienceInputs experienceInputs = await ExperienceTestData.ReadInputsAsync();
        ExperienceInputsValidator.ValidateInputs(experienceInputs);
        using ExperienceResponseHandler experienceResponseHandler = ExperienceTestData.CreateHandler(experienceInputs);
        using var httpClient = new HttpClient(experienceResponseHandler);
        AgentResponse agentResponse = await ExperienceAssistant.AskAsync(experienceInputs, TestSettings(), httpClient);

        ToolExecutionValidator.ValidateToolExecution(agentResponse);
        ParsedExperienceResponse parsedExperienceResponse = ExperienceResponseParser.Parse(agentResponse);
        ExperienceResult experienceResult = parsedExperienceResponse.ExperienceResult;
        ExperienceResultValidator.ValidateAnswer(parsedExperienceResponse.AnswerString, experienceResult);
        Assert.Equal(36, experienceResult.TotalMonthsInt);
        Assert.Equal(2, parsedExperienceResponse.ModelCallCountInt);
        Assert.Equal(1, parsedExperienceResponse.ToolExecutionCountInt);
        Assert.Equal(2, experienceResponseHandler.RequestBodyStrings.Count);
        using JsonDocument secondRequestJsonDocument = JsonDocument.Parse(experienceResponseHandler.RequestBodyStrings[1]);
        JsonElement toolMessageJsonElement = secondRequestJsonDocument.RootElement.GetProperty("messages").EnumerateArray()
            .Single(messageJsonElement => messageJsonElement.GetProperty("role").GetString() == "tool");
        Assert.Contains("36", toolMessageJsonElement.GetProperty("content").GetString());
        using JsonDocument firstRequestJsonDocument = JsonDocument.Parse(experienceResponseHandler.RequestBodyStrings[0]);
        Assert.Contains(experienceInputs.QuestionString,
            firstRequestJsonDocument.RootElement.GetProperty("messages")[1].GetProperty("content").GetString());
        JsonElement parametersJsonElement = firstRequestJsonDocument.RootElement.GetProperty("tools")[0]
            .GetProperty("function").GetProperty("parameters").GetProperty("properties");
        Assert.Equal(new[] { "intervals", "evaluationDate" },
            parametersJsonElement.EnumerateObject().Select(jsonProperty => jsonProperty.Name));
    }

    // The framework's iteration setting stops a model that keeps requesting the tool.
    [Fact]
    public async Task Framework_stops_after_two_model_calls()
    {
        ExperienceInputs experienceInputs = await ExperienceTestData.ReadInputsAsync();
        ExperienceInputsValidator.ValidateInputs(experienceInputs);
        string argumentsString = JsonSerializer.Serialize(
            new
            {
                intervals = experienceInputs.WorkIntervals,
                evaluationDate = experienceInputs.EvaluationDateString
            },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        string toolResponseString = ExperienceTestData.CreateResponse(null, argumentsString);
        // Keep final text usable so this case checks the loop limit independently of response checks.
        using var experienceResponseHandler = new ExperienceResponseHandler(toolResponseString,
            ExperienceTestData.CreateResponse("36 full calendar months [CalculateExperience.totalMonths].", argumentsString));
        using var httpClient = new HttpClient(experienceResponseHandler);

        AgentResponse agentResponse = await ExperienceAssistant.AskAsync(experienceInputs, TestSettings(), httpClient);

        Assert.Equal(2, experienceResponseHandler.RequestBodyStrings.Count);
        Assert.Single(agentResponse.Messages.SelectMany(chatMessage => chatMessage.Contents).OfType<FunctionResultContent>());
    }

    // Earlier assistant text must not hide a missing or cut-short final answer.
    [Theory]
    [InlineData("length", "36 full calendar months [CalculateExperience.totalMonths].", "truncated")]
    [InlineData("stop", null, "no response text")]
    public async Task Unusable_final_answers_are_rejected_before_returning(string finishReasonString, string? answerString,
        string expectedErrorString)
    {
        ExperienceInputs experienceInputs = await ExperienceTestData.ReadInputsAsync();
        ExperienceInputsValidator.ValidateInputs(experienceInputs);
        string argumentsString = JsonSerializer.Serialize(
            new
            {
                intervals = experienceInputs.WorkIntervals,
                evaluationDate = experienceInputs.EvaluationDateString
            },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        JsonNode finalResponseJsonNode = JsonNode.Parse(ExperienceTestData.CreateResponse(answerString))!;
        finalResponseJsonNode["choices"]![0]!["finish_reason"] = finishReasonString;
        using var experienceResponseHandler = new ExperienceResponseHandler(
            ExperienceTestData.CreateResponse("I will use the calculator.", argumentsString), finalResponseJsonNode.ToJsonString());
        using var httpClient = new HttpClient(experienceResponseHandler);

        InvalidDataException errorInvalidDataException = await Assert.ThrowsAsync<InvalidDataException>(() =>
            ExperienceAssistant.AskAsync(experienceInputs, TestSettings(), httpClient));

        Assert.Contains(expectedErrorString, errorInvalidDataException.Message);
        Assert.Equal(2, experienceResponseHandler.RequestBodyStrings.Count);
    }

    // Dates that look valid must still be rejected when the model changes, drops, or invents source inputs.
    [Theory]
    [InlineData("date")]
    [InlineData("reference")]
    [InlineData("omission")]
    public async Task Changed_tool_inputs_stop_before_another_model_request(string changeString)
    {
        ExperienceInputs experienceInputs = await ExperienceTestData.ReadInputsAsync();
        ExperienceInputsValidator.ValidateInputs(experienceInputs);
        List<WorkInterval> changedWorkIntervals = experienceInputs.WorkIntervals.ToList();
        string? changedReferenceString = experienceInputs.EvaluationDateString;
        if (changeString == "date")
        {
            changedWorkIntervals[0] = changedWorkIntervals[0] with
            {
                StartDateString = "2010-01-01"
            };
        }

        if (changeString == "reference")
        {
            changedReferenceString = "2024-04-01";
        }

        if (changeString == "omission")
        {
            changedWorkIntervals.RemoveAt(0);
        }

        string argumentsString = JsonSerializer.Serialize(
            new
            {
                intervals = changedWorkIntervals,
                evaluationDate = changedReferenceString
            },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var experienceResponseHandler = new ExperienceResponseHandler(ExperienceTestData.CreateResponse(null, argumentsString));
        using var httpClient = new HttpClient(experienceResponseHandler);

        await Assert.ThrowsAsync<InvalidDataException>(() => ExperienceAssistant.AskAsync(experienceInputs, TestSettings(), httpClient));
        Assert.Single(experienceResponseHandler.RequestBodyStrings);
    }

    // A provider can answer without calling a function; the acceptance stage must detect that explicitly.
    [Fact]
    public async Task A_fabricated_answer_without_tool_use_cannot_pass_the_pipeline()
    {
        ExperienceInputs experienceInputs = await ExperienceTestData.ReadInputsAsync();
        ExperienceInputsValidator.ValidateInputs(experienceInputs);
        using var experienceResponseHandler = new ExperienceResponseHandler(ExperienceTestData.CreateResponse("36 full calendar months [CalculateExperience.totalMonths]."));
        using var httpClient = new HttpClient(experienceResponseHandler);
        AgentResponse agentResponse = await ExperienceAssistant.AskAsync(experienceInputs, TestSettings(), httpClient);
        Assert.Throws<InvalidDataException>(() => ToolExecutionValidator.ValidateToolExecution(agentResponse));
        Assert.Single(experienceResponseHandler.RequestBodyStrings);
    }

    // Follow Program.cs: explicit input validation must stop the request before the transport receives anything.
    [Theory]
    [InlineData(null)]
    [InlineData("invalid")]
    public async Task An_unresolved_current_role_prevents_paid_requests(string? evaluationDateString)
    {
        ExperienceInputs experienceInputs = await ExperienceTestData.ReadInputsAsync();
        using var experienceResponseHandler = new ExperienceResponseHandler();
        using var httpClient = new HttpClient(experienceResponseHandler);
        ExperienceInputs invalidExperienceInputs = experienceInputs with
        {
            EvaluationDateString = evaluationDateString
        };
        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            ExperienceInputsValidator.ValidateInputs(invalidExperienceInputs);
            await ExperienceAssistant.AskAsync(invalidExperienceInputs, TestSettings(), httpClient);
        });
        Assert.Empty(experienceResponseHandler.RequestBodyStrings);
    }

    // This is the same full acceptance sequence as Program.cs, using real shared settings and copied jobs.
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Selected_provider_uses_the_real_calculator_and_returns_its_total()
    {
        ExperienceInputs experienceInputs = await ExperienceTestData.ReadInputsAsync();
        ExperienceInputsValidator.ValidateInputs(experienceInputs);
        AgentResponse agentResponse = await ExperienceAssistant.AskAsync(experienceInputs);
        ToolExecutionValidator.ValidateToolExecution(agentResponse);
        ParsedExperienceResponse parsedExperienceResponse = ExperienceResponseParser.Parse(agentResponse);
        ExperienceResult experienceResult = parsedExperienceResponse.ExperienceResult;
        ExperienceResultValidator.ValidateAnswer(parsedExperienceResponse.AnswerString, experienceResult);
        outputITestOutputHelper.WriteLine(parsedExperienceResponse.AnswerString);
        outputITestOutputHelper.WriteLine(JsonSerializer.Serialize(experienceResult, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
        Assert.Equal(36, experienceResult.TotalMonthsInt);
        Assert.Equal(2, experienceResult.MergedWorkIntervals.Count);
        Assert.Equal(2, parsedExperienceResponse.ModelCallCountInt);
        Assert.Equal(1, parsedExperienceResponse.ToolExecutionCountInt);
    }

    // Made-up credentials are sufficient when only the HTTP transport is replaced.
    private static ProviderSettings TestSettings()
    {
        return new("OpenRouter", "test-key", "test-model", new Uri("https://example.invalid/v1"));
    }
}
