using ResumeRag.Tests.TestData;
using Xunit;

namespace ResumeRag.Tests;

public class MarkdownChunkerTests
{
    // IDs and headings identify the same source across repeat ingestions, including oversized paragraphs.
    [Fact]
    public void Chunking_preserves_source_bounds_overlap_and_repeatable_ids()
    {
        string paragraphString = new('x', 180);
        List<SourceDocument> sourceDocuments = [new("project-a.md", $"# Project\n\n## Delivery\n\n{paragraphString}\n\n## Storage\n\nPostgreSQL work.")];
        List<DocumentChunk> documentChunks = MarkdownChunker.Chunk(sourceDocuments, 100, 20);
        Assert.Equal(documentChunks, MarkdownChunker.Chunk(sourceDocuments, 100, 20));
        Assert.Equal(3, documentChunks.Count);
        Assert.All(documentChunks, documentChunk => Assert.InRange(documentChunk.TextString.Length, 1, 100));
        Assert.Equal("project-a.md#chunk-01", documentChunks[0].ChunkIdString);
        Assert.Equal("Delivery", documentChunks[0].HeadingString);
        Assert.Equal(documentChunks[0].TextString[^20..], documentChunks[1].TextString[..20]);
        Assert.Equal("Storage", documentChunks[2].HeadingString);
        Assert.All(documentChunks, documentChunk => Assert.Equal("project-a.md", documentChunk.FilenameString));
    }

    // The absent skill is a deliberate control in the real fixtures, not a special-case answer in the application.
    [Fact]
    public async Task Sample_documents_include_known_skills_and_exclude_the_negative_control()
    {
        List<DocumentChunk> documentChunks = await RagFixtures.LoadChunksAsync();
        Assert.Equal(6, documentChunks.Select(documentChunk => documentChunk.FilenameString).Distinct().Count());
        Assert.Contains(documentChunks, documentChunk => documentChunk.FilenameString == "project-a.md" && documentChunk.TextString.Contains("Docker"));
        Assert.Contains(documentChunks, documentChunk => documentChunk.FilenameString == "project-b.md" && documentChunk.TextString.Contains("PostgreSQL"));
        Assert.DoesNotContain(documentChunks, documentChunk => documentChunk.TextString.Contains("Kubernetes", StringComparison.OrdinalIgnoreCase));
    }
}
