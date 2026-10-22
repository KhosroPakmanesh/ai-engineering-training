# Function Calling: one small experiment

Function calling lets an AI ask your application to run a C# method and then use its result. Here, the model answers a question about three fictional backend jobs by selecting `CalculateExperience`. C# merges overlapping work dates and counts full calendar months. **Program.cs** shows the main stages. **ExperienceInputsLoader.cs** reads the jobs, **ExperienceInputsValidator.cs** checks the question and dates, **ExperienceAssistant.cs** declares the instruction, tool, and request settings, and **ExperienceCalculator.cs** performs the arithmetic. **ToolExecutionValidator.cs** checks that the function really ran, **ExperienceResponseParser.cs** reads the answer, calculation result, and call counts, and **ExperienceResultValidator.cs** checks the final total and citation. **OutputPrinter.cs** explains the tool sequence, prints the redacted execution log, and presents the accepted answer and measurements.

| Path | Concept |
| --- | --- |
| Direct invocation | C# validates known arguments with `ExperienceInputsValidator`, then calls `ExperienceCalculator.CalculateExperience`. Deterministic tests exercise this path without an API. |
| Agent-mediated invocation | The model requests the registered function. Agent Framework converts its arguments, executes C#, sends the structured result back, and obtains the final answer. |

Both paths use the same calculator and month-counting rule. The instruction tells the model to copy all supplied intervals and the explicit evaluation date, call the function exactly once, and cite `CalculateExperience.totalMonths`. The direct calculation is not supplied as a suggested answer. Change the dates to explore the arithmetic; keep them fixed when comparing the invocation paths. This lesson counts all supplied jobs rather than selecting roles from a resume.

## Execution flow

Follow the arrows from top to bottom. Program.cs loads the source inputs and explicitly calls ExperienceInputsValidator.cs before making a paid request. Tool arguments are checked against those inputs because a model can return different dates even when its JSON looks valid.

```mermaid
flowchart TD
    A["Program.cs + ExperienceInputsLoader.cs<br/>Read fictional jobs, question, and evaluation date"] --> B{"ExperienceInputsValidator.cs<br/>Are the question and dates usable?"}
    B -->|Yes| O["OutputPrinter.cs<br/>Introduce the question, dates, and tool sequence"]
    O --> C["ExperienceAssistant.cs<br/>Create AIFunction and ChatClientAgent with ChatOptions"]
    C --> D["ExperienceAssistant.cs<br/>Call agent.RunAsync; first model call with the available tool"]
    D --> E{"Did the model select CalculateExperience?"}
    E -->|Yes| F{"ExperienceAssistant.cs<br/>Do arguments match the supplied jobs and date?"}
    F -->|Yes| G["ExperienceCalculator.cs<br/>Sort, merge intervals, and count full months"]
    G --> H["FunctionInvokingChatClient<br/>Record FunctionResultContent; send the second model call"]
    H --> R{"ExperienceAssistant.cs<br/>Is the final answer present and complete?"}
    R -->|Yes| I{"ToolExecutionValidator.cs<br/>Did CalculateExperience execute exactly once?"}
    R -->|No| X
    I -->|Yes| P["ExperienceResponseParser.cs<br/>Read the final answer, typed result, and call counts"]
    P --> J{"ExperienceResultValidator.cs<br/>Does the answer report and cite the returned total?"}
    J -->|Yes| K["OutputPrinter.cs<br/>Print counts, timing, accepted answer, and takeaway"]
    K --> Y["Program.cs<br/>Exit 0"]
    B -->|No| X["Show the error and exit 1"]
    E -->|No| X
    F -->|No| X
    I -->|No| X
    J -->|No| X
    D -->|API or tool error| X
    H -->|API or tool error| X
```

Loading, validation, requests, and calculation are separate stages. ExperienceInputsLoader.cs reads the file and constructs the input records. ExperienceInputsValidator.cs rejects empty questions, missing jobs, invalid dates, unresolved current roles, and end dates before start dates. Callers must complete this validation before calling ExperienceAssistant.AskAsync or calculating directly. Those methods assume their source inputs have passed the checks. Tests follow the same order as Program.cs.

Dates must be complete ISO dates such as `2024-02-01`. Each interval includes its start and excludes its end: February 1 to March 1 covers all of February. A month counts only when its entire calendar month is covered. January 15 to March 15 therefore counts February only. February 1 to February 29 excludes February's final day and counts zero months, even in a leap year. Equal start and end dates cover no time.

