using Microsoft.Extensions.AI;

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
        indexedProjects.Add(
            new(
                projects[index], 
                VectorNormalizer.Normalize(documentEmbeddings[index].Vector.Span))
            );

    // The local index uses the same model and vector dimensions as the remote embedding service.
    var searchIndex = new SearchIndex(settings.Name, settings.Model, 
        documentEmbeddings[0].Dimensions, indexedProjects);

    // 4. Save, reload, and validate the complete SQLite index.
    // Replace the collection so changed or removed projects cannot leave stale vectors.
    SearchIndexValidator.Validate(searchIndex);

    // Store document vectors locally; the query still needs its own embedding request.
    string indexPath = Path.Combine(folder, "projects.sqlite");
    SqliteSearchIndex.Replace(indexPath, searchIndex);

    // Read the stored index and validate it before semantic search.
    SearchIndex storedIndex = SqliteSearchIndex.Read(indexPath);
    SearchIndexValidator.Validate(storedIndex);

    // 5. Request and validate the query embedding.
    GeneratedEmbeddings<Embedding<float>> queryEmbeddings = 
        await ProjectEmbedder.RequestAsync(generator, queryTexts);

    // Check model, count, and vectors before normalization; shared plumbing has checked and ordered wire indexes.
    EmbeddingResponseValidator.Validate(queryEmbeddings, 1, settings.Model);

    // Read the one vector returned for the query.
    Embedding<float> queryEmbedding = queryEmbeddings[0];

    // 6. Check compatibility and normalize the query vector.
    // The query and stored documents must use the same provider, model, and vector size.
    SearchIndexValidator.ValidateCompatibility
        (storedIndex, settings.Name, queryEmbedding.ModelId!, queryEmbedding.Vector.Span);

    // Normalize the query vector to length one for cosine similarity comparison with the stored vectors.
    List<float> normalizedQuery = VectorNormalizer.Normalize(queryEmbedding.Vector.Span);

    // 7. Rank the stored projects by cosine similarity to the normalized query vector.
    List<SearchResult> semanticResults = SemanticSearch.Rank(storedIndex, normalizedQuery, topK);

    //------------------------------------------------------------------------//    
    //                         RESULTS AND COMPARISON                         //
    //------------------------------------------------------------------------//

    // 1. Show the query, index details, and the limits of comparing the score scales.
    OutputPrinter.PrintComparisonGuide(query);

    // 2. Show both rankings, starting at rank 1, with excerpts for the learner to judge.
    Console.WriteLine("\nRESULTS: each method starts at rank 1; excerpts show up to 90 characters.");
    for (int index = 0; index < semanticResults.Count; index++)
    {
        SearchResult semantic = semanticResults[index];
        SearchResult lexical = lexicalResults[index];
        OutputPrinter.PrintRankComparison(semantic, lexical);
    }
    // 3. Report input token counts when the provider supplies them.
    OutputPrinter.PrintIndexDetails(storedIndex, indexPath, documentEmbeddings.Usage, queryEmbeddings.Usage);
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
