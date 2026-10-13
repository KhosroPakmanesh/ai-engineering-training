using System.ClientModel;
using System.Text.Json;

// Change this to "resume-ambiguous.md" to explore missing facts and ambiguous dates.
const string resumeFile = "resume.md";

try
{
    // Read the source before asking the agent to extract its facts.
    string folder = AppContext.BaseDirectory;
    string resume = await ReadResumeAsync(folder, resumeFile);

    // The lesson: CV -> agent's JSON -> schema and data checks -> typed profile.
    string json = await CvExtractor.ExtractAsync(resume, resumeFile);
    CandidateProfile profile = ProfileValidator.ParseAndValidate(json, resume, resumeFile);

    // Present and save only a profile that passed the local checks.
    await WriteProfileAsync(profile, folder);
    return 0;
}
// An unsuccessful extraction leaves the previously saved profile in place.
catch (ClientResultException error)
{
    OpenRouter.WriteError(error);
    return 1;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("The request was canceled or exceeded the 120-second time limit.");
    return 1;
}
catch (Exception error)
{
    Console.Error.WriteLine(error.Message);
    return 1;
}

// Copied input files make console and Visual Studio execution use the same source.
static Task<string> ReadResumeAsync(string folder, string resumeFile) =>
    File.ReadAllTextAsync(Path.Combine(folder, "sample-data", resumeFile));

// Both destinations receive the same formatted, validated JSON.
static async Task WriteProfileAsync(CandidateProfile profile, string folder)
{
    string formattedJson = JsonSerializer.Serialize(profile, ProfileValidator.JsonOptions);
    Console.WriteLine(formattedJson);
    string outputFile = Path.Combine(folder, "profile.json");
    await File.WriteAllTextAsync(outputFile, formattedJson);
    Console.WriteLine($"Saved validated profile: {outputFile}");
}
