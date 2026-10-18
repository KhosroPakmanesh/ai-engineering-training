// Store the selected provider's settings, but leave its API key out of ToString().
public sealed record ProviderSettings(string Name, string Key, string Model, Uri Endpoint)
{
    public override string ToString() => $"{Name}: {Model}";
}
