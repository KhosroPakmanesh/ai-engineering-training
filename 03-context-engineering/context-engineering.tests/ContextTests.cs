using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

public class ContextTests(ITestOutputHelper output)
{
    // Selection is deterministic even when an unsupported question has no matching section.
    [Fact]
    public void Selection_matches_every_fixture_including_an_unknown_question()
    {
        ProjectSection[] sections = ReadSections();
        foreach (QuestionFixture fixture in ReadFixtures())
        {
            SelectedContext selected = ContextSelector.Select(sections, fixture.Question, full: false);
            Assert.Equal(fixture.ExpectedSectionIds, selected.Sections.Select(section => section.Id));
        }
    }

    // Heading matches outrank repeated body words, and document order breaks equal scores.
    [Fact]
    public void Selection_uses_distinct_keywords_heading_priority_and_document_order()
    {
        ProjectSection[] sections = ContextSelector.Parse("""
            ## [P1] Body match
            PostgreSQL PostgreSQL PostgreSQL
            ## [P2] PostgreSQL heading
            Second section.
            ## [P3] PostgreSQL heading
            Third section.
            """);
        SelectedContext selected = ContextSelector.Select(
            sections, "Which project used POSTGRESQL PostgreSQL?", full: false);
        Assert.Equal(["P2", "P3"], selected.Sections.Select(section => section.Id));
    }

    // Both strategies reject excess input rather than silently clipping it.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Context_is_accepted_at_the_cap_and_rejected_above_it(bool full)
    {
        ProjectSection[] sections = ReadSections();
        SelectedContext context = ContextSelector.Select(sections, "Which project used PostgreSQL?", full);
        Assert.Equal(context.Text, ContextSelector.Select(sections, "Which project used PostgreSQL?", full, context.Text.Length).Text);
        Assert.Throws<InvalidDataException>(() =>
            ContextSelector.Select(sections, "Which project used PostgreSQL?", full, context.Text.Length - 1));
    }

    // A citation must name a supplied section, regardless of which supported bracket style it uses.
    [Fact]
    public void Nonexistent_and_excluded_citations_are_rejected()
    {
        ProjectSection[] selected = ContextSelector.Select(ReadSections(), "Which project used PostgreSQL?", full: false).Sections;
        Assert.Contains(ContextEvidence.CheckEvidence("It used PostgreSQL [P99].", selected), flag => flag.Contains("Rejected citation [P99]"));
        Assert.Contains(ContextEvidence.CheckEvidence("It used PostgreSQL 【P99】.", selected), flag => flag.Contains("Rejected citation [P99]"));
        Assert.Contains(ContextEvidence.CheckEvidence("It used TypeScript [P2].", selected), flag => flag.Contains("Rejected citation [P2]"));
    }

    // Valid source spans pass; invented evidence and unsupported confident answers are flagged.
    [Fact]
    public void Evidence_requires_an_exact_quote_and_accepts_an_explicit_decline()
    {
        ProjectSection[] selected = ContextSelector.Select(ReadSections(), "Which project used PostgreSQL?", full: false).Sections;
        Assert.Empty(ContextEvidence.CheckEvidence("The inventory service used PostgreSQL: \"PostgreSQL stores stock levels and product records.\" [P1].", selected));
        Assert.Empty(ContextEvidence.CheckEvidence("It states that “PostgreSQL stores stock levels and product records”【P1】.", selected));
        Assert.Equal(["P1"], ContextEvidence.CitedIds("A citation [P1] repeated as 【P1】."));
        Assert.NotEmpty(ContextEvidence.CheckEvidence("It used PostgreSQL: \"a made-up quotation\" [P1].", selected));
        Assert.Empty(ContextEvidence.CheckEvidence(ContextComparison.UncertaintyResponse, [], []));
        Assert.NotEmpty(ContextEvidence.CheckEvidence("The Salesforce project is P1 [P1].", selected, []));
    }

    // Repeated document IDs cannot provide an unambiguous citation target.
    [Fact]
    public void Duplicate_section_ids_are_rejected()
    {
        Assert.Throws<InvalidDataException>(() => ContextSelector.Parse("## [P1] One\nText\n## [P1] Two\nMore text"));
    }

    // A controlled comparison changes only the supplied sections.
    [Fact]
    public void Both_prompts_keep_the_question_and_change_only_the_sections()
    {
        const string question = "Which project used PostgreSQL?";
        ProjectSection[] sections = ReadSections();
        using JsonDocument full = JsonDocument.Parse(ContextComparison.BuildPrompt(question, ContextSelector.Select(sections, question, true)));
        using JsonDocument selected = JsonDocument.Parse(ContextComparison.BuildPrompt(question, ContextSelector.Select(sections, question, false)));
        Assert.Equal(full.RootElement.GetProperty("question").GetString(), selected.RootElement.GetProperty("question").GetString());
        Assert.Equal(6, full.RootElement.GetProperty("sections").GetArrayLength());
        Assert.Single(selected.RootElement.GetProperty("sections").EnumerateArray());
        Assert.Equal(full.RootElement.GetProperty("sections")[0].GetRawText(), selected.RootElement.GetProperty("sections")[0].GetRawText());
    }

    // Four real OpenRouter requests total, through the same method Program.cs uses.
    [Theory]
    [InlineData("Which project used PostgreSQL?", false)]
    [InlineData("Which project used Salesforce?", true)]
    [Trait("Category", "Integration")]
    public async Task Real_comparisons_cite_evidence_or_decline_when_it_is_missing(string question, bool unknown)
    {
        ComparisonReport report = await ContextComparison.CompareAsync(question);
        output.WriteLine(JsonSerializer.Serialize(report, ContextComparison.JsonOptions));

        // Inspect each result with the same constraints, then compare the prompt sizes.
        Assert.Equal(["FULL", "SELECTED"], report.Results.Select(result => result.Strategy));
        foreach (ContextResult result in report.Results)
            AssertComparisonResult(result, report, unknown);
        Assert.True(report.Results[1].PromptCharacters < report.Results[0].PromptCharacters);
    }

    // Evidence requirements depend on whether the supplied source can answer the fixture question.
    private static void AssertComparisonResult(ContextResult result, ComparisonReport report, bool unknown)
    {
        Assert.False(string.IsNullOrWhiteSpace(result.Response));
        Assert.Empty(result.EvidenceFlags);
        Assert.InRange(result.ContextCharacters, 0, report.ContextCharacterCap);
        Assert.InRange(result.PromptCharacters, 1, report.PromptCharacterCap);
        if (unknown) Assert.Equal(ContextComparison.UncertaintyResponse, result.Response);
        else
        {
            Assert.Contains("P1", result.CitedSectionIds);
            Assert.Equal(1.0, result.SourceRecall);
        }
    }

    // The tests and application parse the same copied teaching document.
    private static ProjectSection[] ReadSections() => ContextSelector.Parse(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "sample-data", "projects.md")));

    // Fixtures specify expected sources without prescribing model wording.
    private static QuestionFixture[] ReadFixtures() => JsonSerializer.Deserialize<QuestionFixture[]>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "sample-data", "questions.json")),
        ContextComparison.JsonOptions)!;
}
