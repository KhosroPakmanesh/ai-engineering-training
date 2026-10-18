using System.Text.Json;

internal static class ContextTestData
{
    // Each test gets its own sections while preserving the application's copied source text.
    public static List<ProjectSection> ReadSections() => ProjectSectionParser.ParseProjectSection(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "SampleData", "projects.md")));

    // Fixtures identify expected evidence without prescribing a model's wording.
    public static List<QuestionFixture> ReadFixtures() => JsonSerializer.Deserialize<List<QuestionFixture>>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "SampleData", "questions.json")),
        new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
}
