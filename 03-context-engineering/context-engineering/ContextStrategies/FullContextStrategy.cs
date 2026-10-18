public static class FullContextStrategy
{
    public const string StrategyDescription = "All sections in document order; reject if the context exceeds the character cap.";
    // Give the model every project section in the same order as the source document.
    public static ContextStrategy Prepare(List<ProjectSection> sections,
        int characterCap = ContextCreatorHelpers.ContextCharacterCap)
    {
        if (characterCap <= 0) throw new ArgumentOutOfRangeException(nameof(characterCap));
        return ContextCreator.CreateContext(sections, "FULL", characterCap);
    }
}
