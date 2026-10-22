using Microsoft.Extensions.AI;
using static ConsoleOutput;

public static class OutputPrinter
{
    // Keep the setup explanation separate so the embedding request stages stay easy to follow.
    public static void PrintIntroduction(string query, int projectCount, int topK, ProviderSettings settings)
    {
        PrintHeading("Keyword vs. Semantic Search");
        Console.WriteLine(
            $"Query: \"{query}\"\n" +
            $"Search the same {projectCount} project descriptions; show the top {topK} from each method.");
        PrintColoredLine(
            $"Provider: {settings.Name}\n" +
            $"Embedding model: {settings.Model}",
            ConsoleColor.Gray);
        PrintColoredLine(
            "\nPreparing embeddings: two remote calls, one for all descriptions and one for the query.\n" +
            $"Up to {AiClientFactory.MaxRetries} HTTP retries per call. These calls send the fictional text and query to the provider.",
            ConsoleColor.Gray);
    }

    // Explain the two score scales before the learner compares the ranked projects.
    public static void PrintComparisonGuide(string query)
    {
        PrintHeading("\nHow to read the comparison");
        Console.WriteLine($"Both searches use the query: \"{query}\"");
        PrintColoredLine(
            "KEYWORD: counts distinct query words found in the description, ignoring case and punctuation.",
            ConsoleColor.Cyan);
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
        PrintColoredLine(
            "Embedding search can find related meaning despite different wording.",
            ConsoleColor.DarkYellow);
        Console.WriteLine(
            "\nThis supports RAG (retrieval-augmented generation): finding relevant documents to give an AI\n" +
            "before it answers a question.");
    }

    // Explain rank and excerpt limits once before printing the individual results.
    public static void PrintResultsHeading() =>
        Console.WriteLine("\nRESULTS: each method starts at rank 1; excerpts show up to 90 characters.");

    // Use the same method colours in every rank so learners can follow each search easily.
    public static void PrintRankComparison(SearchResult semantic, SearchResult lexical)
    {
        string semanticLabel = $"{semantic.Rank}. {semantic.DocumentId}: {semantic.Score:F4}";
        PrintColoredLine(
            $"\n── Rank {semantic.Rank} ──────────────────────────",
            ConsoleColor.White);
        PrintColoredLine(
            $"KEYWORD  | {lexical.Rank}. {lexical.DocumentId} | query words matched: {lexical.Score:F0}",
            ConsoleColor.Cyan);
        Console.WriteLine($"  Excerpt: {lexical.Snippet}");
        PrintColoredLine($"SEMANTIC | {semanticLabel} (cosine similarity)", ConsoleColor.DarkYellow);
        Console.WriteLine($"  Excerpt: {semantic.Snippet}");
    }

    // Keep storage and usage details together, after the comparison they describe.
    public static void PrintIndexDetails(
        SearchIndex storedIndex, string indexPath, UsageDetails? documentUsage, UsageDetails? queryUsage)
    {
        PrintHeading("\nIndex and request details");
        PrintColoredLine(
            $"Indexed projects: {storedIndex.Projects.Count}; numbers per embedding: {storedIndex.Dimensions}\n" +
            $"SQLite index: {indexPath}\n" +
            "Provider input tokens: documents=" +
            $"{documentUsage?.InputTokenCount.ToString() ?? "unavailable"}, " +
            $"query={queryUsage?.InputTokenCount.ToString() ?? "unavailable"}",
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
