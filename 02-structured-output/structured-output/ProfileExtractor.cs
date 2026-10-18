using System.Text.Json;

public static class ProfileExtractor
{
    // The schema-checked JSON becomes the records used by the evidence checks.
    public static CandidateProfile ExtractProfile(string jsonResume)
    {
        JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
        return JsonSerializer.Deserialize<CandidateProfile>(jsonResume, jsonOptions)!;
    }
}
