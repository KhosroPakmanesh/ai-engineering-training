# Repository guidance for AI agents

Keep all repository rules in this file. The project creation skill is a short workflow that refers to this guide; do not copy rules into the skill or another guide.

## Keep the learning goal visible

- The learning projects are small C#/.NET 10 console applications; shared is their common class library. Start by reading the project's README and source files.
- Keep the actual SDK or Agent Framework request, prompts, and generation settings visible in the experiment's own source file.
- During plumbing changes, preserve existing prompts, sample data, model settings, request counts, return types, successful console output, and exported JSON formats unless the user requests a change.
- When adding a project, keep changes scoped to that project and the shared code, solution entries, and documentation it needs.
- Run projects directly with dotnet run. Keep application execution free of command-line arguments and offline or simulated API modes.
- For a comparison, state the variable being explored and keep all other inputs and settings fixed. For prompt comparisons, this includes the source data, model, and generation settings. Use independent conversations for each model call so earlier outputs do not affect later runs.
- Use fictional sample data rich enough to demonstrate the lesson's constraints. For a summary limit, use source material longer than the permitted summary; choose its length for the lesson rather than applying a universal word count.
- Remove files made obsolete or redundant by the current change. Do not keep backups of abandoned implementations in the project or remove unrelated work.

## Keep files, methods, and types cohesive

- Give each file and class one clear responsibility. Keep closely related code together; split a file when its responsibilities need separate explanations.
- Extract meaningful stages into cohesive, clearly named methods so a learner can follow the experiment without reading a large method that handles many responsibilities. Each method should do one thing; its name should explain the action.
- Keep short, obvious operations inline when extracting them would add navigation without making the concept clearer.
- Represent meaningful units of data and behavior with the simplest suitable construct: a record for related data, a static class for stateless operations, or a small instance class when state makes the concept easier to understand.
- Use clear English names from the learning task, such as SelectContext or CheckEvidence. Prefer explicit inputs and results that make the data flow easy to follow.
- Keep Program.cs and other orchestration methods as readable sequences of those stages. Keep routine configuration and error handling in shared/ so the learner can focus on the learning area.
- Order methods from overview to detail: put the entry point or main orchestration method first, arrange its helpers in call order, and place a helper used by only one method immediately below its caller. Put helpers shared by several methods after the main stages and keep related overloads together.
- Keep constants, fields, and configuration at the top. When reordering existing methods, move each complete declaration with its comments without changing its signature, body, or calls; preserve field initializer order.
- Add a layer, interface, generic helper, or dependency only when it has a clear purpose in the lesson. Keep the number of files and methods proportional to the concepts being taught.
- Add a brief comment before each meaningful stage or method to explain its purpose, the lesson, or a non-obvious decision. Comments explain why; method names explain what. Keep obvious statements readable without repeating them in comments.
- Apply these conventions throughout the learning applications, shared library, and tests. Organize the code so learners can understand the main flow without first learning a plumbing framework.

## Create a project from a description

Use the repository-local [create-learning-project skill](.agents/skills/create-learning-project/SKILL.md) when a user provides a project description and asks for a new learning project. The skill reads this guide and the existing projects before implementing the description.

- Use the user's explicit project requirements to define the behavior. Identify the concept the learner should observe, the input they can change, and the output they should inspect.
- Use existing conventions for unspecified routine details. Clarify missing information when it changes the learning objective or required behavior.
- Before implementing, read the README and source of the existing project closest to the learning concept. Inspect [the solution](AIEngineeringTraining.slnx) and [shared tests](shared/shared.tests/OpenRouterTests.cs) alongside the shared plumbing described below.

Example request: `$create-learning-project Implement a small project demonstrating tool calling with a fictional resume.`

## Put reusable plumbing in shared/

- shared/ is at the repository root, beside the numbered project folders.
- Follow the same layout as the learning projects: shared/shared/ contains the class library and shared/shared.tests/ contains its xUnit tests.
- Read shared/shared/OpenRouter.cs before adding settings parsing, response guards, timeout values, or provider error messages.
- Extend an existing shared helper when multiple projects need the same behavior. Do not copy shared code into individual project folders.
- Keep lesson-specific prompts, data types, selection rules, schemas, and validation in their own project.
- shared/shared.tests/OpenRouterTests.cs holds deterministic tests for the common helpers. Run these in shared.tests and extend them when changing shared behavior.

## Use one credentials file

- The only source configuration is shared/shared/.env. Create it from shared/shared/.env.example.
- Use OPENROUTER_API_KEY and OPENROUTER_MODEL. Never create another project-local .env or hardcode credentials.
- Never print credentials, include them in examples, or commit shared/shared/.env. It is ignored by Git.
- shared.csproj copies its .env to build output; project references carry that file into each executable and test output. Read that copy through OpenRouter.ReadSettingsAsync(AppContext.BaseDirectory).
- Rebuild after editing shared/shared/.env. Preserve the selected model unless changing it is part of the user's request.
- Requests send prompts and document excerpts to OpenRouter and its remote provider.

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

