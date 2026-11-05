List<PromptFile> promptFiles =
[
    new(
        "A: Zero-shot",
        "Prompts/A-zero-shot.txt"),
    new(
            "B: Few-shot",
            "Prompts/B-few-shot.txt"),
    new(
        "C: Constrained",
        "Prompts/C-constrained.txt")
];

try
{
    //------------------------------------------------------------------------//
    //                              PREPARATION                               //
    //------------------------------------------------------------------------//

    // 1. Load every case and check the complete comparison before paid work.
    ComparisonInputs comparisonInputs = await ComparisonInputsLoader.LoadAsync(promptFiles);
    InputValidator.Validate(comparisonInputs);

    // 2. Freeze every prompt and the selected settings before the first request.
    List<PreparedPrompt> preparedPrompts = PromptPreparer.Prepare(comparisonInputs);
    ProviderSettings providerSettings = await SettingsReader.ReadSettingsAsync(AppContext.BaseDirectory);

    // 3. Explain the fixed inputs and exact request plan.
    OutputPrinter.PrintIntroduction(preparedPrompts);
    OutputPrinter.PrintRequestDetails(providerSettings);

    //------------------------------------------------------------------------//
    //                               COMPARISON                               //
    //------------------------------------------------------------------------//

    // 1. Compare the prepared prompts in independent conversations.
    await PromptComparer.ComparePromptsAsync(
        preparedPrompts,
        providerSettings);

    //------------------------------------------------------------------------//
    //                                TAKEAWAY                                //
    //------------------------------------------------------------------------//

    // 1. Explain the local checks and suggest one input to change.
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
