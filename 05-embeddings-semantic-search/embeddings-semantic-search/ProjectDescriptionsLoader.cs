using System.Text.Json;

public static class ProjectDescriptionsLoader
{
    // Read copied inputs so Visual Studio, dotnet run, and tests find the same fictional projects.
    public static async Task<List<ProjectDescription>> LoadAsync
        (string path, CancellationToken cancellationToken = default)
    {
        string jsonProjects = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonSerializer.Deserialize<List<ProjectDescription>>
            (jsonProjects, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidDataException("The projects file must contain a JSON list.");
    }
}
