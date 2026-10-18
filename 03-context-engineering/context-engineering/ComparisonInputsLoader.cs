
using System.Text.Json;

public static class ComparisonInputsLoader
{
    // Use the same project text and expected project IDs in console runs and tests.
    public static async Task<ComparisonInputs> LoadComparisonInputsAsync(
        string question, string sourceFile, CancellationToken cancellationToken = default)
    {
        string folder = AppContext.BaseDirectory;

        string markdown = await File.ReadAllTextAsync(Path.Combine(folder, "SampleData", sourceFile), cancellationToken);
        List<ProjectSection> sections = [.. ProjectSectionParser.ParseProjectSection(markdown)];

        if (sections.Count < 6) throw new InvalidDataException("The learning example needs at least six project sections.");

        JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
        List<QuestionFixture> fixtures = JsonSerializer.Deserialize<List<QuestionFixture>>(
            await File.ReadAllTextAsync(Path.Combine(folder, "SampleData", "questions.json"), cancellationToken), jsonOptions)
            ?? throw new InvalidDataException("The question fixtures are missing.");

        List<string>? expectedIds = fixtures.FirstOrDefault(fixture =>
            fixture.Question.Equals(question.Trim(), StringComparison.OrdinalIgnoreCase))?.ExpectedSectionIds;

        return new(question, sections, expectedIds);
    }
}
