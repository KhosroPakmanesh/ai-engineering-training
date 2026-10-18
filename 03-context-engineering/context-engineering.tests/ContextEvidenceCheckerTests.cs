using Xunit;

public class ContextEvidenceCheckerTests
{
    // A citation must name a supplied section, regardless of which supported bracket style it uses.
    [Fact]
    public void Nonexistent_and_excluded_citations_are_rejected()
    {
        List<ProjectSection> selected = SelectedContextStrategy.Prepare(ContextTestData.ReadSections(), "Which project used PostgreSQL?").Sections;
        Assert.Contains(ContextEvidenceChecker.CheckEvidence("It used PostgreSQL [P99].", selected), flag => flag.Contains("Rejected citation [P99]"));
        Assert.Contains(ContextEvidenceChecker.CheckEvidence("It used PostgreSQL 【P99】.", selected), flag => flag.Contains("Rejected citation [P99]"));
        Assert.Contains(ContextEvidenceChecker.CheckEvidence("It used TypeScript [P2].", selected), flag => flag.Contains("Rejected citation [P2]"));
    }

    // Valid source spans pass; invented evidence and unsupported confident answers are flagged.
    [Fact]
    public void Evidence_requires_an_exact_quote_and_accepts_an_explicit_decline()
    {
        List<ProjectSection> selected = SelectedContextStrategy.Prepare(ContextTestData.ReadSections(), "Which project used PostgreSQL?").Sections;
        Assert.Empty(ContextEvidenceChecker.CheckEvidence("The inventory service used PostgreSQL: \"PostgreSQL stores stock levels and product records.\" [P1].", selected));
        Assert.Empty(ContextEvidenceChecker.CheckEvidence("It states that “PostgreSQL stores stock levels and product records”【P1】.", selected));
        Assert.Equal(["P1"], ContextEvidenceChecker.CitedIds("A citation [P1] repeated as 【P1】."));
        Assert.NotEmpty(ContextEvidenceChecker.CheckEvidence("It used PostgreSQL: \"a made-up quotation\" [P1].", selected));
        Assert.Empty(ContextEvidenceChecker.CheckEvidence(ContextCreatorHelpers.UncertaintyResponse, [], []));
        Assert.NotEmpty(ContextEvidenceChecker.CheckEvidence("The Salesforce project is P1 [P1].", selected, []));
    }

    // A real quotation still needs a source ID so the learner can trace the answer.
    [Fact]
    public void An_answer_without_a_citation_is_flagged()
    {
        List<ProjectSection> selected = SelectedContextStrategy.Prepare(
            ContextTestData.ReadSections(), "Which project used PostgreSQL?").Sections;

        List<string> flags = ContextEvidenceChecker.CheckEvidence(
            "The inventory service used PostgreSQL: \"PostgreSQL stores stock levels and product records.\".", selected);

        Assert.Contains("Missing section citation.", flags);
    }
}
