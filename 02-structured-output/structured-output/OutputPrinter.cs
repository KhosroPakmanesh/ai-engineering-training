using System.Text.Json;
using static ConsoleOutput;

public static class OutputPrinter
{
    // Explain why a fixed JSON shape and source evidence are separate parts of this lesson.
    public static void PrintIntroduction(string resumeFileNameString)
    {
        PrintHeading("Structured Output: turn a resume into checked JSON");
        Console.WriteLine($"Source: SampleData/{resumeFileNameString}\n" +
            "The model proposes a profile with fixed fields. Unknown facts should stay null.\n" +
            "Local stages check the JSON schema, read the framework's typed profile, and check exact resume quotes.\n" +
            "Only then are skill names trimmed and duplicates removed.");
    }

    // Keep provider details beside the request without moving settings or AI setup into the printer.
    public static void PrintRequestDetails(ProviderSettings providerSettings)
    {
        PrintColoredLine(
            $"Model: {providerSettings.ModelString}. Sending the fictional CV to {providerSettings.NameString}" +
            $" (one model call, up to {AiClientFactory.MaxRetries} retries).",
            ConsoleColor.Gray);
    }

    // Call this after validation and normalization; serialization changes only the display format.
    public static void PrintProfile(CandidateProfile candidateProfile)
    {
        PrintHeading("\nAccepted profile");
        PrintColoredLine(
            "── Profile 1 ──────────────────────────────",
            ConsoleColor.White);
        Console.WriteLine("Schema and evidence checks: passed; skill normalization: complete.");
        JsonSerializerOptions jsonSerializerOptions = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };
        string formattedJsonString = JsonSerializer.Serialize(
            candidateProfile,
            jsonSerializerOptions);
        Console.WriteLine(formattedJsonString);
    }

    // Passing structural and quote checks still leaves meaning for the learner to judge.
    public static void PrintTakeaway()
    {
        PrintHeading("\nWhat to take away");
        Console.WriteLine(
            "A JSON schema checks field names and value types. Evidence checks link facts to exact resume quotes.\n" +
            "Read the quotes yourself: mentioning a qualification does not prove the candidate earned it.\n" +
            "Change resumeFileNameString in Program.cs to resume-ambiguous.md and run again.\n" +
            "Inspect null fields, date precision, and duplicate skills in the printed JSON.");
    }
}
