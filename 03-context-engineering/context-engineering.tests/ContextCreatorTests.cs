using System.Text.Json;
using Xunit;

public class ContextCreatorTests
{
    // Keep the question and instructions the same; change only the supplied project sections.
    [Fact]
    public void Both_prompts_keep_the_question_and_change_only_the_sections()
    {
        // Arrange
        const string questionString = "Which project used PostgreSQL?";
        List<ProjectSection> projectSections = ContextTestData.ReadSections();

        // Act
        List<PreparedContext> preparedContexts = ContextCreator.CreateContextStrategies(
            new ComparisonInputs(
                questionString,
                projectSections,
                null));
        ContextStrategy fullContextStrategy = preparedContexts[0].ContextStrategy;

        ContextStrategy selectedContextStrategy = preparedContexts[1].ContextStrategy;

        // Assert
        Assert.Equal(
            "FULL",
            fullContextStrategy.StrategyString);
        Assert.Equal(
            "SELECTED",
            selectedContextStrategy.StrategyString);
        Assert.Equal(
            projectSections,
            fullContextStrategy.ProjectSections);
        Assert.All(
            projectSections,
            projectSection => Assert.Contains(
                projectSection.TextString,
                fullContextStrategy.TextString));
        Assert.Equal(
            projectSections[0].TextString,
            selectedContextStrategy.TextString);

        using JsonDocument fullJsonDocument = JsonDocument.Parse(preparedContexts[0].PromptString);
        using JsonDocument selectedJsonDocument = JsonDocument.Parse(preparedContexts[1].PromptString);
        Assert.Equal(
            questionString,
            fullJsonDocument.RootElement.GetProperty("question").GetString());
        Assert.Equal(
            questionString,
            selectedJsonDocument.RootElement.GetProperty("question").GetString());
        Assert.Equal(
            6,
            fullJsonDocument.RootElement.GetProperty("sections").GetArrayLength());
        Assert.Single(selectedJsonDocument.RootElement.GetProperty("sections").EnumerateArray());
        Assert.Equal(
            fullJsonDocument.RootElement.GetProperty("sections")[0].GetRawText(),
            selectedJsonDocument.RootElement.GetProperty("sections")[0].GetRawText());
    }

    // JSON can write one character as six characters, so check the final prompt's length.
    [Fact]
    public void Complete_prompt_limit_is_checked_separately_from_context_size()
    {
        // Arrange
        const string questionString = "Which project used PostgreSQL?";
        List<ProjectSection> projectSections = ContextTestData.ReadSections();
        projectSections[^1] = projectSections[^1] with
        {
            TextString = projectSections[^1].TextString + "\n" + new string(
                '\u03A9',
                2_000)
        };

        // Act
        ContextStrategy fullContextStrategy = FullContextStrategy.Prepare(projectSections);

        // Assert
        Assert.InRange(
            fullContextStrategy.TextString.Length,
            1,
            ContextCreatorHelpers.ContextCharacterCapInt);

        // Act
        InvalidDataException errorInvalidDataException = Assert.Throws<InvalidDataException>(() =>
            ContextCreator.CreateContextStrategies(new ComparisonInputs(
                questionString,
                projectSections,
                null)));

        // Assert
        Assert.Contains(
            $"The complete prompt exceeds {ContextCreatorHelpers.PromptCharacterCapInt} characters.",
            errorInvalidDataException.Message);
    }
}
