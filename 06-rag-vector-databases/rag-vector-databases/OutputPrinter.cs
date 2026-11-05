using Microsoft.Extensions.AI;
using static ConsoleOutput;

namespace ResumeRag;

public static class OutputPrinter
{
    // Explain costs and boundaries before the first remote request.
    public static void PrintIntroduction(
        ProviderSettings embeddingProviderSettings,
        ProviderSettings chatProviderSettings,
        string storeString,
        List<SourceDocument> sourceDocuments,
        List<RagQuestion> ragQuestions,
        int chunksInt,
        int changedInt,
        int topKInt,
        double minimumScoreDouble)
    {
        PrintHeading("RAG: retrieve passages, then answer with citations");
        Console.WriteLine("Retrieval-augmented generation (RAG) finds source text before asking an AI to answer.\n" +
            "Follow each question through retrieval, evidence checks, and an answer with source citations.");
        PrintColoredLine(
            $"\nStore: {storeString}\n" +
            $"Provider: {chatProviderSettings.NameString}\n" +
            $"Chat model: {chatProviderSettings.ModelString}\n" +
            $"Embedding model: {embeddingProviderSettings.ModelString}",
            ConsoleColor.Gray);

        PrintHeading("\nSource documents and questions");
        Console.WriteLine(
            $"Inputs: {sourceDocuments.Count} fictional documents; {chunksInt} chunks; {changedInt} need embeddings.");
        Console.WriteLine(
            $"Sources: {string.Join(", ", sourceDocuments.Select(sourceDocument => sourceDocument.FilenameString))}");
        for (int indexInt = 0; indexInt < ragQuestions.Count; indexInt++)
        {
            PrintColoredLine(
                $"  {indexInt + 1}. {ragQuestions[indexInt].TextString}",
                ConsoleColor.Cyan);
        }

        PrintHeading("\nHow to read the retrieved passages");
        Console.WriteLine($"Top-k: retrieve up to {topKInt} passages per question.");
        PrintColoredLine(
            $"COSINE: minimum score {minimumScoreDouble}; higher scores mean closer vector directions.\n" +
            "Scores range from -1 to 1. They are not probabilities or proof of relevance.",
            ConsoleColor.DarkYellow);
        Console.WriteLine(
            "INCLUDED: the passage passed the score and skill-mention checks and fits in the answer context.\n" +
            "EXCLUDED: the passage is shown for inspection but is not sent to the agent.\n" +
            "Read the full passages to judge whether they support the question. No admitted passages means no chat call.");

        PrintHeading("\nModel-call plan");
        PrintColoredLine(
            $"Document embeddings: {(changedInt > 0 ? 1 : 0)} batch for new or changed passages.\n" +
            "Question embeddings: 1 batch for all questions.\n" +
            $"Answers: 0-{ragQuestions.Count} independent chat calls, depending on admitted evidence.\n" +
            $"Each model call: {AiClientFactory.RequestTimeoutSeconds}s total; at most {AiClientFactory.MaxRetries} HTTP retries.\n" +
            "Document text, questions, and answer context go to the selected remote provider and may incur charges.",
            ConsoleColor.Gray);
    }

    // Missing provider measurements remain unknown rather than being reported as zero.
    public static void PrintUsage(
        string stageString,
        UsageDetails? usageDetails)
    {
        PrintColoredLine(
            $"\n{stageString} usage: input tokens={usageDetails?.InputTokenCount?.ToString() ?? "unavailable"}; " +
            $"output tokens={usageDetails?.OutputTokenCount?.ToString() ?? "unavailable"}",
            ConsoleColor.Gray);
    }

