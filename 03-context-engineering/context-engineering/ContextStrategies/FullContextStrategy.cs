public static class FullContextStrategy
{
    public const string StrategyDescriptionString = "All sections in document order; reject if the context exceeds the character cap.";
    // Give the model every project section in the same order as the source document.
    public static ContextStrategy Prepare(List<ProjectSection> projectSections,
        int characterCapInt = ContextCreatorHelpers.ContextCharacterCapInt)
    {
        if (characterCapInt <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(characterCapInt));
        }

        return ContextCreator.CreateContext(projectSections, "FULL", characterCapInt);
    }
}
