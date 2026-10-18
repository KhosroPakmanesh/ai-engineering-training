# ai-engineering-training

These small C#/.NET 10 experiments help you learn by changing an input and inspecting the AI's response. Open [AIEngineeringTraining.slnx](AIEngineeringTraining.slnx) to load the projects.

- [Prompt engineering](01-prompt-engineering/prompt-engineering/README.md): change the instructions given to the AI and compare three summaries of the same resume.
- [Structured output](02-structured-output/structured-output/README.md): ask the AI to turn a resume into a JSON profile, then check its format and supporting facts.
- [Context engineering](03-context-engineering/context-engineering/README.md): choose which project descriptions the AI receives and compare its answer with one based on all six descriptions.

All three applications use the [shared class library](shared/shared/README.md) for their common setup and checks. [SettingsReader.cs](shared/shared/SettingsReader.cs) reads the selected provider's settings. [AiProvider.cs](shared/shared/AiProvider.cs) creates its SDK client, checks returned text, supplies the request deadline, and explains API errors. [ProviderSettings.cs](shared/shared/ProviderSettings.cs) holds the connection settings. The [shared tests](shared/shared.tests/shared.tests.csproj) check these helpers locally without AI requests, with a counterpart test file for each tested source file.

Create `shared/shared/.env` from [shared/shared/.env.example](shared/shared/.env.example). Choose an AI provider with `AI_PROVIDER`, then configure its API key and model using the [shared setup instructions](shared/shared/README.md#run). Every application reads a copy of this same file from its build output folder. Rebuild after changing it. To try a lesson, open its project folder and run `dotnet run`.

All three applications send their model requests through Microsoft Agent Framework. The framework uses the OpenAI .NET SDK underneath to connect to either provider's Chat Completions endpoint. Requests send the lesson's fictional data to the selected remote service and may charge your account. Choose a model that supports the lesson's settings; the structured-output lesson also needs JSON Schema support. See the [shared setup](shared/shared/README.md) and [Gemini compatibility documentation](https://ai.google.dev/gemini-api/docs/openai).

The shared HTTP retry policy allows two retries for temporary provider failures. A model call normally sends one HTTP request and can make up to three attempts, all within its 120-second deadline. Each lesson's README lists its model calls and maximum HTTP attempts. The full solution test run makes nine model calls and can make up to 27 HTTP attempts; deterministic tests make no real API requests.

[AGENTS.md](AGENTS.md) explains how to add projects, share the setup code and settings, and write READMEs that teach each experiment in plain language.

To create another learning project, use the repository-local [create-learning-project skill](.agents/skills/create-learning-project/SKILL.md): write `$create-learning-project`, followed by what you want the project to demonstrate. The skill follows AGENTS.md for the code, checks, and README.
