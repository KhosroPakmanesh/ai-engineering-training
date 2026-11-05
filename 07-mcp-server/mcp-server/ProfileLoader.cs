using System.Text.Json;

namespace ResumeMcpServer;

public static class ProfileLoader
{
    // Read the copied source; validation is a separate step in Program.cs.
    public static async Task<List<ResumeProfile>> LoadAsync(string pathString)
    {
        string jsonString = await File.ReadAllTextAsync(pathString);
        return JsonSerializer.Deserialize<List<ResumeProfile>>(jsonString)
            ?? throw new InvalidDataException("The profile file must contain a JSON list.");
    }
}
