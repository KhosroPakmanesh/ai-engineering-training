using System.Text.Json;
using Json.Schema;

// Coordinate JSON shape checks, source-data checks, and skill normalization.
public static class ProfileValidator
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    // Keep the conversion from untrusted JSON to an accepted profile visible as three stages.
    public static CandidateProfile ParseAndValidate(string json, string resume, string sourceFile)
    {
        CheckSchema(json);

        CandidateProfile profile = JsonSerializer.Deserialize<CandidateProfile>(json, JsonOptions)!;
        new ProfileChecks(resume, sourceFile).Validate(profile);

        return NormalizeSkills(profile);
    }

    // Shape comes first: malformed JSON, duplicate keys, missing fields, and wrong types fail here.
    private static void CheckSchema(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions { AllowDuplicateProperties = false });
        JsonSchema schema = JsonSchema.FromText(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "profile.schema.json")));
        EvaluationResults evaluation = schema.Evaluate(document.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });
        if (!evaluation.IsValid)
            throw new InvalidDataException(string.Join(Environment.NewLine, SchemaErrors(evaluation)));
    }

    // Schema failures retain their JSON paths so a learner can locate the offending field.
    private static IEnumerable<string> SchemaErrors(EvaluationResults result)
    {
        if (result.Errors is not null)
            foreach (var error in result.Errors)
                yield return $"{(result.InstanceLocation.ToString() == "" ? "$" : result.InstanceLocation)}: {error.Value}";
        if (result.Details is not null)
            foreach (EvaluationResults detail in result.Details)
                foreach (string error in SchemaErrors(detail)) yield return error;
    }

    // Keep the first skill's wording and evidence when names differ only by case or outer spaces.
    private static CandidateProfile NormalizeSkills(CandidateProfile profile) => profile with
    {
        Skills = profile.Skills.Select(skill => skill with { Name = skill.Name.Trim() })
            .DistinctBy(skill => skill.Name, StringComparer.OrdinalIgnoreCase).ToArray()
    };
}
