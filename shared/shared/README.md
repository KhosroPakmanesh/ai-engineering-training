# Shared Plumbing: one small library

Read **OpenRouter.cs** for the common settings reader, response checks, timeout, and API error messages. The three learning applications reference this library while keeping their actual model requests in their own experiment sources.

| Method or value | Purpose |
| --- | --- |
| ReadSettingsAsync and ParseSettings | Read the key and model from the build's .env file. |
| ReadResponse | Reject empty or truncated responses and preserve valid text. |
| RequestTimeoutSeconds | Give requests a common 120-second deadline. |
| WriteError | Print provider details and useful failure guidance. |

Each application uses the same helpers. Prompts, generation settings, sample data, and lesson-specific checks stay in the learning projects.

## Run

Install the .NET 10 SDK. Create `.env` in this folder from [.env.example](.env.example) and fill in the OpenRouter key and model. This is the repository's only source settings file, and Git ignores it.

From this folder, build the library:

```sh
dotnet build
```

shared.csproj copies `.env` to its output. Project references carry the same settings into the applications and their test outputs; rebuild after editing the source settings. The library has no console entry point. Run a learning application with `dotnet run` from that application's folder, or select it as the startup project in Visual Studio.

Building the library makes no model requests. Each application's README describes its request count, model requirements, and provider failures.

## Learn by changing a check

1. Read ParseSettings and predict how it handles comments, quotes, and equals signs in a value.
2. Compare your prediction with [OpenRouterTests.cs](../shared.tests/OpenRouterTests.cs).
3. Change a test input and run the shared tests. Observe which settings or responses are accepted.

ReadResponse returns valid text unchanged. Its checks protect the request flow; factual meaning still needs the lesson's own checks and manual review.

## Tests

[shared.tests](../shared.tests/shared.tests.csproj) contains three deterministic cases in [OpenRouterTests.cs](../shared.tests/OpenRouterTests.cs). They check settings parsing, missing or placeholder settings, and empty or truncated responses. These checks run here rather than being copied into each lesson's test project.

From the repository root, run:

```sh
dotnet test shared/shared.tests/shared.tests.csproj
```

You can also use Visual Studio's Test Explorer. This run makes zero API requests and uses fictional settings strings.

## Files to read

```text
OpenRouter.cs                        Shared helper methods and timeout
shared.csproj                        .NET 10 library and settings copy
../shared.tests/OpenRouterTests.cs   Three deterministic shared checks
../shared.tests/shared.tests.csproj  xUnit project referencing the library
.env                                 Your OpenRouter key and model
.env.example                         Settings template
```

References: [MSBuild project items and references](https://learn.microsoft.com/en-us/visualstudio/msbuild/common-msbuild-project-items).
