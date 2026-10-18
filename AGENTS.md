# Repository guidance for AI agents

Keep all repository rules in this file. The project creation skill is a short workflow that refers to this guide; do not copy rules into the skill or another guide.

## Keep the learning goal visible

- The learning projects are small C#/.NET 10 console applications; shared is their common class library. Start by reading the project's README and source files.
- Keep the AiProvider.RequestAsync call, prompts, and generation settings visible in the experiment's own source file. Keep SDK clients, agents, provider-specific options, and SDK response types behind the shared facade.
- During plumbing changes, preserve existing prompts, sample data, model settings, request counts, return types, successful console output, and exported JSON formats unless the user requests a change.
- When adding a project, keep changes scoped to that project and the shared code, solution entries, and documentation it needs.
- Run projects directly with dotnet run. Keep application execution free of command-line arguments and offline or simulated API modes.
- For a comparison, state the variable being explored and keep all other inputs and settings fixed. For prompt comparisons, this includes the source data, model, and generation settings. Use independent conversations for each model call so earlier outputs do not affect later runs. Load and validate every comparison case and prepare all prompts before the first model request, so a later input problem cannot waste earlier calls.
- Use fictional sample data rich enough to demonstrate the lesson's constraints. For a summary limit, use source material longer than the permitted summary; choose its length for the lesson rather than applying a universal word count.
- Remove files made obsolete or redundant by the current change. Do not keep backups of abandoned implementations in the project or remove unrelated work.

## Keep files, methods, and types cohesive

Use the manually refactored learning applications as examples of the preferred coding style. The [structured output flow](02-structured-output/structured-output/Program.cs) shows explicit processing stages, and the [selected context scoring](03-context-engineering/context-engineering/ContextStrategies/SelectedContextStrategy.cs) shows straightforward control flow. Apply the preferences below to new code and scoped changes; preserve unrelated existing code and behavior.

- Give each file and class one clear responsibility. Prefer a focused class for a separately explainable lesson stage, such as input validation, context preparation, model requests, profile parsing, evidence validation, or normalization. Keep closely related details together within that responsibility.
- Make Program.cs and other orchestration methods show the lesson's stages in execution order, with explicit intermediate results. Call loading, preparation, requests, validation, normalization, and presentation stages directly where they apply, rather than hiding the whole experiment behind one convenience method.
- Keep separately meaningful checks and transformations visible as distinct calls. For structured output, show schema validation, conversion into records, evidence validation, and normalization in order. Preserve every check when separating stages; callers must complete the required sequence before accepting or presenting a result.
- Keep stage contracts predictable: request methods return raw model output, parsers produce typed records, validators check data and report failures, and normalizers return transformed records. Do not silently trim, deduplicate, or repair data inside a validator.
- Extract meaningful stages into cohesive, clearly named methods. Keep the related details of one operation together when they are easiest to understand in one pass, such as reading headings, constructing sections, and checking IDs during document parsing. Avoid splitting a cohesive operation into many tiny private helpers.
- Keep short, obvious operations inline when extracting them would add navigation without making the concept clearer. A direct file read or a couple of console writes do not need their own wrapper unless it adds a meaningful responsibility.
- Represent meaningful data passed between stages with named records and explicit inputs and results. Put records used by multiple stages in clearly named files; use a Types/ folder when it helps navigation. Closely related records may share a file, and implementation details used by only one class may remain private.
- Use static classes for stateless operations. Keep per-call source text, errors, and other working state local, pass that state explicitly, or use a small instance created for one operation. Do not store an operation's mutable state in static fields.
- Use clear English names from the learning task, such as SelectContext or CheckEvidence. Prefer descriptive variable names that identify the domain value or its representation, such as rawStringResume, jsonResume, comparisonInputs, preparedContexts, and comparisonReport, when shorter names would obscure the data flow. Name methods for the action and result they actually provide.
- Prefer explicit C# types for meaningful intermediate results and stage boundaries so a learner can see what each step produces. Use var when the type is obvious from a local construction and it does not hide an important result type.
- Prefer straightforward foreach loops and if/else branches when scoring or validation has several decisions. Use simple LINQ for clear filtering, projection, ordering, and deduplication; avoid nested conditional expressions or compressed expressions that make the rules harder to follow.
- Default to List<T> for new collection properties, intermediate results, and collection inputs or outputs in the learning code. Do not convert a list to an array merely because construction is finished. Reuse an existing collection when it already represents the required result; avoid ToList() or collection spreading that merely copies an already suitable collection unless an independent copy is needed. Use IEnumerable<T> for enumeration-only inputs and other collection types when their semantics or a required API justify them. Preserve existing return types during unrelated changes.
- Use named constructor arguments when a record or result has many fields, especially measurements and comparison reports. Keep short, obvious constructions positional when field meanings are easy to follow.
- Represent visibly different experiment strategies with clearly named methods or classes so the caller states which behavior it selects. Keep the number of strategy classes proportional to the lesson; do not introduce an interface or hierarchy solely to separate two simple operations.
- Keep routine configuration and error handling in shared/ so the explicit orchestration focuses on the learning area.
- Order methods from overview to detail: put the entry point or main orchestration method first, arrange its helpers in call order, and place a helper used by only one method immediately below its caller. Put helpers shared by several methods after the main stages and keep related overloads together.
- Keep constants, fields, and configuration at the top. When reordering existing methods, move each complete declaration with its comments without changing its signature, body, or calls; preserve field initializer order.
- Add a layer, interface, generic helper, or dependency only when it has a clear purpose in the lesson. Keep the number of files and methods proportional to the concepts being taught.
- Add a brief comment before each meaningful stage or method to explain its purpose, the lesson, or a non-obvious decision. Comments explain why; method names explain what. Keep obvious statements readable without repeating them in comments.
- Write comments for a beginner, using short sentences and familiar words. Explain the reason or rule in concrete terms from the project: prefer "an exact quote from the resume" to "a source span." Explain a technical term briefly when it is needed. Keep comments accurate about the code's behavior, limits, and model-call counts, and update them when the code changes. Apply this to application code, shared helpers, and tests.
- Apply these conventions throughout the learning applications, shared library, and tests. Organize the code so learners can understand the main flow without first learning a plumbing framework.