    // Show retrieval separately before any answer is requested.
    public static void PrintRetrieval(PreparedAnswer preparedAnswer)
    {
        PrintHeading($"\nRetrieved passages: {preparedAnswer.RagQuestion.TextString}");
        Console.WriteLine(
            $"Retrieved: {preparedAnswer.CandidateRetrievedChunks.Count}; included in answer context: {preparedAnswer.SelectedRetrievedChunks.Count}.");
        for (int indexInt = 0; indexInt < preparedAnswer.CandidateRetrievedChunks.Count; indexInt++)
        {
            RetrievedChunk candidateRetrievedChunk = preparedAnswer.CandidateRetrievedChunks[indexInt];
            bool includedBool = preparedAnswer.SelectedRetrievedChunks.Any(selectedRetrievedChunk =>
                selectedRetrievedChunk.DocumentChunk.ChunkIdString == candidateRetrievedChunk.DocumentChunk
                .ChunkIdString);
            PrintColoredLine(
                $"\n── Candidate {indexInt + 1} ──────────────────────────",
                ConsoleColor.White);
            PrintColoredLine(
                $"SOURCE  | [{candidateRetrievedChunk.DocumentChunk.ChunkIdString}]",
                ConsoleColor.Cyan);
            PrintColoredLine(
                $"COSINE  | {candidateRetrievedChunk.ScoreDouble:F4}",
                ConsoleColor.DarkYellow);
            PrintColoredLine(
                $"CONTEXT | {(includedBool ? "INCLUDED in the answer context" : "EXCLUDED from the answer context")}",
                includedBool ? ConsoleColor.Cyan : ConsoleColor.DarkYellow);
            Console.WriteLine(
                $"Heading: {candidateRetrievedChunk.DocumentChunk.HeadingString}\nPassage:\n{candidateRetrievedChunk.DocumentChunk.TextString}");
        }

        if (preparedAnswer.SelectedRetrievedChunks.Count == 0)
        {
            PrintColoredLine(
                "\nEVIDENCE CHECK: no passages admitted. Chat will be skipped for this question.",
                ConsoleColor.DarkYellow);
        }
        else
        {
            PrintColoredLine(
                $"\nEVIDENCE CHECK: admitted passage count = {preparedAnswer.SelectedRetrievedChunks.Count}. Read them to check their meaning.",
                ConsoleColor.Cyan);
        }
    }

    // Evidence decisions now give an exact chat plan, while the introduction could only show an upper bound.
    public static void PrintChatPlan(
        IndexMetadata indexMetadata,
        int callsInt)
    {
        PrintHeading("\nIndex and answer request details");
        PrintColoredLine(
            $"Stored embedding dimensions: {indexMetadata.DimensionsInt} numbers per vector.\n" +
            $"Chat plan after retrieval: {callsInt} independent model calls; at most {callsInt * (AiClientFactory.MaxRetries + 1)} HTTP attempts.",
            ConsoleColor.Gray);
        Console.WriteLine("Each question with admitted passages gets its own conversation. Other questions skip chat.");
    }

    // Accepted results already passed citation checks; the printer owns only their presentation.
    public static void PrintResults(
        List<RagResult> ragResults,
        string outputPathString)
    {
        PrintHeading("\nAnswers and citation checks");
        Console.WriteLine(
            "Match each answer's citation IDs to the retrieved passages above, then check the claims yourself.");
        for (int indexInt = 0; indexInt < ragResults.Count; indexInt++)
        {
            RagResult ragResult = ragResults[indexInt];
            PrintColoredLine(
                $"\n── Answer {indexInt + 1} ─────────────────────────────",
                ConsoleColor.White);
            PrintColoredLine(
                $"QUESTION | {ragResult.QuestionString}",
                ConsoleColor.Cyan);
            PrintColoredLine(
                $"STATUS   | {ragResult.StatusString}",
                ragResult.StatusString == "answered" ? ConsoleColor.Cyan : ConsoleColor.DarkYellow);
            Console.WriteLine($"Answer:\n{ragResult.AnswerString}");

            if (ragResult.Citations.Count == 0)
            {
                PrintColoredLine(
                    "Citations: none; the answer reports insufficient evidence.",
                    ConsoleColor.DarkYellow);
            }
            else
            {
                Console.WriteLine("Citations:");
                foreach (Citation citation in ragResult.Citations)
                {
                    PrintColoredLine(
                        $"  [{citation.ChunkIdString}] -> {citation.SourceString}",
                        ConsoleColor.Cyan);
                }

                PrintColoredLine(
                    "Citation check: passed; every cited ID belongs to a passage supplied to this answer.",
                    ConsoleColor.Gray);
            }
        }

        PrintHeading("\nSaved results");
        PrintColoredLine(
            $"JSON: {outputPathString}",
            ConsoleColor.Gray);
    }

    // A valid source ID and a skill mention do not prove every generated claim follows from the text.
    public static void PrintTakeaway()
    {
        PrintHeading("\nWhat to take away");
        PrintColoredLine(
            "RETRIEVE: embeddings find passages that may help answer the question.",
            ConsoleColor.DarkYellow);
        PrintColoredLine(
            "ANSWER: the agent receives only passages admitted by the evidence checks.",
            ConsoleColor.Cyan);
        Console.WriteLine(
            "Citation checks confirm that cited IDs were supplied to the agent. They do not prove every claim.\n" +
            "Scores are not probabilities. The skill-mention guard does not understand negation or prove experience.");
        PrintHeading("\nTry changing an input");
        Console.WriteLine("Edit SampleData/project-a.md, rebuild, and run again. Which chunks need new embeddings,\n" +
            "which passages enter the context, and does the answer still match its sources?\n" +
            "Change the backend constant in Program.cs to try the same flow with Qdrant.");
    }
}
