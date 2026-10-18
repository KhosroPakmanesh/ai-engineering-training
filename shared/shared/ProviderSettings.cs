// Keep the selected provider's connection values together without exposing its key in diagnostics.
public sealed record ProviderSettings(string Name, string Key, string Model, Uri Endpoint)
{
    public override string ToString() => $"{Name}: {Model}";
}