## Create a project from a description

Use the repository-local [create-learning-project skill](.agents/skills/create-learning-project/SKILL.md) when a user provides a project description and asks for a new learning project. The skill reads this guide and the existing projects before implementing the description.

- Use the user's explicit project requirements to define the behavior. Identify the concept the learner should observe, the input they can change, and the output they should inspect.
- Use existing conventions for unspecified routine details. Clarify missing information when it changes the learning objective or required behavior.
- Before implementing, read the README and source of the existing project closest to the learning concept. Inspect [the solution](AIEngineeringTraining.slnx) and [shared tests](shared/shared.tests/AiProviderTests.cs) alongside the shared plumbing described below.

Example request: `$create-learning-project Implement a small project demonstrating tool calling with a fictional resume.`

## Put reusable plumbing in shared/

- shared/ is at the repository root, beside the numbered project folders.
- Follow the same layout as the learning projects: shared/shared/ contains the class library and shared/shared.tests/ contains its xUnit tests.
- Read shared/shared/SettingsReader.cs before adding settings parsing, and shared/shared/AiProvider.cs before adding model calls, provider options, response guards, timeout values, or provider error messages.
- Extend an existing shared helper when multiple projects need the same behavior. Do not copy shared code into individual project folders.
- Keep lesson-specific prompts, data types, selection rules, schemas, and validation in their own project.
- shared/shared.tests/ mirrors the shared source files: AiProviderTests.cs checks facade requests, provider options, independent calls, responses, API errors, and cancellation; SettingsReaderTests.cs checks configuration parsing; ProviderSettingsTests.cs checks safe descriptions; and AiRetryPolicyTests.cs checks retries through the SDK pipeline. Run these in shared.tests and extend the corresponding file when changing shared behavior.

## Use one credentials file

- The only source configuration is shared/shared/.env. Create it from shared/shared/.env.example.
- Select `AI_PROVIDER=openrouter` or `AI_PROVIDER=gemini`. Use OPENROUTER_API_KEY and OPENROUTER_MODEL for OpenRouter, or GEMINI_API_KEY and GEMINI_MODEL for direct Gemini access. Only the selected provider requires credentials. An absent AI_PROVIDER defaults to OpenRouter for existing settings files. Never create another project-local .env or hardcode credentials.
- Never print credentials, include them in examples, or commit shared/shared/.env. It is ignored by Git.
- shared.csproj copies its .env to build output; project references carry that file into each executable and test output. Read that copy through SettingsReader.ReadSettingsAsync(AppContext.BaseDirectory).
- Rebuild after editing shared/shared/.env. Preserve the selected model unless changing it is part of the user's request.
- Requests send prompts and document excerpts to the selected remote service: OpenRouter and its upstream provider, or Google's Gemini API through its OpenAI-compatible endpoint.

