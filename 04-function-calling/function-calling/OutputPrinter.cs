using static ConsoleOutput;

public static class OutputPrinter
{
    // Separate the model's choice of a tool from the C# calculation it asks the app to perform.
    public static void PrintIntroduction(ExperienceInputs inputs)
    {
        PrintHeading("Function Calling: let C# calculate the answer");
        Console.WriteLine(
            $"Question: {inputs.Question}\n" +
            $"Source: SampleData/jobs.json; evaluation date: {inputs.EvaluationDate}\n" +
            "The first model call selects CalculateExperience with the supplied dates.\n" +
            "C# checks those arguments, merges overlapping or touching jobs, and counts full calendar months.\n" +
            "The second model call receives the tool result and must report its total with the required citation.");
    }

    // Distinguish model turns from retries before starting the complete tool conversation.
    public static void PrintRequestDetails(ProviderSettings settings) =>
        PrintColoredLine(
            $"Model: {settings.Model}. Sending fictional work dates to {settings.Name} " +
            $"(up to {ExperienceAssistant.MaxModelCalls} model calls, up to {AiClientFactory.MaxRetries} HTTP retries per call).",
            ConsoleColor.Gray);

    // Preserve the redacted execution log even when source checks or calculation fail.
    public static void PrintToolExecution(string toolName, long durationMilliseconds, bool succeeded) =>
        PrintColoredLine(
            $"tool_name={toolName} inputs=[redacted] duration_ms={durationMilliseconds} succeeded={succeeded}",
            ConsoleColor.Gray);

    // Call this only after execution and answer checks; the printer does not establish acceptance.
    public static void PrintResults(ParsedExperienceResponse parsedResponse, long requestMilliseconds)
    {
        ExperienceResult result = parsedResponse.Result;
        PrintHeading("\nAccepted tool answer");
        Console.WriteLine($"Input intervals: {result.InputIntervalCount}; merged intervals: {result.MergedIntervals.Count}");
        Console.WriteLine($"Model calls: {parsedResponse.ModelCallCount}; tool executions: {parsedResponse.ToolExecutionCount}");
        Console.WriteLine($"Request time: {requestMilliseconds} ms");
        Console.WriteLine(parsedResponse.Answer);
    }

    // Explain the arithmetic's boundary so a matching total is not mistaken for proof of employment.
    public static void PrintTakeaway()
    {
        PrintHeading("\nWhat to take away");
        Console.WriteLine(
            "The model chooses the function; C# owns the arithmetic. Overlapping work counts only once.\n" +
            "End dates are exclusive, and only fully covered calendar months count.\n" +
            "The checks confirm one successful tool execution and an answer matching its total and citation.\n" +
            "They do not establish that the jobs are true or relevant to a real hiring decision.\n" +
            "Request time includes setup, model turns, local calculation, and retry waits. Tool duration is local work.\n" +
            "Change one date in SampleData/jobs.json, rebuild, and predict how the merged coverage changes.");
    }
}
