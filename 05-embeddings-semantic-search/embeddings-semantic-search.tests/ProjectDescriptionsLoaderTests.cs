using Xunit;

public class ProjectDescriptionsLoaderTests
{
    // The complete copied corpus includes relevant work, a keyword trap, and unrelated controls.
    [Fact]
    public async Task Copied_sample_contains_twelve_valid_fictional_projects()
    {
        List<ProjectDescription> projectDescriptions = await EmbeddingTestData.LoadAsync();
        SearchInputsValidator.Validate(projectDescriptions, "experience building containerized APIs", 5);

        Assert.Equal(12, projectDescriptions.Count);
        Assert.Contains(projectDescriptions,
            projectDescription => projectDescription.IdString == "P01" && projectDescription.SkillStrings.Contains("Docker") && projectDescription.SkillStrings.Contains("ASP.NET Core"));
        Assert.Contains(projectDescriptions,
            projectDescription => projectDescription.IdString == "P06" && projectDescription.SkillStrings.Contains("Proofreading"));
        Assert.Contains(projectDescriptions,
            projectDescription => projectDescription.IdString == "P11" && projectDescription.SkillStrings.Contains("Spreadsheets"));
    }
}
