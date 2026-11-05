using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ResumeRag;
using ResumeRag.Stores;

const StoreBackend selectedStoreBackend = StoreBackend.Sqlite;
const int chunkSizeInt = 650;
const int overlapInt = 80;
const int topKInt = 3;
const double minimumScoreDouble = 0.2;
const int contextLimitInt = 2_400;

try
{
    //------------------------------------------------------------------------//
    //                              PREPARATION                               //
    //------------------------------------------------------------------------//

    // 1. Load and validate every input before any paid work.
    // These questions ask about named skills, so every question includes its skill.
    string folderString = AppContext.BaseDirectory;
    List<RagQuestion> ragQuestions =
    [
        new(
            "Where did I use Docker?",
            "Docker"),
        new(
                "Where did I use PostgreSQL?",
                "PostgreSQL"),
        new(
            "Where did I use Kubernetes?",
            "Kubernetes")
    ];
    List<SourceDocument> sourceDocuments = await DocumentsLoader.LoadAsync(Path.Combine(
        folderString,
        "SampleData"));
    InputsValidator.Validate(
        sourceDocuments,
        ragQuestions,
        chunkSizeInt,
        overlapInt,
        topKInt,
        minimumScoreDouble,
        contextLimitInt);

    // 2. Split the sources into passages and prepare all question texts before requests begin.
    List<DocumentChunk> documentChunks = MarkdownChunker.Chunk(
        sourceDocuments,
        chunkSizeInt,
        overlapInt);
    List<string> questionTextStrings = ragQuestions.Select(ragQuestion => ragQuestion.TextString).ToList();

    // 3. Check both model settings and existing index compatibility before requesting embeddings.
    ProviderSettings embeddingProviderSettings = await SettingsReader.ReadSettingsAsync(
        folderString,
        useEmbeddingModelBool: true);
    ProviderSettings chatProviderSettings = await SettingsReader.ReadSettingsAsync(folderString);
    using IChunkStore selectedIChunkStore = ChunkStoreFactory.Create(
        selectedStoreBackend,
        folderString);
    ChunkIndex? previousChunkIndex = await selectedIChunkStore.ReadAsync();
    if (previousChunkIndex is not null)
    {
        IndexValidator.Validate(previousChunkIndex);
        IndexValidator.ValidateCompatibility(
            previousChunkIndex,
            embeddingProviderSettings.NameString,
            embeddingProviderSettings.ModelString,
            chunkSizeInt,
            overlapInt);
    }

    // 4. Identify passages that need new vectors and explain the model-call plan.
    List<DocumentChunk> changedDocumentChunks = IndexPreparer.FindChanged(
        documentChunks,
        previousChunkIndex);
    List<string> documentTextStrings = changedDocumentChunks
        .Select(documentChunk => documentChunk.HeadingString + "\n" + documentChunk.TextString)
        .ToList();
    OutputPrinter.PrintIntroduction(
        embeddingProviderSettings,
        chatProviderSettings,
        selectedIChunkStore.DescriptionString,
        sourceDocuments,
        ragQuestions,
        documentChunks.Count,
        changedDocumentChunks.Count,
        topKInt,
        minimumScoreDouble);

    //------------------------------------------------------------------------//
    //                           DOCUMENT INDEXING                            //
    //------------------------------------------------------------------------//

    // 1. Request only new or changed passages; keep response checks and normalization separate.
    var aiClientFactory = new AiClientFactory();
    using IEmbeddingGenerator<string, Embedding<float>> iEmbeddingGenerator =
        aiClientFactory.CreateEmbeddingGenerator(embeddingProviderSettings);
    var changedIndexedChunks = new List<IndexedChunk>();
    int dimensionsInt = previousChunkIndex?.IndexMetadata.DimensionsInt ?? 0;
    if (changedDocumentChunks.Count > 0)
    {
        GeneratedEmbeddings<Embedding<float>> documentGeneratedEmbeddings =
            await ChunkEmbedder.RequestAsync(
                iEmbeddingGenerator,
                documentTextStrings);
        EmbeddingValidator.Validate(
            documentGeneratedEmbeddings,
            changedDocumentChunks.Count,
            embeddingProviderSettings.ModelString,
            previousChunkIndex?.IndexMetadata.DimensionsInt);

        dimensionsInt = documentGeneratedEmbeddings[0].Dimensions;
        for (int indexInt = 0; indexInt < changedDocumentChunks.Count; indexInt++)
        {
            changedIndexedChunks.Add(new(
                changedDocumentChunks[indexInt],
                VectorMath.Normalize(documentGeneratedEmbeddings[indexInt].Vector.Span)));
        }

        OutputPrinter.PrintUsage(
            "Document embeddings",
            documentGeneratedEmbeddings.Usage);
    }

    // 2. Merge new and reused vectors, then synchronize the store, including removals.
    var indexMetadata = new IndexMetadata(
        ProviderString: embeddingProviderSettings.NameString,
        EmbeddingModelString: embeddingProviderSettings.ModelString,
        DimensionsInt: dimensionsInt,
        ChunkSizeInt: chunkSizeInt,
        OverlapInt: overlapInt);

    ChunkIndex desiredChunkIndex = IndexPreparer.Merge(
        indexMetadata,
        documentChunks,
        previousChunkIndex,
        changedIndexedChunks);
    IndexValidator.Validate(desiredChunkIndex);
    await selectedIChunkStore.SynchronizeAsync(desiredChunkIndex);

    // 3. Read and validate the actual stored index before using it for retrieval.
    ChunkIndex storedChunkIndex = await selectedIChunkStore.ReadAsync()
        ?? throw new InvalidDataException("The store returned no index after ingestion.");
    IndexValidator.Validate(storedChunkIndex);
    IndexValidator.ValidateCompatibility(
        storedChunkIndex,
        embeddingProviderSettings.NameString,
        embeddingProviderSettings.ModelString,
        chunkSizeInt,
        overlapInt);

    //------------------------------------------------------------------------//
    //                         RETRIEVAL AND CONTEXT                          //
    //------------------------------------------------------------------------//

    // 1. Embed all questions in one batch with the same model and vector size as the documents.
    GeneratedEmbeddings<Embedding<float>> questionGeneratedEmbeddings =
        await ChunkEmbedder.RequestAsync(
            iEmbeddingGenerator,
            questionTextStrings);
    EmbeddingValidator.Validate(
        questionGeneratedEmbeddings,
        ragQuestions.Count,
        embeddingProviderSettings.ModelString,
        storedChunkIndex.IndexMetadata.DimensionsInt);
    OutputPrinter.PrintUsage(
        "Question embeddings",
        questionGeneratedEmbeddings.Usage);

    // 2. Retrieve passages, prepare every bounded context, and print the evidence decisions before chat.
    var preparedAnswers = new List<PreparedAnswer>();
    for (int indexInt = 0; indexInt < ragQuestions.Count; indexInt++)
    {
        List<float> vectorFloats = VectorMath.Normalize(questionGeneratedEmbeddings[indexInt].Vector.Span);
        List<RetrievedChunk> candidateRetrievedChunks = await selectedIChunkStore.SearchAsync(
            vectorFloats,
            topKInt);
        PreparedAnswer preparedAnswer = EvidenceSelector.Prepare(
            ragQuestions[indexInt],
            candidateRetrievedChunks,
            minimumScoreDouble,
            contextLimitInt);
        preparedAnswers.Add(preparedAnswer);
        OutputPrinter.PrintRetrieval(preparedAnswer);
    }

    // 3. Show the exact chat-call plan now that every question's evidence has been checked.
    OutputPrinter.PrintChatPlan(
        storedChunkIndex.IndexMetadata,
        preparedAnswers.Count(preparedAnswer => preparedAnswer.SelectedRetrievedChunks.Count > 0));

    //------------------------------------------------------------------------//
    //                      ANSWERS AND CITATION CHECKS                       //
    //------------------------------------------------------------------------//

    // 1. Use an independent conversation for each question with admitted evidence.
    using IChatClient answerIChatClient = aiClientFactory.CreateClient(chatProviderSettings);
    var ragResults = new List<RagResult>();
    foreach (PreparedAnswer preparedAnswer in preparedAnswers)
    {
        string answerString;
        UsageDetails? usageDetails = null;
        if (preparedAnswer.SelectedRetrievedChunks.Count == 0)
        {
            // Missing evidence ends this question locally, before any chat request.
            answerString = RagAnswerer.InsufficientEvidenceString;
        }
        else
        {
            AgentResponse agentResponse = await RagAnswerer.RequestAsync(
                answerIChatClient,
                preparedAnswer);
            answerString = agentResponse.Messages.Last(chatMessage =>
                chatMessage.Role == ChatRole.Assistant).Text!.Trim();
            usageDetails = agentResponse.Usage;
        }

        // 2. Parse citations and check that every cited passage was supplied to this answer.
        List<string> citedIdStrings = CitationParser.Parse(answerString);
        CitationValidator.Validate(
            answerString,
            citedIdStrings,
            preparedAnswer.SelectedRetrievedChunks);
        List<Citation> citations = citedIdStrings
            .Distinct(StringComparer.Ordinal)
            .Select(idString => new Citation(
                idString,
                preparedAnswer.SelectedRetrievedChunks
                .Single(selectedRetrievedChunk => selectedRetrievedChunk.DocumentChunk.ChunkIdString == idString)
                .DocumentChunk.FilenameString))
            .ToList();

        // 3. Keep accepted answers with all retrieved scores, including excluded passages.
        ragResults.Add(new(
            QuestionString: preparedAnswer.RagQuestion.TextString,
            AnswerString: answerString,
            Citations: citations,
            RetrievalScores: preparedAnswer.CandidateRetrievedChunks
            .Select(retrievedChunk => new RetrievalScore(
                retrievedChunk.DocumentChunk.ChunkIdString,
                retrievedChunk.ScoreDouble))
            .ToList(),
            StatusString: answerString == RagAnswerer
            .InsufficientEvidenceString ? "insufficient_evidence" : "answered"));
        if (preparedAnswer.SelectedRetrievedChunks.Count > 0)
        {
            OutputPrinter.PrintUsage(
                "Chat",
                usageDetails);
        }
    }

    //------------------------------------------------------------------------//
    //                          RESULTS AND TAKEAWAY                          //
    //------------------------------------------------------------------------//

    // 1. Save only accepted results. Generated files live in the ignored build output.
    string outputPathString = Path.Combine(
        folderString,
        "rag-results.json");
    await File.WriteAllTextAsync(
        outputPathString,
        JsonSerializer.Serialize(
            ragResults,
            new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));

    // 2. Show the answers and citation checks, then suggest a concrete input to change.
    OutputPrinter.PrintResults(
        ragResults,
        outputPathString);
    OutputPrinter.PrintTakeaway();
    return 0;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("The request was canceled or exceeded its time limit.");
    return 1;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}
