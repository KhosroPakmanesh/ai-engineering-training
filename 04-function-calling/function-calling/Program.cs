using System.Diagnostics;
using Microsoft.Agents.AI;

const string questionString = "How many full calendar months of backend experience do these fictional jobs cover?";
const string evaluationDateString = "2023-04-01";

try
{
    //------------------------------------------------------------------------//
    //                              PREPARATION                               //
    //------------------------------------------------------------------------//

    // 1. Load and validate the source inputs before making a model request.
    ExperienceInputs experienceInputs = await ExperienceInputsLoader.LoadAsync(
        questionString,
        evaluationDateString);
    ExperienceInputsValidator.ValidateInputs(experienceInputs);

    // 2. Explain the two-turn conversation.
    OutputPrinter.PrintIntroduction(experienceInputs);

    //------------------------------------------------------------------------//
    //                           TOOL CONVERSATION                            //
    //------------------------------------------------------------------------//

    // 1. The model selects the tool, and the framework executes the C# function.
    var timerStopwatch = Stopwatch.StartNew();
    AgentResponse agentResponse = await ExperienceAssistant.AskAsync(experienceInputs);
    timerStopwatch.Stop();

    //------------------------------------------------------------------------//
    //                           ACCEPTANCE CHECKS                            //
    //------------------------------------------------------------------------//

    // 1. Confirm that the calculator really executed successfully.
    ToolExecutionValidator.ValidateToolExecution(agentResponse);

    // 2. Read the final answer, typed calculation result, and call counts.
    ParsedExperienceResponse parsedExperienceResponse = ExperienceResponseParser.Parse(agentResponse);
    ExperienceResult experienceResult = parsedExperienceResponse.ExperienceResult;

    // 3. Check the final answer's total and citation against the calculation.
    ExperienceResultValidator.ValidateAnswer(
        parsedExperienceResponse.AnswerString,
        experienceResult);

    //------------------------------------------------------------------------//
    //                          RESULTS AND TAKEAWAY                          //
    //------------------------------------------------------------------------//

    // 1. Show the calculation counts, request measurements, and accepted answer.
    OutputPrinter.PrintResults(
        parsedExperienceResponse,
        timerStopwatch.ElapsedMilliseconds);
    OutputPrinter.PrintTakeaway();

    return 0;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("The request was canceled or exceeded the 120-second time limit.");
    return 1;
}
catch (Exception errorException)
{
    Console.Error.WriteLine(errorException.Message);
    return 1;
}
