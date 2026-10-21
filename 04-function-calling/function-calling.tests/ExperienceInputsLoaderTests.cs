using Xunit;

public class ExperienceInputsLoaderTests
{
    // Loading keeps the selected question, reference date, and original job dates for the next stage.
    [Fact]
    public async Task Copied_jobs_keep_the_supplied_question_and_evaluation_date()
    {
        const string question = "How much experience?";
        const string evaluationDate = "2023-04-01";
        ExperienceInputs inputs = await ExperienceInputsLoader.LoadAsync(question, evaluationDate);

        Assert.Equal(question, inputs.Question);
        Assert.Equal(evaluationDate, inputs.EvaluationDate);
        Assert.Equal(3, inputs.Intervals.Count);
        Assert.Equal(new WorkInterval("2020-01-01", "2021-07-01"), inputs.Intervals[0]);
        Assert.Null(inputs.Intervals[2].EndDate);
    }
}
