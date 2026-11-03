using Xunit;

public class PromptPreparerTests
{
    // Every complete prompt must contain the same original source and its own unchanged instruction.
    [Fact]
    public async Task Preparation_keeps_all_original_instructions_and_one_resume()
    {
        ComparisonInputs comparisonInputs = await ComparisonInputsLoader.LoadAsync(PromptTestData.CreatePromptFiles());
        InputValidator.Validate(comparisonInputs);
        List<PreparedPrompt> preparedPrompts = PromptPreparer.Prepare(comparisonInputs);
        Assert.Equal(3, preparedPrompts.Count);
        for (int indexInt = 0; indexInt < preparedPrompts.Count; indexInt++)
        {
            Assert.Equal(comparisonInputs.PromptCases[indexInt].InstructionString + "\n\nRESUME DATA:\n" + comparisonInputs.ResumeString,
                preparedPrompts[indexInt].TextString);
        }
    }
}
