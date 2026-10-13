# Context Engineering: one small experiment

Read **Program.cs** for the comparison flow, **ContextSelector.cs** for context selection, **ContextComparison.cs** for the agent requests, and **ContextEvidence.cs** for the evidence checks. The application loads six fictional projects, asks the same question with two different contexts, and prints the answers, prompt sizes, and request times.

| Context | Concept |
| --- | --- |
| FULL | Include every project section, within a declared size cap. |
| SELECTED | Rank sections by keyword and heading matches, then include up to two relevant sections. |

The shared system instruction is visible in ContextComparison.cs. It tells the model to treat project text as data, answer using only supplied facts, cite section IDs, and include exact evidence quotes. Each call starts a fresh agent conversation with the same question, model, instruction, and generation settings. Only the included project sections change.

## Run

Install the .NET 10 SDK. If `shared/shared/.env` is missing, copy [shared/shared/.env.example](../../shared/shared/.env.example) to `shared/shared/.env` in the repository root. Every project uses this settings file. Fill in your key and one available text-chat model, without quotes:

```dotenv
OPENROUTER_API_KEY=your-key
OPENROUTER_MODEL=your-model-id
```

Then run from this folder:

```sh
dotnet run
```

You can also run the project in Visual Studio. Set `context-engineering` as the startup project. Builds copy the project data and question fixtures, together with `shared/shared/.env`, beside the executable; the program reads its files from that folder. Edit the files in the project folder and rebuild to use your changes. The first build downloads the packages if needed. To restore and build separately, use `dotnet restore` and `dotnet build` from this folder. These commands work in Windows PowerShell and on macOS/Linux.

[OpenRouter.cs](../../shared/shared/OpenRouter.cs) reads the build's copy of `shared/shared/.env`, which Git ignores. ContextComparison.cs prints the selected model. Each run sends fictional project excerpts to OpenRouter and its remote provider and may charge your account: two requests, up to 4,096 output tokens each, with automatic retries disabled. The same temperature (0.2) is used each time, and each request has a 120-second timeout.

Use a model and provider that support Chat Completions, temperature, and the output token limit. On API failure, the program prints the HTTP status and provider error message. HTTP 429 means the provider is rate limiting requests or is at capacity. Wait and run again, or change the model in `shared/shared/.env`. After editing `shared/shared/.env`, stop any running debugging session and rebuild; the configuration is copied on every build.

Both contexts have an 8,000-character cap, and both complete prompts are checked against a 12,000-character cap before any API request. Oversized inputs are rejected rather than clipped. Empty or provider-truncated responses are also rejected; on reasoning models, the output budget includes thinking tokens.

The comparison is printed in the console and saved as `comparison.json` beside the executable; the program prints its absolute path. The report contains the selection rules, included and cited section IDs, prompt sizes, token estimates, provider usage when available, request times, source recall, answers, and evidence flags. A flagged comparison is saved and exits with code 1. An API failure stops the comparison and does not replace a previously saved report.

## Learn by changing the context

1. Predict which sections in [the fictional projects](sample-data/projects.md) are needed to answer `Which project used PostgreSQL?`.
2. Run the program. FULL should include P1-P6 and SELECTED should include P1. Compare both answers with the source, check their citations and quotes, and compare prompt sizes and request times.
3. Change `question` at the top of Program.cs to `Which project used Kubernetes?` and run again. Then try `Which project used Salesforce?`: both answers should be `I don't have enough evidence to answer this question.`
4. Change one project description or the selection rule in ContextSelector.cs and repeat; each run makes another two requests. Observe which passages are selected and how the answer changes.

ContextSelector.cs ignores common question words and scores each distinct keyword as 3 for a heading match, otherwise 1 for a match in the section text. Positive scores are sorted descending, ties follow document order, and up to two sections are selected. Selection needs no embeddings or extra model request. A question without matching keywords produces an empty SELECTED context.

Prompt token counts are estimates: prompt characters divided by four, rounded up. This excludes message framing and is not a model-specific tokenizer. Provider token counts remain `null` when unavailable. Source recall measures how many expected section IDs were included, using [the question fixtures](sample-data/questions.json); it is not applicable without a positive fixture.

ContextEvidence.cs rejects unknown or excluded citation IDs, missing citations, and missing exact quotations. Both `[P1]` and `【P1】` citation styles are recognized. Fixture questions also check whether uncertainty was appropriate. Review factual meaning yourself: a valid citation and quotation do not prove that every claim in the answer is supported. Identical settings do not guarantee identical output, and a shorter prompt does not guarantee a faster response.

## Tests

The solution includes `context-engineering.tests`, a small xUnit project. [ContextTests.cs](../context-engineering.tests/ContextTests.cs) contains ten cases. Eight check deterministic selection, context limits, citation and quotation validation, duplicate section IDs, and consistent prompt construction. Two call the same `ContextComparison.CompareAsync` method as the console app, using the same shared settings and sample files copied through the project reference. Common configuration and response checks run separately in [shared.tests](../../shared/shared.tests/shared.tests.csproj).

From the repository root, run:

```sh
dotnet test 03-context-engineering/context-engineering.tests/context-engineering.tests.csproj
```

You can also use Visual Studio's Test Explorer. A complete test run makes four real API requests. The integration cases check both context strategies for a supported PostgreSQL question and an unsupported Salesforce question. Comparison reports appear in test output. Missing configuration, API failures, and invalid model output fail the tests. A rate limit or unavailable provider does not establish a code defect, and factual meaning still needs manual review.

## Files to read

```text
Program.cs                                    Main comparison flow and selected question
ContextSelector.cs                            Markdown sections, keyword ranking, and context cap
ContextComparison.cs                          Agent requests and comparison measurements
ContextEvidence.cs                            Citation and exact-quotation checks
ComparisonReport.cs                           Fixture and exported report records
../../shared/shared/OpenRouter.cs             Shared configuration, response checks, and API errors
../../shared/shared.tests/OpenRouterTests.cs  Shared plumbing checks in shared.tests
../../shared/shared/.env                      Your OpenRouter key and model
../../shared/shared/.env.example              Template for creating the shared settings
sample-data/projects.md                       Six fictional project records
sample-data/questions.json                    Expected source IDs and an unsupported question
context-engineering.csproj                    .NET 10 project and pinned package dependencies
```

The application displays the comparison in the console and saves it to a file. It is a small teaching example with two agent requests and local context selection. Its pinned packages are `OpenAI` 2.14.0, `Microsoft.Extensions.AI.OpenAI` 10.10.1, and `Microsoft.Agents.AI` 1.24.0. Tests use `Microsoft.NET.Test.Sdk` 18.10.1, `xunit` 2.9.3, and `xunit.runner.visualstudio` 4.0.1.

References: [Microsoft Agent Framework agents](https://learn.microsoft.com/en-us/agent-framework/concepts/agents), [official OpenAI .NET SDK](https://github.com/openai/openai-dotnet), and [OpenRouter Chat Completions](https://openrouter.ai/docs/api/api-reference/chat/create-a-chat-completion).
