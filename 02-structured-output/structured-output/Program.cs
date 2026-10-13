using System.ClientModel;
using System.Text.Json;

// Change this to "resume-ambiguous.md" to explore missing facts and ambiguous dates.
const string resumeFile = "resume.md";

try
{
    string folder = AppContext.BaseDirectory;
    string resume = await File.ReadAllTextAsync(Path.Combine(folder, "sample-data", resumeFile));

    // The lesson: CV -> agent's JSON -> schema and data checks -> typed profile.
    string json = await CvExtractor.ExtractAsync(resume, resumeFile);
    CandidateProfile profile = ProfileValidator.ParseAndValidate(json, resume, resumeFile);

    string formattedJson = JsonSerializer.Serialize(profile, ProfileValidator.JsonOptions);
    Console.WriteLine(formattedJson);
    string outputFile = Path.Combine(folder, "profile.json");
    await File.WriteAllTextAsync(outputFile, formattedJson);
    Console.WriteLine($"Saved validated profile: {outputFile}");
    return 0;
}
catch (ClientResultException error)
{
    Console.Error.WriteLine($"OpenRouter request failed (HTTP {error.Status}): {error.Message}");
    if (error.Status == 429)
        Console.Error.WriteLine("The provider is rate limiting requests or is at capacity. Wait and run again.");
    return 1;
}
catch (Exception error)
{
    Console.Error.WriteLine(error.Message);
    return 1;
}
