const string questionString = "Which project used PostgreSQL?";
const string sourceFileString = "projects.md";

try
{
    //------------------------------------------------------------------------//
    //                              PREPARATION                               //
    //------------------------------------------------------------------------//

    // 1. Reject an unusable question before loading inputs or making model requests.
    ComparisonInputsValidator.ValidateQuestion(questionString);

    // 2. Expected project IDs let us check whether each context includes the project needed to answer.
    ComparisonInputs comparisonInputs = await ComparisonInputsLoader
        .LoadComparisonInputsAsync(
            questionString,
            sourceFileString);

    // 3. Prepare and size-check both prompts before sending the first request.
    List<PreparedContext> preparedContexts = ContextCreator.CreateContextStrategies(comparisonInputs);

    // 4. Explain what changes and what stays fixed.
    OutputPrinter.PrintIntroduction(
        questionString,
        sourceFileString);

    //------------------------------------------------------------------------//
    //                               COMPARISON                               //
    //------------------------------------------------------------------------//

    // 1. Change only the supplied context while keeping the question and settings fixed.
    ComparisonReport comparisonReport = await ContextComparer.
        CompareContextAsync(
            sourceFileString,
            comparisonInputs,
            preparedContexts);

    //------------------------------------------------------------------------//
    //                          RESULTS AND TAKEAWAY                          //
    //------------------------------------------------------------------------//

    // 1. Show each answer with its prompt size, request time, and any failed evidence checks.
    OutputPrinter.PrintResults(
        comparisonReport,
        comparisonInputs.ProjectSections);
    OutputPrinter.PrintTakeaway();

    return comparisonReport.ContextResults.All(contextResult => contextResult.EvidenceFlagStrings.Count == 0) ? 0 : 1;
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
