using Microsoft.Extensions.AI;
using static ConsoleOutput;

public static class OutputPrinter
{
    // Keep the setup explanation separate so the embedding request stages stay easy to follow.
    public static void PrintIntroduction(string queryString, int projectCountInt, int topKInt, ProviderSettings providerSettings)
    {
        PrintHeading("Keyword vs. Semantic Search");
        PrintColoredLine($"QUERY | \"{queryString}\"", ConsoleColor.Cyan);
        Console.WriteLine($"Search the same {projectCountInt} project descriptions; show the top {topKInt} from each method.");
        PrintColoredLine(
            $"Provider: {providerSettings.NameString}\n" +
            $"Embedding model: {providerSettings.ModelString}",
            ConsoleColor.Gray);
        PrintColoredLine(
            "\nPreparing embeddings: two remote calls, one for all descriptions and one for the query.\n" +
            $"Up to {AiClientFactory.MaxRetries} HTTP retries per call. These calls send the fictional text and query to the provider.",
            ConsoleColor.Gray);
    }

    // Explain the two score scales before the learner compares the ranked projects.
    public static void PrintComparisonGuide(string queryString)
    {
        PrintHeading("\nHow to read the comparison");
        Console.WriteLine($"Both searches use the query: \"{queryString}\"");
        PrintColoredLine("KEYWORD: counts distinct query words found in the description, ignoring case and punctuation.", ConsoleColor.Cyan);
        PrintColoredLine(
            "SEMANTIC: compares the query and description embeddings using cosine similarity.\n" +
            "Embeddings are lists of numbers that represent text; related texts can point in similar directions.",
            ConsoleColor.DarkYellow);
        Console.WriteLine(
            "Higher scores rank first within each method. Cosine similarity ranges from -1 to 1.\n" +
            "Keyword counts and cosine scores use different scales; do not compare their numbers directly.\n" +
            "Neither score is a probability or proof that a project answers the query.");
        Console.WriteLine(
            "Each block compares the same ranking position. The two project IDs may differ.\n" +
            "Compare the project IDs and read the excerpts to judge relevance.");
        PrintHeading("\nWhy use embeddings?");
        Console.WriteLine(
            "People can describe the same idea using different words.\n\n" +
            "Example query:   experience building containerized APIs\n" +
            "Related wording: built web services packaged as Docker images");
        PrintColoredLine("Keyword search counts shared words.", ConsoleColor.Cyan);
        PrintColoredLine("Embedding search can find related meaning despite different wording.", ConsoleColor.DarkYellow);
        Console.WriteLine(
            "\nThis supports RAG (retrieval-augmented generation): finding relevant documents to give an AI\n" +
            "before it answers a question.");
    }

    // Explain rank and excerpt limits once before printing the individual results.
    public static void PrintResultsHeading()
    {
        Console.WriteLine("\nRESULTS: each method starts at rank 1; excerpts show up to 90 characters.");
    }

    // Use the same method colours in every rank so learners can follow each search easily.
    public static void PrintRankComparison(SearchResult semanticSearchResult, SearchResult lexicalSearchResult)
    {
        string semanticLabelString = $"{semanticSearchResult.RankInt}. {semanticSearchResult.DocumentIdString}: {semanticSearchResult.ScoreDouble:F4}";
        PrintColoredLine($"\n── Rank {semanticSearchResult.RankInt} ──────────────────────────", ConsoleColor.White);
        PrintColoredLine(
            $"KEYWORD  | {lexicalSearchResult.RankInt}. {lexicalSearchResult.DocumentIdString} | query words matched: {lexicalSearchResult.ScoreDouble:F0}",
            ConsoleColor.Cyan);
        Console.WriteLine($"  Excerpt: {lexicalSearchResult.SnippetString}");
        PrintColoredLine($"SEMANTIC | {semanticLabelString} (cosine similarity)", ConsoleColor.DarkYellow);
        Console.WriteLine($"  Excerpt: {semanticSearchResult.SnippetString}");
    }

    // Keep storage and usage details together, after the comparison they describe.
    public static void PrintIndexDetails(SearchIndex storedSearchIndex, string indexPathString, UsageDetails? documentUsageDetails,
        UsageDetails? queryUsageDetails)
    {
        PrintHeading("\nIndex and request details");
        PrintColoredLine(
            $"Indexed projects: {storedSearchIndex.IndexedProjects.Count}; numbers per embedding: {storedSearchIndex.DimensionsInt}\n" +
            $"SQLite index: {indexPathString}\n" +
            "Provider input tokens: documents=" +
            $"{documentUsageDetails?.InputTokenCount?.ToString() ?? "unavailable"}, " +
            $"query={queryUsageDetails?.InputTokenCount?.ToString() ?? "unavailable"}",
            ConsoleColor.Gray);
    }

    // Finish with the lesson's limits and a concrete next step for the learner.
    public static void PrintTakeaway()
    {
        PrintHeading("\nWhat to take away");
        PrintColoredLine("Keyword search rewards shared words.", ConsoleColor.Cyan);
        PrintColoredLine("Semantic search can find related wording through embeddings.", ConsoleColor.DarkYellow);
        PrintHeading("\nWhen should you use each method?");
        PrintColoredLine("Keywords:   exact names, product codes, and identifiers.", ConsoleColor.Cyan);
        PrintColoredLine("Embeddings: related meanings and different wording.", ConsoleColor.DarkYellow);
        Console.WriteLine(
            "\nApplications can combine both approaches, called hybrid search.\n" +
            "This experiment lets you inspect each method separately.\n");
        Console.WriteLine(
            "Either method can return misleading matches. A high score alone does not prove relevance.\n" +
            "Read the full descriptions in SampleData/projects.json and decide which projects support the query.\n" +
            "Then change the query and run again: which projects move, and which results are more useful?");
    }
}
