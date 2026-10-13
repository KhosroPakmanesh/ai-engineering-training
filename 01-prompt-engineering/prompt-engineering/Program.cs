using System.ClientModel;

// The three cases share one resume; only the prompt instruction changes.
(string Name, string File)[] prompts =
[
    ("A: Zero-shot", "prompts/A-zero-shot.txt"),
    ("B: Few-shot", "prompts/B-few-shot.txt"),
    ("C: Constrained", "prompts/C-constrained.txt")
];

try
{
    // Reject missing inputs before any of the three paid requests begins.
    await ResumeSummarizer.ValidateInputsAsync(prompts.Select(prompt => prompt.File));

    // Present the cases in a fixed order so their summaries can be compared.
    await ComparePromptsAsync(prompts);
    return 0;
}
// Keep provider failures and local failures visible to the learner.
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

// Each case starts a fresh request with the same model and generation settings.
static async Task ComparePromptsAsync((string Name, string File)[] prompts)
{
    foreach (var (name, promptFile) in prompts)
    {
        Console.WriteLine($"\n{name}");
        string summary = await ResumeSummarizer.SummarizeAsync(promptFile);
        PrintSummary(summary);
    }
}

// A whitespace word count makes the constrained prompt's result easy to inspect.
static void PrintSummary(string summary)
{
    Console.WriteLine(summary);
    Console.WriteLine($"Word count: {summary.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Length}");
}
