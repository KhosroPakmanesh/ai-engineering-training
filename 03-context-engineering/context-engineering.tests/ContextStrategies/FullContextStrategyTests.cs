using Xunit;

public class FullContextStrategyTests
{
    // Stop when the full context is too long; do not remove project text to make it fit.
    [Fact]
    public void Context_is_accepted_at_the_cap_and_rejected_above_it()
    {
        List<ProjectSection> sections = ContextTestData.ReadSections();
        ContextStrategy context = FullContextStrategy.Prepare(sections);
        ContextStrategy atCap = FullContextStrategy.Prepare(sections, context.Text.Length);

        Assert.Equal(context.Text, atCap.Text);
        Assert.Throws<InvalidDataException>(() =>
            FullContextStrategy.Prepare(sections, context.Text.Length - 1));
    }
}
