List<(string Name, string File)> prompts =
[
    ("A: Zero-shot", "Prompts/A-zero-shot.txt"),
    ("B: Few-shot", "Prompts/B-few-shot.txt"),
    ("C: Constrained", "Prompts/C-constrained.txt")
];

try
{
    // Check every input file before making any paid model calls.
    await InputValidator.ValidateInputsAsync(prompts.Select(prompt => prompt.File));

    // Explain what changes before the learner sees the three answers.
    OutputPrinter.PrintIntroduction(prompts.Count);

    // Keep the resume fixed while comparing the three prompt instructions.
    await PromptComparer.ComparePromptsAsync(prompts);

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
