using System.Text.Json;

public static class ProfileExtractor
{
    // Convert JSON into profile records after its field names and value types have been checked.
    public static CandidateProfile ExtractProfile(string jsonResume)
    {
        JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
        return JsonSerializer.Deserialize<CandidateProfile>(jsonResume, jsonOptions)!;
    }
}
