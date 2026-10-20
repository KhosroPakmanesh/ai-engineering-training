using System.Text.Json;
using System.Text.Json.Serialization;

public static class ExperienceInputsLoader
{
    // Read the same fictional jobs in console runs, Visual Studio, and tests.
    public static async Task<ExperienceInputs> LoadAsync(string question, string? evaluationDate,
        CancellationToken cancellationToken = default)
    {
        string jsonJobs = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "SampleData", "jobs.json"), cancellationToken);
        JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web)
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        List<WorkInterval> intervals = JsonSerializer.Deserialize<List<WorkInterval>>(jsonJobs, jsonOptions)
            ?? throw new InvalidDataException("The jobs file must contain work intervals.");
        ExperienceInputs inputs = new(question, intervals, evaluationDate);

        return inputs;
    }
}
