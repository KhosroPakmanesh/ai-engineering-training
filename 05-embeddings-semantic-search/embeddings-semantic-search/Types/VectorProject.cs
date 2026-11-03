// Keep each stored vector beside its source text and embedding model metadata.
public sealed class VectorProject
{
    public required string IdString
    {
        get; set;
    }
    public required string TextString
    {
        get; set;
    }
    public required string ProviderString
    {
        get; set;
    }
    public required string ModelString
    {
        get; set;
    }
    public ReadOnlyMemory<float> VectorReadOnlyMemory
    {
        get; set;
    }
}
