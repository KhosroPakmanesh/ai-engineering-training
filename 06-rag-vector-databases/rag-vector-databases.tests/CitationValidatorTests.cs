using ResumeRag.Tests.TestData;
using Xunit;

namespace ResumeRag.Tests;

public class CitationValidatorTests
{
    // A real retrieved ID still fails if the context budget or evidence guard excluded that passage.
    [Theory]
    [InlineData("Docker work with no citation.")]
    [InlineData("Docker work [invented.md#chunk-01].")]
    [InlineData("Docker work [project-b.md#chunk-01].")]
    public void Missing_unknown_or_excluded_citations_are_rejected(string answerString)
    {
        List<RetrievedChunk> selectedRetrievedChunks = [new(RagFixtures.CreateIndex().IndexedChunks[0].DocumentChunk, 0.9)];
        Assert.Throws<InvalidDataException>(() => CitationValidator.Validate(answerString, CitationParser.Parse(answerString), selectedRetrievedChunks));
    }

    // Source membership is checked separately from the learner's review of factual meaning.
    [Fact]
    public void Supplied_citation_is_accepted()
    {
        const string answerString = "Mira packaged an API [project-a.md#chunk-01].";
        CitationValidator.Validate(answerString, CitationParser.Parse(answerString),
            [new(RagFixtures.CreateIndex().IndexedChunks[0].DocumentChunk, 0.9)]);
    }
}
