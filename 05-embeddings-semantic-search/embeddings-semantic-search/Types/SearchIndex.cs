// One index belongs to one provider, model, and vector size.
public sealed record SearchIndex(string Provider, string Model, int Dimensions, List<IndexedProject> Projects);
