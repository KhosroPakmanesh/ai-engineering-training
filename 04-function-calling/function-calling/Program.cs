using System.Text.Json;

const string question = "How many full calendar months of backend experience do these fictional jobs cover?";
const string evaluationDate = "2023-04-01";

try
{
    // Find unusable job dates before sending the question and source data to the model.
    ExperienceInputs inputs = await ExperienceInputsLoader.LoadAsync(question, evaluationDate);

    // Validate the inputs before sending them to the model; the model does not validate them.
    ExperienceInputsValidator.ValidateInputs(inputs);

    // The model chooses the tool; its local function performs the arithmetic.
    AiResponse response = await ExperienceAssistant.AskAsync(inputs);

    // Accept a result only after it was confirmed that the function really ran.
    ToolExecutionValidator.ValidateToolExecution(response);

    ExperienceResult result = response.ToolExecutions[0].Result.Deserialize<ExperienceResult>(
        new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ?? throw new InvalidDataException("The calculation returned no structured result.");

        // Check the model's final number and citation against the actual tool result.
    ExperienceResultValidator.ValidateAnswer(response.Text, result);

    Console.WriteLine($"Input intervals: {result.InputIntervalCount}; merged intervals: {result.MergedIntervals.Count}");
    Console.WriteLine($"Model calls: {response.ModelCalls}; tool executions: {response.ToolExecutions.Count}");
    Console.WriteLine($"Request time: {response.LatencyMilliseconds} ms");
    Console.WriteLine(response.Text);

    return 0;
}
catch (AiRequestException error)
{
    AiProvider.WriteError(error);
    return 1;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("The request was canceled or exceeded the 120-second time limit.");
    return 1;
}
catch (Exception error)
{
    Console.Error.WriteLine(error.Message);
    return 1;
}
