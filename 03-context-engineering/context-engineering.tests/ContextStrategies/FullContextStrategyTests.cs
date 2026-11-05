using Xunit;

public class FullContextStrategyTests
{
    // Stop when the full context is too long; do not remove project text to make it fit.
    [Fact]
    public void Context_is_accepted_at_the_cap_and_rejected_above_it()
    {
        // Arrange
        List<ProjectSection> projectSections = ContextTestData.ReadSections();

        // Act
        ContextStrategy contextStrategy = FullContextStrategy.Prepare(projectSections);

        ContextStrategy atCapContextStrategy = FullContextStrategy.Prepare(
            projectSections,
            contextStrategy.TextString.Length);

        // Assert
        Assert.Equal(
            contextStrategy.TextString,
            atCapContextStrategy.TextString);
        Assert.Throws<InvalidDataException>(() =>
            FullContextStrategy.Prepare(
                projectSections,
                contextStrategy.TextString.Length - 1));
    }
}
