using System.Diagnostics;
using Microsoft.Agents.AI;

const string question = "How many full calendar months of backend experience do these fictional jobs cover?";
const string evaluationDate = "2023-04-01";

try
{
    // Load and validate the source inputs before making a model request.
    ExperienceInputs inputs = await ExperienceInputsLoader.LoadAsync(question, evaluationDate);
    ExperienceInputsValidator.ValidateInputs(inputs);

    // The model selects the tool, and the framework executes the C# function.
    var timer = Stopwatch.StartNew();
    AgentResponse response = await ExperienceAssistant.AskAsync(inputs);
    timer.Stop();

    // Confirm that the calculator really executed successfully.
    ToolExecutionValidator.ValidateToolExecution(response);

    // Read the final answer, typed calculation result, and call counts.
    ParsedExperienceResponse parsedResponse = ExperienceResponseParser.Parse(response);
    ExperienceResult result = parsedResponse.Result;

    // Check the final answer's total and citation against the calculation.
    ExperienceResultValidator.ValidateAnswer(parsedResponse.Answer, result);

    // Show the calculation counts, request measurements, and accepted answer.
    Console.WriteLine($"Input intervals: {result.InputIntervalCount}; merged intervals: {result.MergedIntervals.Count}");
    Console.WriteLine($"Model calls: {parsedResponse.ModelCallCount}; tool executions: {parsedResponse.ToolExecutionCount}");
    Console.WriteLine($"Request time: {timer.ElapsedMilliseconds} ms");
    Console.WriteLine(parsedResponse.Answer);

    return 0;
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
