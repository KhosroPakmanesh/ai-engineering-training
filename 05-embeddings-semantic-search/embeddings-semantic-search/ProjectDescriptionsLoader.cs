using System.Text.Json;

public static class ProjectDescriptionsLoader
{
    // Read copied inputs so Visual Studio, dotnet run, and tests find the same fictional projects.
    public static async Task<List<ProjectDescription>> LoadAsync(string pathString, CancellationToken cancellationToken = default)
    {
        string jsonProjectsString = await File.ReadAllTextAsync(pathString, cancellationToken);
        return JsonSerializer.Deserialize<List<ProjectDescription>>(jsonProjectsString, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidDataException("The projects file must contain a JSON list.");
    }
}
