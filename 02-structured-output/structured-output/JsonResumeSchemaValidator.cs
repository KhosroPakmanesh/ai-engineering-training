using Json.Schema;
using System.Text.Json;

public static class JsonResumeSchemaValidator
{
    // Reject repeated field names and values that do not match the required JSON format.
    public static void ValidateSchema(string jsonResume)
    {
        using JsonDocument document = JsonDocument.Parse(jsonResume, new JsonDocumentOptions { AllowDuplicateProperties = false });

        JsonSchema schema = JsonSchema.FromText(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "profile.schema.json")));

        EvaluationResults evaluation = schema.Evaluate(document.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });
        if (!evaluation.IsValid)
            throw new InvalidDataException(string.Join(Environment.NewLine, SchemaErrors(evaluation)));
    }
    // Include each field's path so the learner can find the invalid value.
    private static IEnumerable<string> SchemaErrors(EvaluationResults result)
    {
        if (result.Errors is not null)
            foreach (var error in result.Errors)
                yield return $"{(result.InstanceLocation.ToString() == "" ? "$" : result.InstanceLocation)}: {error.Value}";
        if (result.Details is not null)
            foreach (EvaluationResults detail in result.Details)
                foreach (string error in SchemaErrors(detail)) yield return error;
    }
}
