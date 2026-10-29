using System.Text.Json.Serialization;

namespace ResumeRag;

// Keep the original JSON field names while code members show their full data types.
public sealed record IndexMetadata(
    [property: JsonPropertyName("Provider")] string ProviderString,
    [property: JsonPropertyName("EmbeddingModel")] string EmbeddingModelString,
    [property: JsonPropertyName("Dimensions")] int DimensionsInt,
    [property: JsonPropertyName("ChunkSize")] int ChunkSizeInt,
    [property: JsonPropertyName("Overlap")] int OverlapInt,
    [property: JsonPropertyName("FormatVersion")] int FormatVersionInt = 1);
