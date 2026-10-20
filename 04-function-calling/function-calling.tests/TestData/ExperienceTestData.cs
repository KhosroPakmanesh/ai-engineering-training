using System.Net;
using System.Text;
using System.Text.Json;

internal static class ExperienceTestData
{
    // Each test gets fresh intervals from the same copied jobs used by the console application.
    public static Task<ExperienceInputs> ReadInputsAsync() => ExperienceInputsLoader.LoadAsync(
        "How many full calendar months of backend experience do these fictional jobs cover?", "2023-04-01");

    // Keep only the transport fake; Agent Framework still selects and executes the registered C# tool.
    public static ExperienceResponseHandler CreateHandler(ExperienceInputs inputs, string answer =
        "36 full calendar months [CalculateExperience.totalMonths].")
    {
        string arguments = JsonSerializer.Serialize(new { intervals = inputs.Intervals, evaluationDate = inputs.EvaluationDate },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return new(CreateResponse(null, arguments), CreateResponse(answer));
    }

    // Return the same Chat Completions shape as a provider, including genuine function-call content.
    public static string CreateResponse(string? answer, string? arguments = null) => JsonSerializer.Serialize(new
    {
        id = "experience-test", @object = "chat.completion", created = 0, model = "test-model",
        choices = new[] { new { index = 0, message = new
        {
            role = "assistant", content = answer,
            tool_calls = arguments is null ? null : new[] { new
            {
                id = "experience-call", type = "function", function = new { name = "CalculateExperience", arguments }
            } }
        }, finish_reason = arguments is null ? "stop" : "tool_calls" } }
    });
}

internal sealed class ExperienceResponseHandler(params string[] responses) : HttpMessageHandler
{
    public List<string> RequestBodies { get; } = [];

    // Unexpected extra requests fail locally instead of concealing an unbounded agent loop.
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
        if (RequestBodies.Count > responses.Length)
            throw new InvalidOperationException("The test did not provide another model response.");
        return new(HttpStatusCode.OK)
        {
            Content = new StringContent(responses[RequestBodies.Count - 1], Encoding.UTF8, "application/json")
        };
    }
}
