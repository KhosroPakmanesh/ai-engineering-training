public static class SearchInputsValidator
{
    public const int MaximumQueryCharacters = 500;
    public const int MaximumProjectCharacters = 4_000;
    public const int MaximumProjects = 100;

    // Check every local input before the first paid request, including projects near the end of the file.
    public static void Validate(List<ProjectDescription> projects, string query, int topK)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length > MaximumQueryCharacters)
            throw new ArgumentException($"The query must contain between 1 and {MaximumQueryCharacters} characters.", nameof(query));

        if (topK <= 0)
            throw new ArgumentOutOfRangeException(nameof(topK), "Top-k must be positive.");

        if (projects.Count is 0 or > MaximumProjects)
            throw new InvalidDataException($"Supply between 1 and {MaximumProjects} fictional projects.");

        var errors = new List<string>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < projects.Count; index++)
        {
            ProjectDescription? project = projects[index];
            string path = $"projects[{index}]";

            if (project is null)
            {
                errors.Add($"{path}: a project is required.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(project.Id) || !ids.Add(project.Id))
                errors.Add($"{path}.id: use a nonempty, unique document ID.");

            if (string.IsNullOrWhiteSpace(project.Text) || project.Text.Length > MaximumProjectCharacters)
                errors.Add($"{path}.text: supply between 1 and {MaximumProjectCharacters} characters.");

            if (project.Skills is null || project.Skills.Count == 0 || project.Skills.Any(string.IsNullOrWhiteSpace))
                errors.Add($"{path}.skills: supply nonempty demonstrated skill names.");
        }

        if (errors.Count > 0) throw new InvalidDataException(string.Join(Environment.NewLine, errors));
    }
}
