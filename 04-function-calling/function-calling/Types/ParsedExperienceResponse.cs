// Keep the parsed answer, calculation, and display counts together for the acceptance stages.
public sealed record ParsedExperienceResponse(string Answer, ExperienceResult Result,
    int ModelCallCount, int ToolExecutionCount);
