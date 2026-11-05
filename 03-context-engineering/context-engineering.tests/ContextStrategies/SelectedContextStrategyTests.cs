using Xunit;

public class SelectedContextStrategyTests
{
    // Select the expected projects for each example question, or none when nothing matches.
    [Fact]
    public void Selection_matches_every_fixture_including_an_unknown_question()
    {
        // Arrange
        List<ProjectSection> projectSections = ContextTestData.ReadSections();

        // Act
        // Assert
        foreach (QuestionFixture questionFixture in ContextTestData.ReadFixtures())
        {
            ContextStrategy selectedContextStrategy = SelectedContextStrategy.Prepare(
                projectSections,
                questionFixture.QuestionString);
            Assert.Equal(
                questionFixture.ExpectedSectionIdStrings,
                selectedContextStrategy.ProjectSections.Select(projectSection => projectSection.IdString));
        }
    }

    // Prefer heading matches, count repeated words once, and keep document order for equal scores.
    [Fact]
    public void Selection_uses_distinct_keywords_heading_priority_and_document_order()
    {
        // Arrange
        List<ProjectSection> projectSections = ProjectSectionParser.ParseProjectSection("""
            ## [P1] Body match
            PostgreSQL PostgreSQL PostgreSQL
            ## [P2] PostgreSQL heading
            Second section.
            ## [P3] PostgreSQL heading
            Third section.
            """);

        // Act
        ContextStrategy selectedContextStrategy = SelectedContextStrategy.Prepare(
            projectSections,
            "Which project used POSTGRESQL PostgreSQL?");

        // Assert
        Assert.Equal(
            ["P2", "P3"],
            selectedContextStrategy.ProjectSections.Select(projectSection => projectSection.IdString));
    }

    // A smaller size limit must reject the selected text, not cut part of it away.
    [Fact]
    public void Context_is_accepted_at_the_cap_and_rejected_above_it()
    {
        // Arrange
        const string questionString = "Which project used PostgreSQL?";
        List<ProjectSection> projectSections = ContextTestData.ReadSections();

        // Act
        ContextStrategy contextStrategy = SelectedContextStrategy.Prepare(
            projectSections,
            questionString);

        ContextStrategy atCapContextStrategy = SelectedContextStrategy.Prepare(
            projectSections,
            questionString,
            contextStrategy.TextString.Length);

        // Assert
        Assert.Equal(
            contextStrategy.TextString,
            atCapContextStrategy.TextString);
        Assert.Throws<InvalidDataException>(() =>
            SelectedContextStrategy.Prepare(
                projectSections,
                questionString,
                contextStrategy.TextString.Length - 1));
    }
}
