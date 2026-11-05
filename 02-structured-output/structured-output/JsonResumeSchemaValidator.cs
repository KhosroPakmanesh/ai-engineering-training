using System.Text.Json;
using Json.Schema;

public static class JsonResumeSchemaValidator
{
    // Reject repeated field names and values that do not match the required JSON format.
    public static void ValidateSchema(string jsonResumeString)
    {
        using JsonDocument jsonDocument = JsonDocument.Parse(
            jsonResumeString,
            new JsonDocumentOptions
            {
                AllowDuplicateProperties = false
            });

        JsonSchema jsonSchema = JsonSchema.FromText(File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "profile.schema.json")));

        EvaluationResults evaluationResults = jsonSchema.Evaluate(
            jsonDocument.RootElement,
            new EvaluationOptions
            {
                OutputFormat = OutputFormat.List
            });
        if (!evaluationResults.IsValid)
        {
            throw new InvalidDataException(string.Join(
                Environment.NewLine,
                SchemaErrors(evaluationResults)));
        }
    }

    // Include each field's path so the learner can find the invalid value.
    private static IEnumerable<string> SchemaErrors(EvaluationResults resultEvaluationResults)
    {
        if (resultEvaluationResults.Errors is not null)
        {
            foreach (var errorKeyValuePair in resultEvaluationResults.Errors)
            {
                yield return $"{(resultEvaluationResults.InstanceLocation.ToString() == "" ? "$" : resultEvaluationResults.InstanceLocation)}: {errorKeyValuePair.Value}";
            }
        }

        if (resultEvaluationResults.Details is not null)
        {
            foreach (EvaluationResults detailEvaluationResults in resultEvaluationResults.Details)
            {
                foreach (string errorString in SchemaErrors(detailEvaluationResults))
                {
                    yield return errorString;
                }
            }
        }
    }
}
