using ResumeRag.Tests.TestData;
using Xunit;

namespace ResumeRag.Tests;

public class EvidenceSelectorTests
{
    // A high vector score without the requested skill must not turn an unrelated source into evidence.
    [Fact]
    public void Skill_and_score_are_both_required()
    {
        // Arrange
        DocumentChunk sourceDocumentChunk = RagFixtures.CreateIndex().IndexedChunks[0].DocumentChunk;

        // Act
        Assert.Empty(EvidenceSelector.Prepare(
            new(
                "Where did I use Kubernetes?",
                "Kubernetes"),
            [
                new(
                sourceDocumentChunk,
                0.99)
            ],
            0.2,
            2400).SelectedRetrievedChunks);

        // Assert
        Assert.Empty(EvidenceSelector.Prepare(
            new(
                "Where did I use Docker?",
                "Docker"),
            [
                new(
                sourceDocumentChunk,
                0.1)
            ],
            0.2,
            2400).SelectedRetrievedChunks);
        var ragQuestion = new RagQuestion(
            "Where did I use Docker?",
            "Docker");
        List<RetrievedChunk> candidateRetrievedChunks =
        [
            new(
            sourceDocumentChunk,
            0.9)
        ];
        PreparedAnswer acceptedPreparedAnswer = EvidenceSelector.Prepare(
            ragQuestion,
            candidateRetrievedChunks,
            0.2,
            2400);
        Assert.Same(
            ragQuestion,
            acceptedPreparedAnswer.RagQuestion);
        Assert.Same(
            candidateRetrievedChunks,
            acceptedPreparedAnswer.CandidateRetrievedChunks);
        Assert.Single(acceptedPreparedAnswer.SelectedRetrievedChunks);
        Assert.Contains(
            "Where did I use Docker?",
            acceptedPreparedAnswer.PromptString);
    }

    // Never truncate a passage and then cite text that was not actually supplied to the agent.
    [Fact]
    public void Context_cap_excludes_whole_passages()
    {
        // Arrange
        DocumentChunk sourceDocumentChunk = RagFixtures.CreateIndex().IndexedChunks[0].DocumentChunk;

        // Act
        PreparedAnswer preparedAnswer = EvidenceSelector.Prepare(
            new(
                "Where did I use Docker?",
                "Docker"),
            [
                new(
                sourceDocumentChunk,
                0.9)
            ],
            0.2,
            10);

        // Assert
        Assert.Empty(preparedAnswer.SelectedRetrievedChunks);
        Assert.DoesNotContain(
            sourceDocumentChunk.TextString,
            preparedAnswer.PromptString);
    }
}
