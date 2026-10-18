using System.Text.Json;

internal static class ContextTestData
{
    // Give each test its own project sections, using the same source file as the application.
    public static List<ProjectSection> ReadSections() => ProjectSectionParser.ParseProjectSection(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "SampleData", "projects.md")));

    // The example data lists expected project IDs without requiring exact answer wording.
    public static List<QuestionFixture> ReadFixtures() => JsonSerializer.Deserialize<List<QuestionFixture>>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "SampleData", "questions.json")),
        new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
}
