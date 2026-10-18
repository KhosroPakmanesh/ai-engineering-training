using System.Text.Json;
using Xunit;

public class ContextCreatorTests
{
    // Keep the question and instructions the same; change only the supplied project sections.
    [Fact]
    public void Both_prompts_keep_the_question_and_change_only_the_sections()
    {
        const string question = "Which project used PostgreSQL?";
        List<ProjectSection> sections = ContextTestData.ReadSections();
        List<PreparedContext> preparedContexts = ContextCreator.CreateContextStrategies(
            new ComparisonInputs(question, sections, null));
        ContextStrategy fullContext = preparedContexts[0].Context;
        ContextStrategy selectedContext = preparedContexts[1].Context;
        Assert.Equal("FULL", fullContext.Strategy);
        Assert.Equal("SELECTED", selectedContext.Strategy);
        Assert.Equal(sections, fullContext.Sections);
        Assert.All(sections, section => Assert.Contains(section.Text, fullContext.Text));
        Assert.Equal(sections[0].Text, selectedContext.Text);

        using JsonDocument full = JsonDocument.Parse(preparedContexts[0].Prompt);
        using JsonDocument selected = JsonDocument.Parse(preparedContexts[1].Prompt);
        Assert.Equal(question, full.RootElement.GetProperty("question").GetString());
        Assert.Equal(question, selected.RootElement.GetProperty("question").GetString());
        Assert.Equal(6, full.RootElement.GetProperty("sections").GetArrayLength());
        Assert.Single(selected.RootElement.GetProperty("sections").EnumerateArray());
        Assert.Equal(full.RootElement.GetProperty("sections")[0].GetRawText(), selected.RootElement.GetProperty("sections")[0].GetRawText());
    }

    // JSON can write one character as six characters, so check the final prompt's length.
    [Fact]
    public void Complete_prompt_limit_is_checked_separately_from_context_size()
    {
        const string question = "Which project used PostgreSQL?";
        List<ProjectSection> sections = ContextTestData.ReadSections();
        sections[^1] = sections[^1] with { Text = sections[^1].Text + "\n" + new string('\u03A9', 2_000) };

        ContextStrategy fullContext = FullContextStrategy.Prepare(sections);
        Assert.InRange(fullContext.Text.Length, 1, ContextCreatorHelpers.ContextCharacterCap);

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            ContextCreator.CreateContextStrategies(new ComparisonInputs(question, sections, null)));
        Assert.Contains($"The complete prompt exceeds {ContextCreatorHelpers.PromptCharacterCap} characters.", error.Message);
    }
}
