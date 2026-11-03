using System.Net;
using System.Text;
using System.Text.Json;

internal static class ExperienceTestData
{
    // Each test gets fresh intervals from the same copied jobs used by the console application.
    public static Task<ExperienceInputs> ReadInputsAsync()
    {
        return ExperienceInputsLoader.LoadAsync("How many full calendar months of backend experience do these fictional jobs cover?",
            "2023-04-01");
    }

    // Keep only the transport fake; Agent Framework still selects and executes the registered C# tool.
    public static ExperienceResponseHandler CreateHandler(
        ExperienceInputs experienceInputs,
        string answerString =
        "36 full calendar months [CalculateExperience.totalMonths].")
    {
        string argumentsString = JsonSerializer.Serialize(
            new
            {
                intervals = experienceInputs.WorkIntervals,
                evaluationDate = experienceInputs.EvaluationDateString
            },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return new(CreateResponse(null, argumentsString), CreateResponse(answerString));
    }

    // Return the same Chat Completions shape as a provider, including genuine function-call content.
    public static string CreateResponse(string? answerString, string? argumentsString = null)
    {
        return JsonSerializer.Serialize(new
        {
            id = "experience-test",
            @object = "chat.completion",
            created = 0,
            model = "test-model",
            choices = new[] { new { index = 0, message = new
        {
            role = "assistant", content = answerString,
            tool_calls = argumentsString is null ? null : new[] { new
            {
                id = "experience-call", type = "function", function = new { name = "CalculateExperience", arguments = argumentsString }
            } }
        }, finish_reason = argumentsString is null ? "stop" : "tool_calls" } }
        });
    }
}

internal sealed class ExperienceResponseHandler(params string[] responseStrings) : HttpMessageHandler
{
    public List<string> RequestBodyStrings { get; } = [];
    public Action? BeforeResponseAction
    {
        get; init;
    }

    // Unexpected extra requests fail locally instead of concealing an unbounded agent loop.
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage requestHttpRequestMessage, CancellationToken cancellationToken)
    {
        RequestBodyStrings.Add(await requestHttpRequestMessage.Content!.ReadAsStringAsync(cancellationToken));
        BeforeResponseAction?.Invoke();
        cancellationToken.ThrowIfCancellationRequested();
        if (RequestBodyStrings.Count > responseStrings.Length)
        {
            throw new InvalidOperationException("The test did not provide another model response.");
        }

        return new(HttpStatusCode.OK)
        {
            Content = new StringContent(responseStrings[RequestBodyStrings.Count - 1], Encoding.UTF8, "application/json")
        };
    }
}
