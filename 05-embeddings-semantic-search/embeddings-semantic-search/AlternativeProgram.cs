using CommunityToolkit.VectorData.SqliteVec;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;

const string queryString = "experience building containerized APIs";
const int topKInt = 5;

try
{
    //------------------------------------------------------------------------//
    //                              Preparation                               //
    //------------------------------------------------------------------------//

    string folderString = AppContext.BaseDirectory;

    // 1. Load all source descriptions before preparing the comparison.
    List<ProjectDescription> projectDescriptions = await ProjectDescriptionsLoader
        .LoadAsync(Path.Combine(
            folderString,
            "SampleData",
            "projects.json"));

    // 2. Validate the query, top-k, and every project before paid work.
    SearchInputsValidator.Validate(
        projectDescriptions,
        queryString,
        topKInt);

    // 3. Prepare both batches from the unchanged original text.
    List<string> documentTextStrings = projectDescriptions.Select(projectDescription =>
        projectDescription.TextString).ToList();
    List<string> queryTextStrings = [queryString];

    //------------------------------------------------------------------------//
    //                            KEYWORD SEARCH                              //
    //------------------------------------------------------------------------//

    // 1. Rank the original project text by matching query words.
    List<SearchResult> lexicalSearchResults = LexicalSearch.Rank(
        projectDescriptions,
        queryString,
        topKInt);

    //------------------------------------------------------------------------//
    //                            SEMANTIC SEARCH                             //
    //------------------------------------------------------------------------//

    // 1. Read settings and create the embedding generator.
    // Embeddings use a separate model setting, leaving the four chat lessons unchanged.
    ProviderSettings providerSettings = await SettingsReader
        .ReadSettingsAsync(
            folderString,
            useEmbeddingModelBool: true);

    // Keep provider connections in the shared factory.
    var aiClientFactory = new AiClientFactory();

    // Use Microsoft's embedding interface with the separately configured embedding model.
    using IEmbeddingGenerator<string, Embedding<float>> iEmbeddingGenerator =
        aiClientFactory.CreateEmbeddingGenerator(providerSettings);
    OutputPrinter.PrintIntroduction(
        queryString,
        projectDescriptions.Count,
        topKInt,
        providerSettings);

    // 2. Request and validate document embeddings before accepting their vectors.
    GeneratedEmbeddings<Embedding<float>> documentGeneratedEmbeddings =
        await ProjectEmbedder.RequestAsync(
            iEmbeddingGenerator,
            documentTextStrings);

    // Check model, count, and vectors before normalization; shared plumbing has checked and ordered wire indexes.
    EmbeddingResponseValidator.Validate(
        documentGeneratedEmbeddings,
        projectDescriptions.Count,
        providerSettings.ModelString);

    // 3. Normalize document vectors and build the index with their original project text.
    var indexedProjects = new List<IndexedProject>();
    for (int indexInt = 0; indexInt < projectDescriptions.Count; indexInt++)
    {
        List<float> normalizedVectorFloats =
            VectorNormalizer.Normalize(documentGeneratedEmbeddings[indexInt].Vector.Span);
        indexedProjects.Add(new IndexedProject(
            projectDescriptions[indexInt],
            normalizedVectorFloats));
    }

    // The local index uses the same model and vector dimensions as the remote embedding service.
    var searchIndex = new SearchIndex(
        ProviderString: providerSettings.NameString,
        ModelString: providerSettings.ModelString,
        DimensionsInt: documentGeneratedEmbeddings[0].Dimensions,
        IndexedProjects: indexedProjects);

    // 4. Save, reload, and validate the complete SQLite index.
    // Keep this validated snapshot as the source for the vector collection.
    SearchIndexValidator.Validate(searchIndex);

    // Store document vectors locally; the query still needs its own embedding request.
    string indexPathString = Path.Combine(
        folderString,
        "projects.sqlite");
    SqliteSearchIndex.Replace(
        indexPathString,
        searchIndex);

    // Read the stored index and validate it before semantic search.
    SearchIndex storedSearchIndex = SqliteSearchIndex.Read(indexPathString);
    SearchIndexValidator.Validate(storedSearchIndex);

    // 5. Open the SQLite vector store.
    // This separate database uses the connector's tables to store and search vectors.
    string vectorIndexPathString = Path.Combine(
        folderString,
        "project-vectors.sqlite");
    var connectionSqliteConnectionStringBuilder = new SqliteConnectionStringBuilder
    {
        DataSource = vectorIndexPathString,
        Pooling = false
    };
    using var sqliteVectorStore = new SqliteVectorStore(connectionSqliteConnectionStringBuilder.ToString());

    // 6. Define the collection's key, metadata, vector size, and distance calculation.
    // Map the existing VectorProject type using the vector size returned by the model.
    var vectorStoreCollectionDefinition = new VectorStoreCollectionDefinition
    {
        Properties =
        [
            new VectorStoreKeyProperty(
                nameof(VectorProject.IdString),
                typeof(string)),
            new VectorStoreDataProperty(
                nameof(VectorProject.TextString),
                typeof(string)),
            new VectorStoreDataProperty(
                nameof(VectorProject.ProviderString),
                typeof(string)),
            new VectorStoreDataProperty(
                nameof(VectorProject.ModelString),
                typeof(string)),
            new VectorStoreVectorProperty(
                nameof(VectorProject.VectorReadOnlyMemory),
                typeof(ReadOnlyMemory<float>),
                storedSearchIndex.DimensionsInt)
            {
                // Rank converts this connector's cosine distance into similarity with 1 - distance.
                DistanceFunction = DistanceFunction.CosineDistance
            }
        ]
    };
    using VectorStoreCollection<string, VectorProject> vectorStoreCollection =
        sqliteVectorStore.GetCollection<string, VectorProject>(
            "projects",
            vectorStoreCollectionDefinition);

    // 7. Rebuild the vector collection before uploading the current projects.
    // Removed projects and changed vector sizes must not leave stale records.
    // Unlike SqliteSearchIndex.Replace, deleting and uploading are separate operations, not one transaction.
    await vectorStoreCollection.EnsureCollectionDeletedAsync();
    await vectorStoreCollection.EnsureCollectionExistsAsync();

    // 8. Upload the project text, metadata, and already generated embeddings.
    // Upsert adds or replaces each record by its ID, without another model call.
    var vectorProjects = new List<VectorProject>();
    foreach (IndexedProject indexedProject in storedSearchIndex.IndexedProjects)
    {
        vectorProjects.Add(new VectorProject
        {
            IdString = indexedProject.ProjectDescription.IdString,
            TextString = indexedProject.ProjectDescription.TextString,
            ProviderString = storedSearchIndex.ProviderString,
            ModelString = storedSearchIndex.ModelString,
            VectorReadOnlyMemory = indexedProject.VectorFloats.ToArray()
        });
    }
    await vectorStoreCollection.UpsertAsync(vectorProjects);

    // 9. Request and validate the query embedding.
    GeneratedEmbeddings<Embedding<float>> queryGeneratedEmbeddings =
        await ProjectEmbedder.RequestAsync(
            iEmbeddingGenerator,
            queryTextStrings);

    // Check model, count, and vectors before normalization; shared plumbing has checked and ordered wire indexes.
    EmbeddingResponseValidator.Validate(
        queryGeneratedEmbeddings,
        1,
        providerSettings.ModelString);

    // Read the one vector returned for the query.
    Embedding<float> queryEmbedding = queryGeneratedEmbeddings[0];

    // 10. Check compatibility and normalize the query vector.
    // The query and stored documents must use the same provider, model, and vector size.
    SearchIndexValidator.ValidateCompatibility(
        storedSearchIndex,
        providerSettings.NameString,
        queryEmbedding.ModelId!,
        queryEmbedding.Vector.Span);

    // Normalize the query vector to length one for cosine similarity comparison with the stored vectors.
    List<float> normalizedQueryFloats = VectorNormalizer.Normalize(queryEmbedding.Vector.Span);

    // 11. Rank the stored projects through the vector-store search overload.
    // SearchAsync inside this overload asks SQLite to compare vectors and select the nearest records.
    // The array supplies the ReadOnlyMemory<float> required by the vector-store API.
    List<SearchResult> semanticSearchResults = await SemanticSearch.RankAsync(
        vectorStoreCollection,
        storedSearchIndex,
        normalizedQueryFloats.ToArray(),
        topKInt);

    //------------------------------------------------------------------------//
    //                         RESULTS AND COMPARISON                         //
    //------------------------------------------------------------------------//

    // 1. Show the query, index details, and the limits of comparing the score scales.
    OutputPrinter.PrintComparisonGuide(queryString);

    // 2. Show both rankings, starting at rank 1, with excerpts for the learner to judge.
    OutputPrinter.PrintResultsHeading();
    for (int indexInt = 0; indexInt < semanticSearchResults.Count; indexInt++)
    {
        SearchResult semanticSearchResult = semanticSearchResults[indexInt];
        SearchResult lexicalSearchResult = lexicalSearchResults[indexInt];
        OutputPrinter.PrintRankComparison(
            semanticSearchResult,
            lexicalSearchResult);
    }

    // 3. Report input token counts when the provider supplies them.
    OutputPrinter.PrintIndexDetails(
        storedSearchIndex,
        vectorIndexPathString,
        documentGeneratedEmbeddings.Usage,
        queryGeneratedEmbeddings.Usage);
    OutputPrinter.PrintTakeaway();

    return 0;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("The request was canceled or exceeded the 120-second time limit.");
    return 1;
}
catch (Exception errorException)
{
    Console.Error.WriteLine(errorException.Message);
    return 1;
}
