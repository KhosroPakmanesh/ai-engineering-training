using System.Runtime.InteropServices;

public static class SearchIndexValidator
{
    // A stored file is another input boundary; check its metadata and normalized vectors when reading it.
    public static void Validate(SearchIndex searchIndex)
    {
        if (string.IsNullOrWhiteSpace(searchIndex.Provider) || string.IsNullOrWhiteSpace(searchIndex.Model) ||
            searchIndex.Dimensions <= 0 || searchIndex.Projects.Count == 0)
            throw new InvalidDataException("The search index needs a provider, model, positive dimension, and projects.");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (IndexedProject indexedProject in searchIndex.Projects)
        {
            if (string.IsNullOrWhiteSpace(indexedProject.Project.Id) || !ids.Add(indexedProject.Project.Id) ||
                string.IsNullOrWhiteSpace(indexedProject.Project.Text))
                throw new InvalidDataException("Indexed projects must have unique IDs and nonempty source text.");

            VectorValidator.Validate(CollectionsMarshal.AsSpan(indexedProject.Vector), searchIndex.Dimensions);

            double squaredLength = indexedProject.Vector.Sum(value => (double)value * value);
            if (Math.Abs(squaredLength - 1) > 0.00001)
                throw new InvalidDataException("Stored embedding vectors must be normalized to length one.");
        }
    }

    // Equal dimensions alone do not make vectors from different providers or models comparable.
    public static void ValidateCompatibility(SearchIndex searchIndex, string provider, string model, ReadOnlySpan<float> queryVector)
    {
        if (!string.Equals(searchIndex.Provider, provider, StringComparison.Ordinal) ||
            !string.Equals(searchIndex.Model, model, StringComparison.Ordinal))
            throw new InvalidDataException("The query and search index must use the same provider and embedding model. Rebuild the index.");

        if (queryVector.Length != searchIndex.Dimensions)
            throw new InvalidDataException("The query vector dimensions do not match the search index.");
    }
}
