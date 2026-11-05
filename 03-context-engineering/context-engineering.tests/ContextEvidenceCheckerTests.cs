using Xunit;

public class ContextEvidenceCheckerTests
{
    // A reference must point to a supplied project, whether it uses [P1] or 【P1】.
    [Fact]
    public void Nonexistent_and_excluded_citations_are_rejected()
    {
        // Arrange
        List<ProjectSection> selectedProjectSections = SelectedContextStrategy.Prepare(
            ContextTestData.ReadSections(),
            "Which project used PostgreSQL?").ProjectSections;

        // Act
        Assert.Contains(
            ContextEvidenceChecker.CheckEvidence(
                "It used PostgreSQL [P99].",
                selectedProjectSections),
            flagString => flagString.Contains("Rejected citation [P99]"));

        // Assert
        Assert.Contains(
            ContextEvidenceChecker.CheckEvidence(
                "It used PostgreSQL 【P99】.",
                selectedProjectSections),
            flagString => flagString.Contains("Rejected citation [P99]"));
        Assert.Contains(
            ContextEvidenceChecker.CheckEvidence(
                "It used TypeScript [P2].",
                selectedProjectSections),
            flagString => flagString.Contains("Rejected citation [P2]"));
    }

    // Accept exact project quotes; flag invented quotes and answers that should admit missing evidence.
    [Fact]
    public void Evidence_requires_an_exact_quote_and_accepts_an_explicit_decline()
    {
        // Arrange
        List<ProjectSection> selectedProjectSections = SelectedContextStrategy.Prepare(
            ContextTestData.ReadSections(),
            "Which project used PostgreSQL?").ProjectSections;

        // Act
        Assert.Empty(ContextEvidenceChecker.CheckEvidence(
            "The inventory service used PostgreSQL: \"PostgreSQL stores stock levels and product records.\" [P1].",
            selectedProjectSections));

        // Assert
        Assert.Empty(ContextEvidenceChecker.CheckEvidence(
            "It states that “PostgreSQL stores stock levels and product records”【P1】.",
            selectedProjectSections));
        Assert.Equal(
            ["P1"],
            ContextEvidenceChecker.CitedIds("A citation [P1] repeated as 【P1】."));
        Assert.NotEmpty(ContextEvidenceChecker.CheckEvidence(
            "It used PostgreSQL: \"a made-up quotation\" [P1].",
            selectedProjectSections));
        Assert.Empty(ContextEvidenceChecker.CheckEvidence(
            ContextCreatorHelpers.UncertaintyResponseString,
            [],
            []));
        Assert.NotEmpty(ContextEvidenceChecker.CheckEvidence(
            "The Salesforce project is P1 [P1].",
            selectedProjectSections,
            []));
    }

    // Even an exact quote needs a project ID so the learner can find its source.
    [Fact]
    public void An_answer_without_a_citation_is_flagged()
    {
        // Arrange
        List<ProjectSection> selectedProjectSections = SelectedContextStrategy.Prepare(
            ContextTestData.ReadSections(),
            "Which project used PostgreSQL?").ProjectSections;

        // Act
        List<string> flagStrings = ContextEvidenceChecker.CheckEvidence(
            "The inventory service used PostgreSQL: \"PostgreSQL stores stock levels and product records.\".",
            selectedProjectSections);

        // Assert
        Assert.Contains(
            "Missing section citation.",
            flagStrings);
    }
}
