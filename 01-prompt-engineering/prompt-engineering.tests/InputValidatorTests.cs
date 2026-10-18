using Xunit;

public class InputValidatorTests
{
    // Preparation should discover a missing prompt before the comparison can spend a request.
    [Fact]
    public async Task Missing_prompt_is_rejected_during_preparation_without_an_api_request()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            InputValidator.ValidateInputsAsync(["Prompts/does-not-exist.txt"]));
    }

    // An empty file exists, but its missing instruction must still prevent a request.
    [Fact]
    public async Task Empty_prompt_is_rejected_before_an_api_request()
    {
        string promptFile = Path.GetTempFileName();
        try
        {
            InvalidDataException error = await Assert.ThrowsAsync<InvalidDataException>(() =>
                InputValidator.ValidateInputsAsync([promptFile]));
            Assert.Contains("The prompt instruction is empty.", error.Message);
        }
        finally
        {
            File.Delete(promptFile);
        }
    }
}
