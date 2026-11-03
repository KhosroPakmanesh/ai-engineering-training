using System.Text.Json.Serialization;

// A source filename and an exact resume quote support extracted facts.
public sealed record Evidence([property: JsonPropertyName("source")] string SourceString,
    [property: JsonPropertyName("quote")] string QuoteString);
