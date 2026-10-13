# ai-engineering-training

Open [AIEngineeringTraining.slnx](AIEngineeringTraining.slnx) to load the projects.

- [Prompt engineering lab](01-prompt-engineering/prompt-engineering/README.md): compare three prompt styles using one fictional resume.
- [Structured output lab](02-structured-output/structured-output/README.md): extract a CV into a typed JSON profile, then validate its schema, dates, and source evidence.
- [Context engineering lab](03-context-engineering/context-engineering/README.md): compare full and selected project context for the same question, including prompt size, citations, and request timing.

All applications reference the [shared class library](shared/shared/README.md). [OpenRouter.cs](shared/shared/OpenRouter.cs) handles configuration, response checks, request deadlines, and API errors. [OpenRouterTests.cs](shared/shared.tests/OpenRouterTests.cs) supplies three deterministic checks in the separate shared.tests project; these checks run once per solution test run.

Create `shared/shared/.env` from [shared/shared/.env.example](shared/shared/.env.example) and fill in your OpenRouter key and model. This is the only source settings file. Builds copy it beside each executable and test assembly; rebuild after changing it. Run each application with `dotnet run` from its project folder.

[AGENTS.md](AGENTS.md) explains how to add projects, reuse shared code and credentials, and keep every project README consistent with the existing examples.

To create a project from a description, use the repository-local [create-learning-project skill](.agents/skills/create-learning-project/SKILL.md): `$create-learning-project`, followed by your project description. It follows the recorded guidance on cohesive files, meaningful methods, simple types, shared plumbing, and README structure.
