using System.Text.Json.Serialization;

// Store the selected provider's settings, but leave its API key out of ToString().
public sealed record ProviderSettings(
    [property: JsonPropertyName("name")] string NameString,
    [property: JsonPropertyName("key")] string KeyString,
    [property: JsonPropertyName("model")] string ModelString,
    [property: JsonPropertyName("endpoint")] Uri EndpointUri)
{
    public override string ToString()
    {
        return $"{NameString}: {ModelString}";
    }
}
