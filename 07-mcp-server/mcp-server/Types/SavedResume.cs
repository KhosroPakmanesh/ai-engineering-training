using System.Text.Json.Serialization;

namespace ResumeMcpServer;

// Returning only the chosen filename avoids exposing unrelated server directories.
public sealed record SavedResume([property: JsonPropertyName("filename")] string FilenameString);