The calculator sorts intervals and merges overlaps, duplicates, and touching intervals before counting. Jobs covering January 1-15 and January 15-February 1 together cover one full January. A null `endDate` means a current role and requires the evaluation date declared in Program.cs. The program never uses today's date or invents missing date parts. The evaluation date replaces only missing end dates; completed jobs retain their stated end dates.

ExperienceAssistant.cs binds an ordinary C# callback to this run's original inputs. It rejects changed, omitted, added, or reordered intervals and a changed evaluation date. ExperienceAssistant.cs uses `AIFunctionFactory.Create` to generate the argument schema from that callback's parameter types. Agent Framework performs the actual invocation loop. Tool choice is automatic, so a model that answers without a tool is detected rather than treated as success. The callback measures its local work and calls OutputPrinter.cs from `finally` to retain the redacted log on both success and failure. The printer formats supplied values; it does not execute or validate the tool.

ExperienceAssistant.cs uses the framework's FunctionInvokingChatClient for one tool round trip: a model call to select the function, followed by a model call to explain its result. It handles argument conversion, calls the registered function, and sends the result back to the model. Function errors stop before a follow-up model request. The instruction requests one calculation, and ToolExecutionValidator checks that exactly one successful result was returned; this check runs after the conversation. [AiClientFactory.cs](../../shared/shared/AiClientFactory.cs) retains the original SDK assistant tool message so opaque provider fields, including Gemini thought signatures, survive the next turn.

After RunAsync, ExperienceAssistant.cs checks the native finish reason and rejects empty final assistant text. Program.cs then calls ToolExecutionValidator.cs to check execution, ExperienceResponseParser.cs to read the final answer, typed calculation result, and call counts, and ExperienceResultValidator.cs to check the answer. The parser returns a `ParsedExperienceResponse` containing an `ExperienceResult` and excludes earlier assistant preambles from the answer. Its deliberately small answer contract is `<integer> full calendar months [CalculateExperience.totalMonths].` The citation is literal text. Checking the complete sentence prevents additional unsupported totals from being accepted. Local check failures do not trigger repair requests.

## Run

