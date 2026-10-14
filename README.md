# ai-engineering-training

These small C#/.NET 10 experiments help you learn by changing an input and inspecting the AI's response. Open [AIEngineeringTraining.slnx](AIEngineeringTraining.slnx) to load the projects.

- [Prompt engineering](01-prompt-engineering/prompt-engineering/README.md): change the instructions given to the AI and compare three summaries of the same resume.
- [Structured output](02-structured-output/structured-output/README.md): ask the AI to turn a resume into a JSON profile, then check its format and supporting facts.
- [Context engineering](03-context-engineering/context-engineering/README.md): choose which project descriptions the AI receives and compare its answer with one based on all six descriptions.

All three applications use the [shared class library](shared/shared/README.md) for their common setup and checks. [OpenRouter.cs](shared/shared/OpenRouter.cs) reads the settings, checks returned text, supplies the request deadline, and explains API errors. [OpenRouterTests.cs](shared/shared.tests/OpenRouterTests.cs) contains three local tests for these helpers. They run once per solution test run and make no AI requests.

Create `shared/shared/.env` from [shared/shared/.env.example](shared/shared/.env.example) and fill in your OpenRouter key and model. Every application reads a copy of this same file from its build output folder. Rebuild after changing it. To try a lesson, open its project folder and run `dotnet run`.

[AGENTS.md](AGENTS.md) explains how to add projects, share the setup code and settings, and write READMEs that teach each experiment in plain language.

To create another learning project, use the repository-local [create-learning-project skill](.agents/skills/create-learning-project/SKILL.md): write `$create-learning-project`, followed by what you want the project to demonstrate. The skill follows AGENTS.md for the code, checks, and README.
