internal static class ProfileTestData
{
    // Give each test its own profile so changing it cannot affect another test.
    public static CandidateProfile ReadExample() =>
        ProfileExtractor.ExtractProfile(ReadSample("profile.example.json"));

    // Read the same copied teaching inputs as the console application.
    public static string ReadSample(string filename) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "SampleData", filename));
}