## Add a new project

Use existing pinned package versions when suitable. Check official documentation and installed APIs when a required SDK operation is uncertain.

1. Create a folder that matches the project concept, with one console application and, when requested, one xUnit test project. Match the application name to its project folder and name the test assembly `<application-name>.tests`. Use net10.0, nullable reference types, and pinned package versions.
2. Add the application and any requested test project to AIEngineeringTraining.slnx.
3. Reference the shared class library. Do not link its source files into individual projects or add virtual shared folders. For an application two directories below the repository root, include:

```xml
<ItemGroup>
  <ProjectReference Include="../../shared/shared/shared.csproj" />
</ItemGroup>
```

4. Copy the new project's input files to its build output. Resolve inputs from AppContext.BaseDirectory so dotnet run, Visual Studio, and tests use the same files.
5. In the lesson's test project, reference the application. Shared plumbing tests belong in shared.tests:

```xml
<ItemGroup>
  <ProjectReference Include="../project-name/project-name.csproj" />
</ItemGroup>
```

6. Use SettingsReader.ReadSettingsAsync for configuration and AiProvider.RequestAsync for every model call. Declare the name, instructions, input, temperature, output limit, and optional AiJsonSchema in an AiRequest in the lesson. Consume the shared AiResponse and catch AiRequestException with AiProvider.WriteError. Keep AI SDK package references, client lifetimes, agent construction, response conversion, and provider-specific routing inside shared/ so changing the model, provider, or SDK does not require changes in lesson code. Apply provider-specific options only to the relevant provider; never silently change a lesson's settings or route a failed request to another provider.
7. Validate inputs before making API requests and declare the output limit in the experiment source. The facade uses the shared AiRetryPolicy for at most two HTTP retries on 408, 429, 500, 502, 503, or 504, with AiProvider.RequestTimeoutSeconds setting one 120-second deadline around all attempts and waits. Do not add another deadline or retry loop in a lesson. Use AiProvider.MaxRetries when displaying the retry limit, and document model calls separately from maximum HTTP attempts.
8. When tests are requested, follow the testing guidance below.
9. Follow the README format below.
10. Follow the verification guidance under Check changes.

## Keep tests small and meaningful

- Keep the suite focused on lesson requirements and failures that affect correctness, evidence, request costs, or important limits. Cover useful successful results and representative failures; do not pursue exhaustive input combinations or line coverage.
- For each production file with meaningful tests, create a counterpart named <source-file-name>Tests.cs in the test project and mirror its relative folder path. For example, ContextStrategies/SelectedContextStrategy.cs maps to context-engineering.tests/ContextStrategies/SelectedContextStrategyTests.cs. Match the test class name to its file and keep its assertions focused on that production file's behavior. Do not collect unrelated component tests in a project-wide test file.
- Choose which behavior needs testing before creating test files. Records with no behavior, trivial wrappers, console wording, and private helpers do not each need a test or an empty counterpart file. Exercise private helpers through observable behavior.
- Keep small shared fixture readers in TestData/ when several counterpart files need the same copied inputs. Return fresh test data and avoid building a general test framework.
- Let integration tests call the same request method as the application and use the existing shared settings and project inputs. Mark them with Trait("Category", "Integration"). Use deterministic tests for local checks.
- Put integration tests in the counterpart file for the request or orchestration owner, such as CvExtractorTests.cs or ContextComparerTests.cs. Check the complete required pipeline there without duplicating a test file for every collaborator it calls.
- When an application exposes preparation, validation, and normalization as separate stages, let integration tests follow the same required sequence. Keep deterministic tests focused on the stage or observable constraint they exercise.
- Strengthen an existing test when an important assertion is missing; add a new case for a distinct behavior. For controlled comparisons, assert each prepared prompt contains the original question, not just that both prompts agree with each other.
- Keep counterpart test classes independent. If an existing dependency has shared mutable state, put its test callers in the same xUnit collection until that dependency is made independent; do not disable parallel execution for unrelated tests.
- Assert observable requirements, such as nonempty results, output structure, paragraph or word limits, and evidence references required by the lesson. Avoid exact wording assertions for variable model responses unless exact wording is an explicit contract.
- Leave qualitative judgments, such as factual meaning and useful fact selection, as manual learning exercises unless automated evaluation is requested.

