using System.ClientModel;
using System.Text.Json;

const string resumeFileName = "resume.md";
const string resumeFile = $"SampleData/{resumeFileName}";

try
{
    string folder = AppContext.BaseDirectory;

    // Read the copied resume regardless of the process's working directory.
    string rawStringResume = await File.ReadAllTextAsync(Path.Combine(folder, resumeFile));

    // The model proposes a JSON profile; local checks decide whether to accept it.
    string jsonResume = await CvExtractor.ExtractAsync(rawStringResume, resumeFileName);

    // Reject invalid JSON structure before converting it into typed records.
    JsonResumeSchemaValidator.ValidateSchema(jsonResume);

    CandidateProfile profile = ProfileExtractor.ExtractProfile(jsonResume);

    // A well-shaped profile must still support its facts with exact source quotes.
    ProfileValidator.ValidateProfile(profile, rawStringResume, resumeFileName);

    // Clean up supported skill names only after the source checks pass.
    profile = ProfileNormalizer.NormalizeProfile(profile);

    JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    string formattedJson = JsonSerializer.Serialize(profile, jsonOptions);
    Console.WriteLine(formattedJson);

    return 0;
}
catch (ClientResultException error)
{
    AiProvider.WriteError(error);
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
