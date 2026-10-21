using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Xunit;
using Xunit.Abstractions;

public class ExperienceAssistantTests(ITestOutputHelper output)
{
    // The real agent processes a provider tool call, executes C#, and sends its result back to the model.
    [Fact]
    public async Task Agent_executes_the_calculator_and_grounds_its_final_answer()
    {
        ExperienceInputs inputs = await ExperienceTestData.ReadInputsAsync();
        ExperienceInputsValidator.ValidateInputs(inputs);
        using ExperienceResponseHandler handler = ExperienceTestData.CreateHandler(inputs);
        using var httpClient = new HttpClient(handler);
        AgentResponse response = await ExperienceAssistant.AskAsync(inputs, TestSettings(), httpClient);

        ToolExecutionValidator.ValidateToolExecution(response);
        ParsedExperienceResponse parsedResponse = ExperienceResponseParser.Parse(response);
        ExperienceResult result = parsedResponse.Result;
        ExperienceResultValidator.ValidateAnswer(parsedResponse.Answer, result);
        Assert.Equal(36, result.TotalMonths);
        Assert.Equal(2, parsedResponse.ModelCallCount);
        Assert.Equal(1, parsedResponse.ToolExecutionCount);
        Assert.Equal(2, handler.RequestBodies.Count);
        using JsonDocument secondRequest = JsonDocument.Parse(handler.RequestBodies[1]);
        JsonElement toolMessage = secondRequest.RootElement.GetProperty("messages").EnumerateArray()
            .Single(message => message.GetProperty("role").GetString() == "tool");
        Assert.Contains("36", toolMessage.GetProperty("content").GetString());
        using JsonDocument firstRequest = JsonDocument.Parse(handler.RequestBodies[0]);
        Assert.Contains(inputs.Question, firstRequest.RootElement.GetProperty("messages")[1].GetProperty("content").GetString());
    }

