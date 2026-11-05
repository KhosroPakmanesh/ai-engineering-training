using System.Text.Json.Serialization;

// Pair an original project description with its normalized embedding vector.
public sealed record IndexedProject(
    [property: JsonPropertyName("project")] ProjectDescription ProjectDescription,
    [property: JsonPropertyName("vector")] List<float> VectorFloats);
