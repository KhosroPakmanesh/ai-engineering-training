internal static class ProfileTestData
{
    // Each test receives fresh records, so changes to a fixture cannot affect another test.
    public static CandidateProfile ReadExample() =>
        ProfileExtractor.ExtractProfile(ReadSample("profile.example.json"));

    // Read the same copied teaching inputs as the console application.
    public static string ReadSample(string filename) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "SampleData", filename));
}
