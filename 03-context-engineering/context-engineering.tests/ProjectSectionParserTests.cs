using Xunit;

public class ProjectSectionParserTests
{
    // Each project needs a unique ID so an answer's reference points to only one project.
    [Fact]
    public void Duplicate_section_ids_are_rejected()
    {
        Assert.Throws<InvalidDataException>(() => ProjectSectionParser.ParseProjectSection("## [P1] One\nText\n## [P1] Two\nMore text"));
    }
}
