using Xunit;

public class InputValidatorTests
{
    // Find a missing prompt file before making any paid model calls.
    [Fact]
    public async Task Missing_prompt_is_rejected_during_preparation_without_an_api_request()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            InputValidator.ValidateInputsAsync(["Prompts/does-not-exist.txt"]));
    }

    // A later empty comparison case must stop preparation even when an earlier prompt is usable.
    [Fact]
    public async Task Empty_prompt_is_rejected_before_an_api_request()
    {
        string promptFile = Path.GetTempFileName();
        try
        {
            InvalidDataException error = await Assert.ThrowsAsync<InvalidDataException>(() =>
                InputValidator.ValidateInputsAsync(
                [
                    "Prompts/A-zero-shot.txt",
                    promptFile
                ]
            ));
            Assert.Contains("The prompt instruction is empty.", error.Message);
        }
        finally
        {
            File.Delete(promptFile);
        }
    }
}
