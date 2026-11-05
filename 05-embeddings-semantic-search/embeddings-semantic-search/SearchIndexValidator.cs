using System.Runtime.InteropServices;

public static class SearchIndexValidator
{
    // A stored file is another input boundary; check its metadata and normalized vectors when reading it.
    public static void Validate(SearchIndex searchIndex)
    {
        if (string.IsNullOrWhiteSpace(searchIndex.ProviderString) || string.IsNullOrWhiteSpace(searchIndex.ModelString)
            ||
            searchIndex.DimensionsInt <= 0 || searchIndex.IndexedProjects.Count == 0)
        {
            throw new InvalidDataException(
                "The search index needs a provider, model, positive dimension, and projects.");
        }

        var idStringsHashSet = new HashSet<string>(StringComparer.Ordinal);
        foreach (IndexedProject indexedProject in searchIndex.IndexedProjects)
        {
            if (string.IsNullOrWhiteSpace(indexedProject.ProjectDescription.IdString)
                || !idStringsHashSet.Add(indexedProject.ProjectDescription.IdString) ||
                string.IsNullOrWhiteSpace(indexedProject.ProjectDescription.TextString))
            {
                throw new InvalidDataException("Indexed projects must have unique IDs and nonempty source text.");
            }

            VectorValidator.Validate(
                CollectionsMarshal.AsSpan(indexedProject.VectorFloats),
                searchIndex.DimensionsInt);

            double squaredLengthDouble = indexedProject.VectorFloats.Sum(valueFloat => (double)valueFloat * valueFloat);
            if (Math.Abs(squaredLengthDouble - 1) > 0.00001)
            {
                throw new InvalidDataException("Stored embedding vectors must be normalized to length one.");
            }
        }
    }

    // Equal dimensions alone do not make vectors from different providers or models comparable.
    public static void ValidateCompatibility(
        SearchIndex searchIndex,
        string providerString,
        string modelString,
        ReadOnlySpan<float> queryVectorReadOnlySpan)
    {
        if (!string.Equals(
            searchIndex.ProviderString,
            providerString,
            StringComparison.Ordinal) ||
            !string.Equals(
                searchIndex.ModelString,
                modelString,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The query and search index must use the same provider and embedding model. Rebuild the index.");
        }

        if (queryVectorReadOnlySpan.Length != searchIndex.DimensionsInt)
        {
            throw new InvalidDataException("The query vector dimensions do not match the search index.");
        }
    }
}
