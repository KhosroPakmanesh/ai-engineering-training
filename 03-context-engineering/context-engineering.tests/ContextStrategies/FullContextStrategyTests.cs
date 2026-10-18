using Xunit;

public class FullContextStrategyTests
{
    // The full baseline must reject excess text rather than silently remove evidence.
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
