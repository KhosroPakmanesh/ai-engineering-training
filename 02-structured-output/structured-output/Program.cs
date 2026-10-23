using Microsoft.Agents.AI;

const string resumeFileName = "resume.md";
const string resumeFile = $"SampleData/{resumeFileName}";

try
{
    string folder = AppContext.BaseDirectory;

    // Read the resume beside the executable so the launch folder does not matter.
    string rawStringResume = await File.ReadAllTextAsync(Path.Combine(folder, resumeFile));

    OutputPrinter.PrintIntroduction(resumeFileName);

    // The model proposes a JSON profile; local checks decide whether to accept it.
    AgentResponse<CandidateProfile> response = await CvExtractor.ExtractAsync(rawStringResume, resumeFileName);

    // Check JSON field names and value types before creating profile records.
    JsonResumeSchemaValidator.ValidateSchema(response.Text);

    // Result asks Agent Framework to convert the checked JSON into the profile records.
    CandidateProfile profile = response.Result;

    // Correct JSON structure is not enough: each fact also needs an exact quote from the resume.
    ProfileValidator.ValidateProfile(profile, rawStringResume, resumeFileName);

    // Trim skill names and remove duplicates only after their evidence has passed the checks.
    profile = ProfileNormalizer.NormalizeProfile(profile);

    // Present only the profile that completed every required stage.
    OutputPrinter.PrintProfile(profile);
    OutputPrinter.PrintTakeaway();

    return 0;
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
