using System.Text.Json;

public static class ComparisonInputsLoader
{
    // Use the same project text and expected project IDs in console runs and tests.
    public static async Task<ComparisonInputs> LoadComparisonInputsAsync(
        string questionString,
        string sourceFileString,
        CancellationToken cancellationToken = default)
    {
        string folderString = AppContext.BaseDirectory;

        string markdownString = await File.ReadAllTextAsync(
            Path.Combine(
                folderString,
                "SampleData",
                sourceFileString),
            cancellationToken);
        List<ProjectSection> projectSections = ProjectSectionParser.ParseProjectSection(markdownString);

        if (projectSections.Count < 6)
        {
            throw new InvalidDataException("The learning example needs at least six project sections.");
        }

        JsonSerializerOptions jsonSerializerOptions = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };
        List<QuestionFixture> questionFixtures = JsonSerializer.Deserialize<List<QuestionFixture>>(
            await File.ReadAllTextAsync(
                Path.Combine(
                    folderString,
                    "SampleData",
                    "questions.json"),
                cancellationToken),
            jsonSerializerOptions)
            ?? throw new InvalidDataException("The question fixtures are missing.");

        List<string>? expectedIdStrings = questionFixtures.FirstOrDefault(questionFixture =>
            questionFixture.QuestionString.Equals(
                questionString.Trim(),
                StringComparison.OrdinalIgnoreCase))?.ExpectedSectionIdStrings;

        return new(
            questionString,
            projectSections,
            expectedIdStrings);
    }
}
