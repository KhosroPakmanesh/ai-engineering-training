using System.Text.Json.Serialization;

// The final answer stays beside its executed calculation and observed call counts.
public sealed record ParsedExperienceResponse(
    [property: JsonPropertyName("answer")] string AnswerString,
    [property: JsonPropertyName("result")] ExperienceResult ExperienceResult,
    [property: JsonPropertyName("modelCallCount")] int ModelCallCountInt,
    [property: JsonPropertyName("toolExecutionCount")] int ToolExecutionCountInt);
