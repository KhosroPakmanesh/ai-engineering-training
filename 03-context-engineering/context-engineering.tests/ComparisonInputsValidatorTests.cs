using Xunit;

public class ComparisonInputsValidatorTests
{
    // Both console and test pipelines reject empty or oversized questions before preparation.
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Empty_questions_are_rejected(string questionString)
    {
        Assert.Throws<ArgumentException>(() => ComparisonInputsValidator.ValidateQuestion(questionString));
    }

    // The character cap belongs to the input boundary rather than the request method.
    [Fact]
    public void Oversized_question_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => ComparisonInputsValidator.ValidateQuestion(new string('x', 501)));
    }
}
