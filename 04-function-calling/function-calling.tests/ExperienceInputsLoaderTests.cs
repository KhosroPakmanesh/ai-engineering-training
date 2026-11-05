using Xunit;

public class ExperienceInputsLoaderTests
{
    // Loading keeps the selected question, reference date, and original job dates for the next stage.
    [Fact]
    public async Task Copied_jobs_keep_the_supplied_question_and_evaluation_date()
    {
        // Arrange
        const string questionString = "How much experience?";
        const string evaluationDateString = "2023-04-01";

        // Act
        ExperienceInputs experienceInputs = await ExperienceInputsLoader.LoadAsync(
            questionString,
            evaluationDateString);

        // Assert
        Assert.Equal(
            questionString,
            experienceInputs.QuestionString);
        Assert.Equal(
            evaluationDateString,
            experienceInputs.EvaluationDateString);
        Assert.Equal(
            3,
            experienceInputs.WorkIntervals.Count);
        Assert.Equal(
            new WorkInterval(
                "2020-01-01",
                "2021-07-01"),
            experienceInputs.WorkIntervals[0]);
        Assert.Null(experienceInputs.WorkIntervals[2].EndDateString);
    }
}
