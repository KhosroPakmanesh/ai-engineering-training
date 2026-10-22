using Xunit;

public class ProjectDescriptionsLoaderTests
{
    // The complete copied corpus includes relevant work, a keyword trap, and unrelated controls.
    [Fact]
    public async Task Copied_sample_contains_twelve_valid_fictional_projects()
    {
        List<ProjectDescription> projects = await EmbeddingTestData.LoadAsync();
        SearchInputsValidator.Validate(projects, "experience building containerized APIs", 5);

        Assert.Equal(12, projects.Count);
        Assert.Contains(projects, project => project.Id == "P01" && project.Skills.Contains("Docker") && project.Skills.Contains("ASP.NET Core"));
        Assert.Contains(projects, project => project.Id == "P06" && project.Skills.Contains("Proofreading"));
        Assert.Contains(projects, project => project.Id == "P11" && project.Skills.Contains("Spreadsheets"));
    }
}
