using System.Text.Json.Serialization;

// One stored index belongs to one provider, model, and vector dimension.
public sealed record SearchIndex([property: JsonPropertyName("provider")] string ProviderString,
    [property: JsonPropertyName("model")] string ModelString, [property: JsonPropertyName("dimensions")] int DimensionsInt,
    [property: JsonPropertyName("projects")] List<IndexedProject> IndexedProjects);