6. Use OpenRouter.ReadSettingsAsync for configuration, OpenRouter.ReadResponse for empty/truncated response checks, OpenRouter.WriteError for API errors, and OpenRouter.RequestTimeoutSeconds for network and cancellation deadlines.
7. Validate inputs before making API requests. Keep retries disabled and declare the output limit in the experiment source.
8. When tests are requested, follow the testing guidance below.
9. Follow the README format below.
10. Follow the verification guidance under Check changes.

## Keep tests small and meaningful

- Use a small test project with as few cohesive test source files as practical. Split them when responsibilities need separate explanations.
- Let integration tests call the same request method as the application and use the existing shared settings and project inputs. Mark them with Trait("Category", "Integration"). Use deterministic tests for local checks.
- Assert observable requirements, such as nonempty results, output structure, paragraph or word limits, and evidence references required by the lesson. Avoid exact wording assertions for variable model responses unless exact wording is an explicit contract.
- Leave qualitative judgments, such as factual meaning and useful fact selection, as manual learning exercises unless automated evaluation is requested.

## README format for every project

Every project's README.md must follow the structure and writing style of the existing project READMEs. Before writing or updating one, read the [prompt engineering](01-prompt-engineering/prompt-engineering/README.md), [structured output](02-structured-output/structured-output/README.md), and [context engineering](03-context-engineering/context-engineering/README.md) examples.

For the shared class library, describe the support library and its build command using the same section order.

Keep this order, adapting the content to the project's learning concept:

1. A title in the form `# Concept: one small experiment`.
2. An opening paragraph naming the source files responsible for the main flow, request, and local checks.
3. A small concept table, followed by an explanation of the shared instruction and what changes between runs.
4. `## Execution flow`: a small top-to-bottom Mermaid flowchart showing the main steps, followed by a short explanation of how to read it.
5. `## Run`: shared configuration, `dotnet run` from the project folder, Visual Studio execution, copied inputs, exact application request counts, remote API use, model requirements, and useful failure information.
6. `## Learn by changing ...`: a short numbered exercise and guidance on interpreting the results.
7. `## Tests`: the test files, checks performed, exact command, and number of real API requests.
8. `## Files to read`: a text listing with a short purpose for each important file, including the shared helper and settings template.
9. A final References line linking to relevant official documentation.

Write the execution chart in a fenced `mermaid` block using `flowchart TD`. Use plain action labels, arrows for execution order, and diamonds for important decisions or loops. Show the inputs, prompt or context preparation, model requests, local checks, and printed or saved outputs. Name the responsible source files where useful, keep request counts and call order accurate, and keep the chart aligned with the code when stages change. For the shared library, show how an application calls its helpers around the application's own request.

When creating or modifying a README, write like a teacher explaining the experiment to a beginner:

- Explain what the concept means in plain language before discussing implementation details. Use a concrete example from the project's own sample data to make the idea easy to understand.
- Describe what information enters the program, what happens in execution order, and what the learner should inspect in the printed or saved result. Name the responsible files as part of that explanation.
- Use familiar words and short, direct sentences. Introduce technical terms only when they help the learner, and explain them when first used. Prefer a concrete action or observation over an abstract label.
- For comparisons, explain what changes and what stays fixed. Connect the result to the lesson, and make clear which checks the code performs and which judgments the learner still makes.
- When improving an existing README's language, preserve its headings, section order, concept table, exercises, commands, file list, and References line. Keep request counts, settings, paths, test descriptions, and implementation facts accurate.

Keep commands simple and describe the actual implementation and output. Link to shared/shared/OpenRouter.cs and shared/shared/.env.example. Do not describe projects using numbered "Lab" labels.

## Check changes

- Use dotnet restore AIEngineeringTraining.slnx and dotnet build AIEngineeringTraining.slnx.
- Deterministic checks can run with dotnet test AIEngineeringTraining.slnx --filter "Category!=Integration".
- A full dotnet test AIEngineeringTraining.slnx run makes real API calls. Read each project's README for its request count.
- Run the appropriate checks for the changed files, including existing checks affected by shared changes. Distinguish provider availability or model constraint failures from code defects.
- Review the source from a learner's perspective: the experiment stages, meaningful data types, actual request, and local checks should be easy to find and follow.
- Check README links and git diff --check. Verify shared/shared/.env remains ignored.
- When delivering a project, name the implemented files, briefly explain the concept, give the run command, and report verification results and any remaining limitations accurately.
