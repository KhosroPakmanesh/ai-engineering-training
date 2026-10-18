using Xunit;

public class SelectedContextStrategyTests
{
    // Selection is deterministic even when an unsupported question has no matching section.
    [Fact]
    public void Selection_matches_every_fixture_including_an_unknown_question()
    {
        List<ProjectSection> sections = ContextTestData.ReadSections();
        foreach (QuestionFixture fixture in ContextTestData.ReadFixtures())
        {
            ContextStrategy selected = SelectedContextStrategy.Prepare(sections, fixture.Question);
            Assert.Equal(fixture.ExpectedSectionIds, selected.Sections.Select(section => section.Id));
        }
    }

    // Heading matches outrank repeated body words, and document order breaks equal scores.
    [Fact]
    public void Selection_uses_distinct_keywords_heading_priority_and_document_order()
    {
        List<ProjectSection> sections = ProjectSectionParser.ParseProjectSection("""
            ## [P1] Body match
            PostgreSQL PostgreSQL PostgreSQL
            ## [P2] PostgreSQL heading
            Second section.
            ## [P3] PostgreSQL heading
            Third section.
            """);
        ContextStrategy selected = SelectedContextStrategy.Prepare(
            sections, "Which project used POSTGRESQL PostgreSQL?");
        Assert.Equal(["P2", "P3"], selected.Sections.Select(section => section.Id));
    }

    // Selected evidence is kept complete even when a caller supplies a smaller cap.
    [Fact]
    public void Context_is_accepted_at_the_cap_and_rejected_above_it()
    {
        const string question = "Which project used PostgreSQL?";
        List<ProjectSection> sections = ContextTestData.ReadSections();
        ContextStrategy context = SelectedContextStrategy.Prepare(sections, question);
        ContextStrategy atCap = SelectedContextStrategy.Prepare(sections, question, context.Text.Length);

        Assert.Equal(context.Text, atCap.Text);
        Assert.Throws<InvalidDataException>(() =>
            SelectedContextStrategy.Prepare(sections, question, context.Text.Length - 1));
    }
}