    // The framework's iteration setting stops a model that keeps requesting the tool.
    [Fact]
    public async Task Framework_stops_after_two_model_calls()
    {
        ExperienceInputs inputs = await ExperienceTestData.ReadInputsAsync();
        ExperienceInputsValidator.ValidateInputs(inputs);
        string arguments = JsonSerializer.Serialize(new { intervals = inputs.Intervals, evaluationDate = inputs.EvaluationDate },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        string toolResponse = ExperienceTestData.CreateResponse(null, arguments);
        // Keep final text usable so this case checks the loop limit independently of response checks.
        using var handler = new ExperienceResponseHandler(toolResponse,
            ExperienceTestData.CreateResponse("36 full calendar months [CalculateExperience.totalMonths].", arguments));
        using var httpClient = new HttpClient(handler);

        AgentResponse response = await ExperienceAssistant.AskAsync(inputs, TestSettings(), httpClient);

        Assert.Equal(2, handler.RequestBodies.Count);
        Assert.Single(response.Messages.SelectMany(message => message.Contents).OfType<FunctionResultContent>());
    }

    // Earlier assistant text must not hide a missing or cut-short final answer.
    [Theory]
    [InlineData("length", "36 full calendar months [CalculateExperience.totalMonths].", "truncated")]
    [InlineData("stop", null, "no response text")]
    public async Task Unusable_final_answers_are_rejected_before_returning(
        string finishReason, string? answer, string expectedError)
    {
        ExperienceInputs inputs = await ExperienceTestData.ReadInputsAsync();
        ExperienceInputsValidator.ValidateInputs(inputs);
        string arguments = JsonSerializer.Serialize(new { intervals = inputs.Intervals, evaluationDate = inputs.EvaluationDate },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        JsonNode finalResponse = JsonNode.Parse(ExperienceTestData.CreateResponse(answer))!;
        finalResponse["choices"]![0]!["finish_reason"] = finishReason;
        using var handler = new ExperienceResponseHandler(
            ExperienceTestData.CreateResponse("I will use the calculator.", arguments), finalResponse.ToJsonString());
        using var httpClient = new HttpClient(handler);

        InvalidDataException error = await Assert.ThrowsAsync<InvalidDataException>(() =>
            ExperienceAssistant.AskAsync(inputs, TestSettings(), httpClient));

        Assert.Contains(expectedError, error.Message);
        Assert.Equal(2, handler.RequestBodies.Count);
    }

    // Dates that look valid must still be rejected when the model changes, drops, or invents source inputs.
    [Theory]
    [InlineData("date")]
    [InlineData("reference")]
    [InlineData("omission")]
    public async Task Changed_tool_inputs_stop_before_another_model_request(string change)
    {
        ExperienceInputs inputs = await ExperienceTestData.ReadInputsAsync();
        ExperienceInputsValidator.ValidateInputs(inputs);
        List<WorkInterval> changedIntervals = inputs.Intervals.ToList();
        string? changedReference = inputs.EvaluationDate;
        if (change == "date") changedIntervals[0] = changedIntervals[0] with { StartDate = "2010-01-01" };
        if (change == "reference") changedReference = "2024-04-01";
        if (change == "omission") changedIntervals.RemoveAt(0);
        string arguments = JsonSerializer.Serialize(new { intervals = changedIntervals, evaluationDate = changedReference },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var handler = new ExperienceResponseHandler(ExperienceTestData.CreateResponse(null, arguments));
        using var httpClient = new HttpClient(handler);

        await Assert.ThrowsAsync<InvalidDataException>(() => ExperienceAssistant.AskAsync(inputs, TestSettings(), httpClient));
        Assert.Single(handler.RequestBodies);
    }

    // A provider can answer without calling a function; the acceptance stage must detect that explicitly.
    [Fact]
    public async Task A_fabricated_answer_without_tool_use_cannot_pass_the_pipeline()
    {
        ExperienceInputs inputs = await ExperienceTestData.ReadInputsAsync();
        ExperienceInputsValidator.ValidateInputs(inputs);
        using var handler = new ExperienceResponseHandler(ExperienceTestData.CreateResponse("36 full calendar months [CalculateExperience.totalMonths]."));
        using var httpClient = new HttpClient(handler);
        AgentResponse response = await ExperienceAssistant.AskAsync(inputs, TestSettings(), httpClient);
        Assert.Throws<InvalidDataException>(() => ToolExecutionValidator.ValidateToolExecution(response));
        Assert.Single(handler.RequestBodies);
    }

    // Follow Program.cs: explicit input validation must stop the request before the transport receives anything.
    [Theory]
    [InlineData(null)]
    [InlineData("invalid")]
    public async Task An_unresolved_current_role_prevents_paid_requests(string? evaluationDate)
    {
        ExperienceInputs inputs = await ExperienceTestData.ReadInputsAsync();
        using var handler = new ExperienceResponseHandler();
        using var httpClient = new HttpClient(handler);
        ExperienceInputs invalidInputs = inputs with { EvaluationDate = evaluationDate };
        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            ExperienceInputsValidator.ValidateInputs(invalidInputs);
            await ExperienceAssistant.AskAsync(invalidInputs, TestSettings(), httpClient);
        });
        Assert.Empty(handler.RequestBodies);
    }

    // This is the same full acceptance sequence as Program.cs, using real shared settings and copied jobs.
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Selected_provider_uses_the_real_calculator_and_returns_its_total()
    {
        ExperienceInputs inputs = await ExperienceTestData.ReadInputsAsync();
        ExperienceInputsValidator.ValidateInputs(inputs);
        AgentResponse response = await ExperienceAssistant.AskAsync(inputs);
        ToolExecutionValidator.ValidateToolExecution(response);
        ParsedExperienceResponse parsedResponse = ExperienceResponseParser.Parse(response);
        ExperienceResult result = parsedResponse.Result;
        ExperienceResultValidator.ValidateAnswer(parsedResponse.Answer, result);
        output.WriteLine(parsedResponse.Answer);
        output.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
        Assert.Equal(36, result.TotalMonths);
        Assert.Equal(2, result.MergedIntervals.Count);
        Assert.Equal(2, parsedResponse.ModelCallCount);
        Assert.Equal(1, parsedResponse.ToolExecutionCount);
    }

    // Made-up credentials are sufficient when only the HTTP transport is replaced.
    private static ProviderSettings TestSettings() => new("OpenRouter", "test-key", "test-model", new Uri("https://example.invalid/v1"));
}
