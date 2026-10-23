using CommunityToolkit.VectorData.SqliteVec;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using VectorProject = SemanticSearch.VectorProject;

const string query = "experience building containerized APIs";
const int topK = 5;

try
{
    //------------------------------------------------------------------------//
    //                              Preparation                               //
    //------------------------------------------------------------------------//

    string folder = AppContext.BaseDirectory;

    // Shared preparation: check all inputs and prepare both batches before the first paid request.
    List<ProjectDescription> projects = await ProjectDescriptionsLoader
        .LoadAsync(Path.Combine(folder, "SampleData", "projects.json"));

    // Validate the query, top-k, and each project before sending any paid requests.
    SearchInputsValidator.Validate(projects, query, topK);

    // The local index uses only the original project text, not the skills or other fields.
    List<string> documentTexts = projects.Select(project => project.Text).ToList();
    List<string> queryTexts = [query];

    //------------------------------------------------------------------------//
    //                            KEYWORD SEARCH                              //
    //------------------------------------------------------------------------//

    // 1. Rank the original project text by matching query words.
    List<SearchResult> lexicalResults = LexicalSearch.Rank(projects, query, topK);

    //------------------------------------------------------------------------//
    //                            SEMANTIC SEARCH                             //
    //------------------------------------------------------------------------//

    // 1. Read settings and create the embedding generator.
    // Embeddings use a separate model setting, leaving the four chat lessons unchanged.
    ProviderSettings settings = await SettingsReader
        .ReadSettingsAsync(folder, useEmbeddingModel: true);

    // Keep provider connections in the shared factory.
    var factory = new AiClientFactory();

    // Use Microsoft's embedding interface with the separately configured embedding model.
    using IEmbeddingGenerator<string, Embedding<float>> generator =
        factory.CreateEmbeddingGenerator(settings);
    OutputPrinter.PrintIntroduction(query, projects.Count, topK, settings);

    // 2. Request and validate document embeddings before accepting their vectors.
    GeneratedEmbeddings<Embedding<float>> documentEmbeddings =
        await ProjectEmbedder.RequestAsync(generator, documentTexts);

    // Check model, count, and vectors before normalization; shared plumbing has checked and ordered wire indexes.
    EmbeddingResponseValidator.Validate(documentEmbeddings, projects.Count, settings.Model);

    // 3. Normalize document vectors and build the index with their original project text.
    var indexedProjects = new List<IndexedProject>();
    for (int index = 0; index < projects.Count; index++)
    {
        List<float> normalizedVector = VectorNormalizer.Normalize(documentEmbeddings[index].Vector.Span);
        indexedProjects.Add(new IndexedProject(projects[index], normalizedVector));
    }

    // The local index uses the same model and vector dimensions as the remote embedding service.
    var searchIndex = new SearchIndex(settings.Name, settings.Model,
        documentEmbeddings[0].Dimensions, indexedProjects);

    // 4. Save, reload, and validate the complete SQLite index.
    // Keep this validated snapshot as the source for the vector collection.
    SearchIndexValidator.Validate(searchIndex);

    // Store document vectors locally; the query still needs its own embedding request.
    string indexPath = Path.Combine(folder, "projects.sqlite");
    SqliteSearchIndex.Replace(indexPath, searchIndex);

    // Read the stored index and validate it before semantic search.
    SearchIndex storedIndex = SqliteSearchIndex.Read(indexPath);
    SearchIndexValidator.Validate(storedIndex);

    // 5. Open the SQLite vector store.
    // This separate database uses the connector's tables to store and search vectors.
    string vectorIndexPath = Path.Combine(folder, "project-vectors.sqlite");
    var connectionSettings = new SqliteConnectionStringBuilder
    {
        DataSource = vectorIndexPath,
        Pooling = false
    };
    using var vectorStore = new SqliteVectorStore(connectionSettings.ToString());

    // 6. Define the collection's key, metadata, vector size, and distance calculation.
    // Map the existing VectorProject type using the vector size returned by the model.
    var definition = new VectorStoreCollectionDefinition
    {
        Properties =
        [
            new VectorStoreKeyProperty(nameof(VectorProject.Id), typeof(string)),
            new VectorStoreDataProperty(nameof(VectorProject.Text), typeof(string)),
            new VectorStoreDataProperty(nameof(VectorProject.Provider), typeof(string)),
            new VectorStoreDataProperty(nameof(VectorProject.Model), typeof(string)),
            new VectorStoreVectorProperty(
                nameof(VectorProject.Vector), typeof(ReadOnlyMemory<float>), storedIndex.Dimensions)
            {
                // Rank converts this connector's cosine distance into similarity with 1 - distance.
                DistanceFunction = DistanceFunction.CosineDistance
            }
        ]
    };
    using VectorStoreCollection<string, VectorProject> collection =
        vectorStore.GetCollection<string, VectorProject>("projects", definition);

    // 7. Rebuild the vector collection before uploading the current projects.
    // Removed projects and changed vector sizes must not leave stale records.
    // Unlike SqliteSearchIndex.Replace, deleting and uploading are separate operations, not one transaction.
    await collection.EnsureCollectionDeletedAsync();
    await collection.EnsureCollectionExistsAsync();

    // 8. Upload the project text, metadata, and already generated embeddings.
    // Upsert adds or replaces each record by its ID, without another model call.
    var records = new List<VectorProject>();
    foreach (IndexedProject indexedProject in storedIndex.Projects)
    {
        records.Add(new VectorProject
        {
            Id = indexedProject.Project.Id,
            Text = indexedProject.Project.Text,
            Provider = storedIndex.Provider,
            Model = storedIndex.Model,
            Vector = indexedProject.Vector.ToArray()
        });
    }
    await collection.UpsertAsync(records);

    // 9. Request and validate the query embedding.
    GeneratedEmbeddings<Embedding<float>> queryEmbeddings =
        await ProjectEmbedder.RequestAsync(generator, queryTexts);

    // Check model, count, and vectors before normalization; shared plumbing has checked and ordered wire indexes.
    EmbeddingResponseValidator.Validate(queryEmbeddings, 1, settings.Model);

    // Read the one vector returned for the query.
    Embedding<float> queryEmbedding = queryEmbeddings[0];

    // 10. Check compatibility and normalize the query vector.
    // The query and stored documents must use the same provider, model, and vector size.
    SearchIndexValidator.ValidateCompatibility
        (storedIndex, settings.Name, queryEmbedding.ModelId!, queryEmbedding.Vector.Span);

    // Normalize the query vector to length one for cosine similarity comparison with the stored vectors.
    List<float> normalizedQuery = VectorNormalizer.Normalize(queryEmbedding.Vector.Span);

    // 11. Rank the stored projects through the vector-store search overload.
    // SearchAsync inside this overload asks SQLite to compare vectors and select the nearest records.
    // The array supplies the ReadOnlyMemory<float> required by the vector-store API.
    List<SearchResult> semanticResults = await SemanticSearch.RankAsync(
        collection, storedIndex, normalizedQuery.ToArray(), topK);

    //------------------------------------------------------------------------//
    //                         RESULTS AND COMPARISON                         //
    //------------------------------------------------------------------------//

    // 1. Show the query, index details, and the limits of comparing the score scales.
    OutputPrinter.PrintComparisonGuide(query);

    // 2. Show both rankings, starting at rank 1, with excerpts for the learner to judge.
    OutputPrinter.PrintResultsHeading();
    for (int index = 0; index < semanticResults.Count; index++)
    {
        SearchResult semantic = semanticResults[index];
        SearchResult lexical = lexicalResults[index];
        OutputPrinter.PrintRankComparison(semantic, lexical);
    }
    // 3. Report input token counts when the provider supplies them.
    OutputPrinter.PrintIndexDetails(storedIndex, vectorIndexPath, documentEmbeddings.Usage, queryEmbeddings.Usage);
    OutputPrinter.PrintTakeaway();

    return 0;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("The request was canceled or exceeded the 120-second time limit.");
    return 1;
}
catch (Exception error)
{
    Console.Error.WriteLine(error.Message);
    return 1;
}
