using Xunit;

public class ExperienceInputsLoaderTests
{
    // A valid question still cannot be sent while a current role has an unresolved end date.
    [Theory]
    [InlineData(null)]
    [InlineData("invalid")]
    public async Task Missing_or_invalid_evaluation_dates_fail_during_loading(string? evaluationDate)
    {
        await Assert.ThrowsAsync<InvalidDataException>(() => ExperienceInputsLoader.LoadAsync("How much experience?", evaluationDate));
    }
}
