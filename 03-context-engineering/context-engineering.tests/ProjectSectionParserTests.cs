using Xunit;

public class ProjectSectionParserTests
{
    // Repeated document IDs cannot provide an unambiguous citation target.
    [Fact]
    public void Duplicate_section_ids_are_rejected()
    {
        Assert.Throws<InvalidDataException>(() => ProjectSectionParser.ParseProjectSection("## [P1] One\nText\n## [P1] Two\nMore text"));
    }
}
