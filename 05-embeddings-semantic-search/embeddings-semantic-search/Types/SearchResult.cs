// One index belongs to one provider, model, and vector size.
public sealed record SearchResult(int Rank, string DocumentId, double Score, string Snippet);
