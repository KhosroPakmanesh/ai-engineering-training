using System.Text.Json;

internal static class ProfileTestData
{
    // Give each test its own profile so changing it cannot affect another test.
    public static CandidateProfile ReadExample()
    {
        return JsonSerializer.Deserialize<CandidateProfile>(ReadSample("profile.example.json"), JsonSerializerOptions.Web)!;
    }

    // Read the same copied teaching inputs as the console application.
    public static string ReadSample(string filenameString)
    {
        return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "SampleData", filenameString));
    }
}
