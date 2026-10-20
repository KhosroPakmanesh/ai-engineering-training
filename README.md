# ai-engineering-training

These small C#/.NET 10 experiments help you learn by changing an input and inspecting the AI's response. Open [AIEngineeringTraining.slnx](AIEngineeringTraining.slnx) to load the projects.

- [Prompt engineering](01-prompt-engineering/prompt-engineering/README.md): change the instructions given to the AI and compare three summaries of the same resume.
- [Structured output](02-structured-output/structured-output/README.md): ask the AI to turn a resume into a JSON profile, then check its format and supporting facts.
- [Context engineering](03-context-engineering/context-engineering/README.md): choose which project descriptions the AI receives and compare its answer with one based on all six descriptions.
- [Function calling](04-function-calling/function-calling/README.md): let the model select a C# calculator, merge overlapping job dates, and answer with its verified experience total.

All four applications make their AI calls through the facade in [AiProvider.cs](shared/shared/AiProvider.cs). Each lesson supplies its prompts and generation settings in an [AiRequest](shared/shared/AiRequest.cs) and receives an [AiResponse](shared/shared/AiResponse.cs) with text, token usage, and timing. Tool requests also supply ordinary C# callbacks and explicit call limits, and receive actual execution results and a model-call count. The facade owns SDK clients, agents, tool schemas and invocation, provider options, retries, deadlines, response checks, and API errors. [SettingsReader.cs](shared/shared/SettingsReader.cs) reads the selected provider into [ProviderSettings](shared/shared/ProviderSettings.cs). Changing the configured provider or model leaves lesson code unchanged; replacing the SDK requires changes only inside shared/. The [shared tests](shared/shared.tests/shared.tests.csproj) check this behavior locally, with a counterpart test file for each tested source file.

Create `shared/shared/.env` from [shared/shared/.env.example](shared/shared/.env.example). Choose an AI provider with `AI_PROVIDER`, then configure its API key and model using the [shared setup instructions](shared/shared/README.md#run). Every application reads a copy of this same file from its build output folder. Rebuild after changing it. To try a lesson, open its project folder and run `dotnet run`.

The facade currently uses Microsoft Agent Framework and the OpenAI .NET SDK to connect to either provider's Chat Completions endpoint. The learning projects reference shared/ and use its own request, response, and exception types. Requests send the lesson's fictional data to the selected remote service and may charge your account. Choose a model that supports the lesson's settings; the structured-output lesson also needs JSON Schema support, and function-calling needs function tools. See the [shared setup](shared/shared/README.md) and [Gemini compatibility documentation](https://ai.google.dev/gemini-api/docs/openai).

The shared HTTP retry policy allows two retries for temporary provider failures. Each model call normally sends one HTTP request and can make up to three attempts. A facade request has one 120-second deadline covering every model turn, tool execution, and retry wait. Each lesson's README lists its model calls and maximum HTTP attempts. The full solution test run makes eleven model calls on success and can make up to 33 HTTP attempts; deterministic tests make no real API requests.

[AGENTS.md](AGENTS.md) explains how to add projects, share the setup code and settings, and write READMEs that teach each experiment in plain language.

To create another learning project, use the repository-local [create-learning-project skill](.agents/skills/create-learning-project/SKILL.md): write `$create-learning-project`, followed by what you want the project to demonstrate. The skill follows AGENTS.md for the code, checks, and README.
