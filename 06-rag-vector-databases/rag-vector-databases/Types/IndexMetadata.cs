using System.Text.Json.Serialization;

namespace ResumeRag;
// Provider, model, vector size, and chunking settings identify compatible stored vectors.
public sealed record IndexMetadata(
    [property: JsonPropertyName("Provider")] string ProviderString,
    [property: JsonPropertyName("EmbeddingModel")] string EmbeddingModelString,
    [property: JsonPropertyName("Dimensions")] int DimensionsInt,
    [property: JsonPropertyName("ChunkSize")] int ChunkSizeInt,
    [property: JsonPropertyName("Overlap")] int OverlapInt,
    [property: JsonPropertyName("FormatVersion")] int FormatVersionInt = 1);
