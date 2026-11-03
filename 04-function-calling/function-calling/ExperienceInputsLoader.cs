using System.Text.Json;
using System.Text.Json.Serialization;

public static class ExperienceInputsLoader
{
    // Read the same fictional jobs in console runs, Visual Studio, and tests.
    public static async Task<ExperienceInputs> LoadAsync(string questionString, string? evaluationDateString,
        CancellationToken cancellationToken = default)
    {
        string jsonJobsString = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "SampleData", "jobs.json"),
            cancellationToken);
        JsonSerializerOptions jsonSerializerOptions = new(JsonSerializerDefaults.Web)
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        List<WorkInterval> workIntervals = JsonSerializer.Deserialize<List<WorkInterval>>(jsonJobsString, jsonSerializerOptions)
            ?? throw new InvalidDataException("The jobs file must contain work intervals.");
        ExperienceInputs experienceInputs = new(questionString, workIntervals, evaluationDateString);

        return experienceInputs;
    }
}
