using System.ClientModel;

var prompts = new[]
{
    ("A: Zero-shot", "prompts/A-zero-shot.txt"),
    ("B: Few-shot", "prompts/B-few-shot.txt"),
    ("C: Constrained", "prompts/C-constrained.txt")
};

try
{
    // The experiment: the same resume, model, and settings; only the instruction changes.
    foreach (var (name, promptFile) in prompts)
    {
        Console.WriteLine($"\n{name}");
        string summary = await ResumeSummarizer.SummarizeAsync(promptFile);
        Console.WriteLine(summary);
        Console.WriteLine($"Word count: {summary.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Length}");
    }
    return 0;
}
catch (ClientResultException error)
{
    Console.Error.WriteLine($"OpenRouter request failed (HTTP {error.Status}): {error.Message}");
    Console.Error.WriteLine(error.GetRawResponse()?.Content);
    if (error.Status == 429)
        Console.Error.WriteLine("A rate or provider capacity limit was reached. Wait and run again, or change OPENROUTER_MODEL in .env.");
    return 1;
}
catch (InvalidOperationException error)
{
    Console.Error.WriteLine(error.Message);
    return 1;
}