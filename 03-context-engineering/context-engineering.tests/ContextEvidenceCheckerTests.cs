using Xunit;

public class ContextEvidenceCheckerTests
{
    // A reference must point to a supplied project, whether it uses [P1] or 【P1】.
    [Fact]
    public void Nonexistent_and_excluded_citations_are_rejected()
    {
        List<ProjectSection> selected = SelectedContextStrategy.Prepare(ContextTestData.ReadSections(), "Which project used PostgreSQL?").Sections;
        Assert.Contains(ContextEvidenceChecker.CheckEvidence("It used PostgreSQL [P99].", selected), flag => flag.Contains("Rejected citation [P99]"));
        Assert.Contains(ContextEvidenceChecker.CheckEvidence("It used PostgreSQL 【P99】.", selected), flag => flag.Contains("Rejected citation [P99]"));
        Assert.Contains(ContextEvidenceChecker.CheckEvidence("It used TypeScript [P2].", selected), flag => flag.Contains("Rejected citation [P2]"));
    }

    // Accept exact project quotes; flag invented quotes and answers that should admit missing evidence.
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

    // Even an exact quote needs a project ID so the learner can find its source.
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
