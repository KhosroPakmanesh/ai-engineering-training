using static ConsoleOutput;

public static class OutputPrinter
{
    // Separate the model's choice of a tool from the C# calculation it asks the app to perform.
    public static void PrintIntroduction(ExperienceInputs experienceInputs)
    {
        PrintHeading("Function Calling: let C# calculate the answer");
        PrintColoredLine($"QUESTION | {experienceInputs.QuestionString}", ConsoleColor.Cyan);
        Console.WriteLine(
            $"Source: SampleData/jobs.json; evaluation date: {experienceInputs.EvaluationDateString}\n" +
            "The first model call selects CalculateExperience with the supplied dates.\n" +
            "C# checks those arguments, merges overlapping or touching jobs, and counts full calendar months.\n" +
            "The second model call receives the tool result and must report its total with the required citation.");
    }

    // Distinguish model turns from retries before starting the complete tool conversation.
    public static void PrintRequestDetails(ProviderSettings providerSettings)
    {
        PrintColoredLine(
            $"Model: {providerSettings.ModelString}. Sending fictional work dates to {providerSettings.NameString} " +
            $"(up to {ExperienceAssistant.MaxModelCallsInt} model calls, up to {AiClientFactory.MaxRetries} HTTP retries per call).",
            ConsoleColor.Gray);
    }

    // Preserve the redacted execution log even when source checks or calculation fail.
    public static void PrintToolExecution(string toolNameString, long durationMillisecondsLong, bool succeededBool)
    {
        PrintColoredLine($"tool_name={toolNameString} inputs=[redacted] duration_ms={durationMillisecondsLong} succeeded={succeededBool}",
            ConsoleColor.Gray);
    }

    // Call this only after execution and answer checks; the printer does not establish acceptance.
    public static void PrintResults(ParsedExperienceResponse parsedExperienceResponse, long requestMillisecondsLong)
    {
        ExperienceResult experienceResult = parsedExperienceResponse.ExperienceResult;
        PrintHeading("\nAccepted tool answer");
        PrintColoredLine("── Answer 1 ───────────────────────────────", ConsoleColor.White);
        Console.WriteLine($"Input intervals: {experienceResult.InputIntervalCount}; merged intervals: {experienceResult.MergedWorkIntervals.Count}");
        Console.WriteLine($"Model calls: {parsedExperienceResponse.ModelCallCountInt}; tool executions: {parsedExperienceResponse.ToolExecutionCountInt}");
        Console.WriteLine($"Request time: {requestMillisecondsLong} ms");
        Console.WriteLine(parsedExperienceResponse.AnswerString);
        Console.WriteLine("Accepted citations:");
        PrintColoredLine("  [CalculateExperience.totalMonths] -> the executed calculator's totalMonths result", ConsoleColor.Cyan);
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
