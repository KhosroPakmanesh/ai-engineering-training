using Microsoft.Agents.AI;

const string resumeFileNameString = "resume.md";
const string resumeFileString = $"SampleData/{resumeFileNameString}";

try
{
    //------------------------------------------------------------------------//
    //                              PREPARATION                               //
    //------------------------------------------------------------------------//

    // 1. Resolve the copied inputs beside the executable.
    string folderString = AppContext.BaseDirectory;

    // 2. Read the source before requesting a profile.
    string rawStringResume = await File.ReadAllTextAsync(Path.Combine(folderString, resumeFileString));

    // 3. Explain the source and acceptance checks.
    OutputPrinter.PrintIntroduction(resumeFileNameString);

    //------------------------------------------------------------------------//
    //                             MODEL REQUEST                              //
    //------------------------------------------------------------------------//

    // 1. The model proposes a JSON profile; local checks decide whether to accept it.
    AgentResponse<CandidateProfile> agentResponse = await CvExtractor.ExtractAsync(rawStringResume, resumeFileNameString);

    //------------------------------------------------------------------------//
    //                      VALIDATION AND NORMALIZATION                      //
    //------------------------------------------------------------------------//

    // 1. Check JSON field names and value types before creating profile records.
    JsonResumeSchemaValidator.ValidateSchema(agentResponse.Text);

    // 2. Result asks Agent Framework to convert the checked JSON into the profile records.
    CandidateProfile candidateProfile = agentResponse.Result;

    // 3. Correct JSON structure is not enough: each fact also needs an exact quote from the resume.
    ProfileValidator.ValidateProfile(candidateProfile, rawStringResume, resumeFileNameString);

    // 4. Trim skill names and remove duplicates only after their evidence has passed the checks.
    CandidateProfile normalizedCandidateProfile = ProfileNormalizer.NormalizeProfile(candidateProfile);

    //------------------------------------------------------------------------//
    //                          RESULTS AND TAKEAWAY                          //
    //------------------------------------------------------------------------//

    // 1. Present only the profile that completed every required stage.
    OutputPrinter.PrintProfile(normalizedCandidateProfile);
    OutputPrinter.PrintTakeaway();

    return 0;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("The request was canceled or exceeded the 120-second time limit.");
    return 1;
}
catch (Exception errorException)
{
    Console.Error.WriteLine(errorException.Message);
    return 1;
}
