using Xunit;

public class InputValidatorTests
{
    // A missing instruction is an input error even when the earlier case is usable.
    [Fact]
    public void Empty_later_instruction_is_rejected()
    {
        // Arrange
        var comparisonInputs = new ComparisonInputs(
            "Fictional resume.",
            [
                new(
                "A",
                "a.txt",
                "Summarize."),
                new(
                    "B",
                    "b.txt",
                    " ")
            ]);

        // Act
        InvalidDataException invalidDataException = Assert.Throws<InvalidDataException>(() =>
            InputValidator.Validate(comparisonInputs));

        // Assert
        Assert.Contains(
            "prompt instruction",
            invalidDataException.Message);
    }

    // A later empty comparison case must stop preparation even when an earlier prompt is usable.
    [Fact]
    public void Empty_resume_is_rejected()
    {
        // Arrange
        var comparisonInputs = new ComparisonInputs(
            " ",
            [
                new(
                "A",
                "a.txt",
                "Summarize.")
            ]);

        // Act
        InvalidDataException invalidDataException = Assert.Throws<InvalidDataException>(() =>
            InputValidator.Validate(comparisonInputs));

        // Assert
        Assert.Contains(
            "resume is empty",
            invalidDataException.Message);
    }
}
