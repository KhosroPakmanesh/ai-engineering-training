public static class FullContextStrategy
{
    public const string StrategyDescription = "All sections in document order; reject if the context exceeds the character cap.";
    // The full baseline keeps every section in its original document order.
    public static ContextStrategy Prepare(List<ProjectSection> sections,
        int characterCap = ContextCreatorHelpers.ContextCharacterCap)
    {
        if (characterCap <= 0) throw new ArgumentOutOfRangeException(nameof(characterCap));
        return ContextCreator.CreateContext(sections, "FULL", characterCap);
    }
}
