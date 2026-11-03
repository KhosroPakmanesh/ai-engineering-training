public static class SearchInputsValidator
{
    public const int MaximumQueryCharactersInt = 500;
    public const int MaximumProjectCharactersInt = 4_000;
    public const int MaximumProjectsInt = 100;

    // Check every local input before the first paid request, including projects near the end of the file.
    public static void Validate(List<ProjectDescription> projectDescriptions, string queryString, int topKInt)
    {
        ValidateQuery(queryString);
        ValidateTopK(topKInt);
        ValidateProjects(projectDescriptions);
    }

    // A usable question is required before preparing either text batch.
    private static void ValidateQuery(string queryString)
    {
        if (string.IsNullOrWhiteSpace(queryString) || queryString.Length > MaximumQueryCharactersInt)
        {
            throw new ArgumentException($"The query must contain between 1 and {MaximumQueryCharactersInt} characters.",
                nameof(queryString));
        }
    }

    // Top-k describes how many results each ranking can return.
    private static void ValidateTopK(int topKInt)
    {
        if (topKInt <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(topKInt), "Top-k must be positive.");
        }
    }

    // Report independent project errors together after checking the collection size.
    private static void ValidateProjects(List<ProjectDescription> projectDescriptions)
    {
        if (projectDescriptions.Count is 0 or > MaximumProjectsInt)
        {
            throw new InvalidDataException($"Supply between 1 and {MaximumProjectsInt} fictional projects.");
        }

        var errorStrings = new List<string>();
        var idStringsHashSet = new HashSet<string>(StringComparer.Ordinal);
        for (int indexInt = 0; indexInt < projectDescriptions.Count; indexInt++)
        {
            ProjectDescription? projectDescription = projectDescriptions[indexInt];
            string pathString = $"projects[{indexInt}]";

            if (projectDescription is null)
            {
                errorStrings.Add($"{pathString}: a project is required.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(projectDescription.IdString) || !idStringsHashSet.Add(projectDescription.IdString))
            {
                errorStrings.Add($"{pathString}.id: use a nonempty, unique document ID.");
            }

            if (string.IsNullOrWhiteSpace(projectDescription.TextString) || projectDescription.TextString.Length > MaximumProjectCharactersInt)
            {
                errorStrings.Add($"{pathString}.text: supply between 1 and {MaximumProjectCharactersInt} characters.");
            }

            if (projectDescription.SkillStrings is null || projectDescription.SkillStrings.Count == 0 || projectDescription.SkillStrings.Any(string.IsNullOrWhiteSpace))
            {
                errorStrings.Add($"{pathString}.skills: supply nonempty demonstrated skill names.");
            }
        }

        if (errorStrings.Count > 0)
        {
            throw new InvalidDataException(string.Join(Environment.NewLine, errorStrings));
        }
    }
}