Install the .NET 10 SDK. Use the repository's single settings file, `shared/shared/.env`, created from [shared/shared/.env.example](../../shared/shared/.env.example). Select OpenRouter or Gemini and fill in that provider's key and model using the [shared setup instructions](../../shared/shared/README.md#run). Rebuild after editing settings. Git ignores the credentials file.

From the repository root, these commands work in Windows PowerShell and macOS/Linux shells:

```sh
dotnet restore AIEngineeringTraining.slnx
dotnet build AIEngineeringTraining.slnx
cd 04-function-calling/function-calling
dotnet run
```

You can also select `function-calling` as the startup project in Visual Studio. The project reference carries shared settings into build output, and the project copies `SampleData/jobs.json` there. Inputs are read from `AppContext.BaseDirectory`, so the launch folder does not change which copies are used. Edit the original files and rebuild.

Running the application sends the question and fictional work intervals to the selected remote service: OpenRouter and its upstream provider, or Google's Gemini API through its OpenAI-compatible endpoint. It may charge your account. Restore, build, and deterministic tests do not make model requests. The application runs without command-line arguments; use the calculator tests for local arithmetic checks.

A successful run makes **one agent run containing exactly two model calls and one local tool execution**. This normally sends two HTTP requests and permits at most **six HTTP attempts** with the shared two-retry policy. Early failures can stop after zero, one, or two model calls. The shared **120-second deadline covers the entire conversation**, including both turns, local execution, retries, and waits. ExperienceAssistant.cs declares temperature 0.1 and a 2,048-token output limit for each turn. Model calls, local executions, and HTTP retries are different counts.

Choose a Chat Completions model that supports function tools and the declared generation settings. OpenRouter requests require parameter support; Gemini receives no OpenRouter routing option. The shared library keeps the existing pinned versions: Microsoft.Agents.AI 1.25.0, Microsoft.Extensions.AI.OpenAI 10.11.0, OpenAI 2.14.0, and System.ClientModel 1.16.0.

A missing or invalid reference date fails before an API request. Zero tool calls, unsupported tools, or a changed total or citation fail local checks. The entry point prints the native SDK error message, including the provider details supplied by the SDK. For 401/403, check the selected key and permissions; for 402, check credits; for 404, check model availability; for 429 or 5xx, check quota or availability. A 400 can indicate unsupported settings or tools. The provider factory preserves the configured model and provider and uses the shared retries.

## Understand the results

The fictional jobs cover January 2020-July 2021, January 2021-January 2022, and April 2022 through the explicit April 2023 evaluation date. Summing jobs separately gives 42 months. The first two overlap for six months, so their merged coverage is 24 months. The current role contributes 12 more, giving **36 full calendar months** across two merged intervals.

OutputPrinter.cs introduces the two model turns before the request. Green headings separate the stages, and provider details and the redacted tool log appear in grey. The shared ConsoleOutput.cs helper restores the terminal's original colour. Redirected output stays plain text. After the accepted answer, the takeaway explains full-month counting and asks you to predict the effect of changing one source date.

A successful run includes:

```text
tool_name=CalculateExperience inputs=[redacted] duration_ms=15 succeeded=True
Input intervals: 3; merged intervals: 2
Model calls: 2; tool executions: 1
Request time: 2400 ms
36 full calendar months [CalculateExperience.totalMonths].
```

Durations vary. The trace records the function name, redacted inputs, duration, and success status. It does not print original work dates or exception contents. The native AgentResponse contains FunctionCallContent and FunctionResultContent messages. The successful result carries `totalMonths`, `inputIntervalCount`, and `mergedIntervals`. Program.cs prints the accepted answer and measurements; it does not save an output file. Integration test output includes the fictional structured result for inspection.

Request time includes settings and agent setup, both model turns, local execution, and retry waits. ExperienceResponseParser.cs counts the returned assistant messages; with these Chat Completions clients, each message represents one completed model call. Tool duration measures the local source checks and calculation. A message such as `HTTP 503. Retry 1/2 in 2.3 seconds.` means the shared policy is resending that HTTP request; it does not mean another calculation ran. Native UsageDetails retains provider token counts when reported; missing counts remain unknown.

The checks establish that the printed number matches an executed calculation over the supplied dates. You still decide whether those intervals represent the experience you intended to measure. The program does not extract dates from prose, infer job relevance, or establish that an employment claim is true.

## Learn by changing the dates

1. Read [jobs.json](SampleData/jobs.json). Calculate each job separately, then predict the overlap-adjusted total. Run and compare your prediction with the merged count and accepted answer.
2. Change one start date to the middle of a month and rebuild. Predict which whole calendar months remain covered after merging.
3. Change `evaluationDate` in Program.cs. The current role should use that exact date. Try an invalid date and observe that the program stops before a model request.
4. Read the direct calculator tests, then follow the same function through ExperienceAssistant.cs and its native framework loop. Each successful run makes another two model calls, with at most six HTTP attempts.

## Files to read

```text
Program.cs                                      Question, evaluation date, and explicit acceptance flow
OutputPrinter.cs                                Tool explanation, redacted log, accepted results, and takeaway
ExperienceInputsLoader.cs                       Copied input loading and input records
ExperienceInputsValidator.cs                    Question, date, reference-date, and chronology checks
ExperienceCalculator.cs                         Sorting, overlap merging, and calendar-month counting
ExperienceAssistant.cs                          Prompt, tool callback, source checks, settings, and native agent call
ToolExecutionValidator.cs                       Exactly one real CalculateExperience execution
ExperienceResponseParser.cs                    Final assistant answer, typed calculation result, and call counts
ExperienceResultValidator.cs                    Exact total and citation checks
Types/ExperienceInputs.cs                       Original intervals and question inputs
Types/ExperienceResult.cs                       Merged intervals and structured calculation result
Types/ParsedExperienceResponse.cs               Parsed answer, calculation, and display counts
SampleData/jobs.json                            Three fictional backend work intervals
function-calling.csproj                         .NET 10 application, copied inputs, and shared reference
../function-calling.tests/                      Counterpart tests and fresh test fixtures
../../shared/shared/SettingsReader.cs           Shared settings loading
../../shared/shared/AiClientFactory.cs           Provider connections, routing, and tool metadata
../../shared/shared/ConsoleOutput.cs             Shared headings and terminal colour handling
../../shared/shared/Providers/AiRetryPolicy.cs  Internal bounded HTTP retry policy
../../shared/shared/.env.example                Shared credentials template
```

Start with Program.cs and follow its separate loading and validation calls. Read the calculator for the local rule, then ExperienceAssistant.cs for how the model receives that function. Follow the execution check, ExperienceResponseParser.cs conversion, and answer check in order. Read the native loop settings in ExperienceAssistant.cs and AiClientFactory.cs for provider compatibility. Counterpart tests cover each stage's behavior and the complete request flow.

## Tests

The project contains **37 cases: 36 deterministic cases and one live integration case**. Tests use Microsoft.NET.Test.Sdk 18.11.0, xunit 2.9.3, and xunit.runner.visualstudio 4.0.1.

| Test file | Checks |
| --- | --- |
| [ExperienceInputsLoaderTests.cs](../function-calling.tests/ExperienceInputsLoaderTests.cs) | Copied jobs retain the selected question, evaluation date, and original work dates. |
| [ExperienceInputsValidatorTests.cs](../function-calling.tests/ExperienceInputsValidatorTests.cs) | Empty questions, missing jobs, invalid or partial dates, unresolved current roles, and reversed chronology. |
| [ExperienceCalculatorTests.cs](../function-calling.tests/ExperienceCalculatorTests.cs) | Validated separate and overlapping jobs, current roles, leap dates, partial months, duplicates, touching intervals, and unchanged inputs. |
| [ExperienceAssistantTests.cs](../function-calling.tests/ExperienceAssistantTests.cs) | Real native requests, the framework iteration limit, source preservation, empty or truncated final answers, zero tool calls, validation preventing requests, and one complete live acceptance flow. |
| [ToolExecutionValidatorTests.cs](../function-calling.tests/ToolExecutionValidatorTests.cs) | Missing, wrong, failed, and repeated native function results are rejected. |
| [ExperienceResponseParserTests.cs](../function-calling.tests/ExperienceResponseParserTests.cs) | Final answer selection, typed results, model/tool counts, and rejection of a null calculation result. |
| [ExperienceResultValidatorTests.cs](../function-calling.tests/ExperienceResultValidatorTests.cs) | Wrong totals, missing citations, and extra claims are rejected. |

From the repository root, run local checks without credentials or API access:

```sh
dotnet test 04-function-calling/function-calling.tests/function-calling.tests.csproj --filter "Category!=Integration"
```

The tests load inputs and call ExperienceInputsValidator explicitly before calculation or a model request, matching Program.cs. Input rejection assertions belong to the validator tests; loading tests check the copied data. The in-memory transport supplies scripted responses while the actual SDK and Agent Framework build messages and execute the real calculator. It is a test dependency, not an application mode. These tests make **zero real API requests**. Native tool declarations, provider routing, and thought signature preservation are tested in [AiClientFactoryTests.cs](../../shared/shared.tests/AiClientFactoryTests.cs).

Run the single real provider smoke test explicitly with:

```sh
dotnet test 04-function-calling/function-calling.tests/function-calling.tests.csproj --filter "Category=Integration"
```

Or run the complete project suite:

```sh
dotnet test 04-function-calling/function-calling.tests/function-calling.tests.csproj
```

Either command containing the integration case makes **two real model calls** on success, normally two HTTP requests and at most six HTTP attempts. It loads the same jobs, validates inputs, requests an answer with the same shared settings, checks execution, reads the typed result through ExperienceResponseParser.cs, and validates the answer. Missing credentials, API errors, and rejected model output fail the test. The full repository suite also runs earlier integration tests; use `dotnet test AIEngineeringTraining.slnx --filter "Category!=Integration"` for local checks across all projects. All commands above work in PowerShell and macOS/Linux shells.

References: [Microsoft Agent Framework function tools](https://learn.microsoft.com/en-us/agent-framework/agents/tools/function-tools), [AIFunctionFactory.Create](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.aifunctionfactory.create), [FunctionInvokingChatClient](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.functioninvokingchatclient), [official OpenAI .NET SDK](https://github.com/openai/openai-dotnet), [OpenRouter tool calling](https://openrouter.ai/docs/guides/features/tool-calling), and [Gemini OpenAI compatibility](https://ai.google.dev/gemini-api/docs/openai).
