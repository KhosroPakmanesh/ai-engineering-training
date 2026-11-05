using Xunit;

namespace ResumeRag.Tests;

public class InputsValidatorTests
{
    // Reject every source and question before constructing the remote clients.
    [Fact]
    public void Invalid_later_document_and_question_are_reported_together()
    {
        // Arrange
        List<SourceDocument> sourceDocuments =
        [
            new(
            "project-a.md",
            "# Project\n\nDocker work."),
            new(
                "project-b.md",
                "")
        ];
        List<RagQuestion> ragQuestions =
        [
            new(
            "Where did I use Docker?",
            "Docker"),
            new(
                "Where did I use Rust?",
                "Kubernetes")
        ];

        // Act
        InvalidDataException invalidDataException = Assert.Throws<InvalidDataException>(() =>
            InputsValidator.Validate(
                sourceDocuments,
                ragQuestions,
                650,
                80,
                3,
                0.2,
                2400));

        // Assert
        Assert.Contains(
            "Documents[project-b.md]",
            invalidDataException.Message);
        Assert.Contains(
            "Questions:",
            invalidDataException.Message);
    }
}