## README format for every project

Every project's README.md must follow the structure and writing style of the existing project READMEs. Before writing or updating one, read the [prompt engineering](01-prompt-engineering/prompt-engineering/README.md), [structured output](02-structured-output/structured-output/README.md), and [context engineering](03-context-engineering/context-engineering/README.md) examples.

For the shared class library, describe the support library and its build command using the same section order.

Keep this order, adapting the content to the project's learning concept:

1. A title in the form `# Concept: one small experiment`.
2. An opening paragraph explaining the concept and purpose in plain language, and naming the source files responsible for the main flow, request, and local checks.
3. A small concept table, followed by an explanation of the shared instruction and what changes between runs.
4. `## Execution flow`: a small top-to-bottom Mermaid flowchart showing the main steps, with a short explanation of how to read it and how important stages work.
5. `## Run`: shared configuration, `dotnet run` from the project folder, Visual Studio execution, copied inputs, exact application request counts, remote API use, model requirements, and useful failure information.
6. `## Understand the results`: explain the printed values, saved outputs, measurements, and check results that the learner needs before trying the exercises. For the shared library, explain helper return values and error messages.
7. `## Learn by changing ...`: a short numbered exercise and guidance on interpreting the results.
8. `## Files to read`: a text listing with a short purpose for each important file, including the shared helper and settings template.
9. `## Tests`: the test files, checks performed, exact command, and number of real API requests.
10. A final References line linking to relevant official documentation.

Write the execution chart in a fenced `mermaid` block using `flowchart TD`. Use plain action labels, arrows for execution order, and diamonds for important decisions or loops. Show the inputs, prompt or context preparation, model requests, local checks, and printed or saved outputs. Name the responsible source files where useful, keep request counts and call order accurate, and keep the chart aligned with the code when stages change. For the shared library, show how an application calls its helpers around the application's own request.

Keep explanations beside the section they support. Explain stage rules, such as ranking or validation, in Execution flow. Explain output values and measurements in Understand the results, before exercises that use them. Keep Run focused on setup and execution, and avoid repeating the same explanation in several sections.

When creating or modifying a README, write like a teacher explaining the experiment to a beginner:

- Explain what the concept means in plain language before discussing implementation details. Use a concrete example from the project's own sample data to make the idea easy to understand.
- Describe what information enters the program, what happens in execution order, and what the learner should inspect in the printed or saved result. Name the responsible files as part of that explanation.
- Use familiar words and short, direct sentences. Introduce technical terms only when they help the learner, and explain them when first used. Prefer a concrete action or observation over an abstract label.
- For comparisons, explain what changes and what stays fixed. Connect the result to the lesson, and make clear which checks the code performs and which judgments the learner still makes.
- When improving an existing README's language, preserve its headings, section order, concept table, exercises, commands, file list, and References line. Keep request counts, settings, paths, test descriptions, and implementation facts accurate.

Keep commands simple and describe the actual implementation and output. Link to shared/shared/AiProvider.cs and shared/shared/.env.example. Do not describe projects using numbered "Lab" labels.

## Check changes

- Use dotnet restore AIEngineeringTraining.slnx and dotnet build AIEngineeringTraining.slnx.
- Deterministic checks can run with dotnet test AIEngineeringTraining.slnx --filter "Category!=Integration".
- A full dotnet test AIEngineeringTraining.slnx run makes real API calls. Read each project's README for its request count.
- Run the appropriate checks for the changed files, including existing checks affected by shared changes. Distinguish provider availability or model constraint failures from code defects.
- Review the source from a learner's perspective: the experiment stages, meaningful data types, actual request, and local checks should be easy to find and follow.
- When moving or renaming stages, update application callers, tests, README execution charts, and file lists together. Verify that the explicit flow still performs every required check and preserves the requested outputs.
- Check README links and git diff --check. Verify shared/shared/.env remains ignored.
- When delivering a project, name the implemented files, briefly explain the concept, give the run command, and report verification results and any remaining limitations accurately.
