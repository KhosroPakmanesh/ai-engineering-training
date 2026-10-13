# Structured Output: one small experiment

Read **Program.cs** for the extraction flow, **CvExtractor.cs** for the shared agent request, **ProfileValidator.cs** for the JSON schema checks, and **ProfileChecks.cs** for the source-data checks. The application loads one fictional resume, asks an agent for structured JSON, validates it, and prints and saves the typed profile.

| File | Concept |
| --- | --- |
| [profile.schema.json](profile.schema.json) | Define required fields, nullable values, and the JSON structure. |
| [CandidateProfile.cs](CandidateProfile.cs) | Represent the result with C# records. |
| [CvExtractor.cs](CvExtractor.cs) | Request structured JSON through Microsoft Agent Framework and OpenRouter. |
| [ProfileValidator.cs](ProfileValidator.cs) | Check the JSON schema, coordinate data validation, and deduplicate skills. |
| [ProfileChecks.cs](ProfileChecks.cs) | Check evidence, text facts, date precision, chronology, and stated durations. |

The shared extraction instruction is visible in CvExtractor.cs. It tells the model to treat the resume as data, use only stated facts, and leave unknown values null. Program.cs passes the returned JSON to ProfileValidator.cs before displaying or saving it. Each call starts a fresh agent run.

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

You can also run the project in Visual Studio. Set `structured-output` as the startup project. Builds copy the resumes and schema, together with `shared/shared/.env`, beside the executable; the program reads its files from that folder. Edit the files in the project folder and rebuild to use your changes. The first build downloads the packages if needed.

[OpenRouter.cs](../../shared/shared/OpenRouter.cs) reads the build's copy of `shared/shared/.env`, which Git ignores. CvExtractor.cs prints the selected model. Each run sends the fictional resume to OpenRouter and its remote provider and may charge your account: one request, up to 4,096 output tokens, with automatic retries disabled. The temperature is 0.1, and each request has a 120-second deadline.

Use a model and provider that support JSON Schema structured output. On API failure, the program prints the HTTP status and provider error message. HTTP 429 means the provider is rate limiting requests or is at capacity. Wait and run again, or change the model in `shared/shared/.env`. After editing `shared/shared/.env`, stop any running debugging session and rebuild; the configuration is copied on every build.

The program reports empty or truncated responses. Invalid JSON or data produces error locations and exit code 1. An accepted profile is printed in the console and saved as `profile.json` beside the executable; the program prints its absolute path. A failed run does not replace a previously saved profile.

## Learn by changing a prompt

1. Predict which facts in [the resume](sample-data/resume.md) should become strings, arrays, or null.
2. Run the program and compare its profile with the resume and [the expected example](sample-data/profile.example.json).
3. Change `resumeFile` at the top of Program.cs to `resume-ambiguous.md` and run again. Check that the missing degree, phone, skill proficiency, and ambiguous start date remain null, and that SQL appears once.
4. Change one instruction in CvExtractor.cs and repeat; each run makes another request. Observe what the instruction changes and what the schema still requires.

The schema checks structure. ProfileChecks.cs checks source quotes and text facts, ISO dates, and chronology; ProfileValidator.cs then normalizes duplicate skills. Review factual meaning yourself: a quote containing a qualification name does not necessarily establish that the candidate earned it. Identical settings do not guarantee identical output.

## Tests

The solution includes `structured-output.tests`, a small xUnit project. [StructuredOutputTests.cs](../structured-output.tests/StructuredOutputTests.cs) contains seven cases. Two call the same `CvExtractor.ExtractAsync` method as the console app, using the same shared settings and fictional resumes copied through the project reference. The five local cases check fixture parsing, malformed or schema-invalid JSON, source facts, partial dates and chronology, and skill normalization. Common configuration and response checks run separately in [shared.tests](../../shared/shared.tests/shared.tests.csproj).

From the repository root, run:

```sh
dotnet test 02-structured-output/structured-output.tests/structured-output.tests.csproj
```

You can also use Visual Studio's Test Explorer. A complete test run makes two real API requests. The integration cases check for a populated typed profile and preservation of missing facts as null. Profiles appear in test output. Missing configuration, API failures, and invalid model output fail the tests. A rate limit or unavailable provider does not establish a code defect, and factual meaning still needs manual review.

## Files to read

```text
Program.cs                                    Main extraction flow and selected resume
CvExtractor.cs                                Shared agent request and extraction instruction
CandidateProfile.cs                           Typed records and nullable facts
ProfileValidator.cs                           JSON schema checks and skill normalization
ProfileChecks.cs                              Source facts, evidence, dates, and durations
profile.schema.json                           Required JSON structure
../../shared/shared/OpenRouter.cs             Shared configuration, response checks, and API errors
../../shared/shared.tests/OpenRouterTests.cs  Shared plumbing checks in shared.tests
../../shared/shared/.env                      Your OpenRouter key and model
../../shared/shared/.env.example              Template for creating the shared settings
sample-data/resume.md                         Complete fictional resume
sample-data/resume-ambiguous.md               Missing facts and an ambiguous date
sample-data/profile.example.json              Expected example profile
structured-output.csproj                      .NET 10 project and pinned package dependencies
```

The application displays the validated profile in the console and saves it to a file. It is a small teaching example with one agent request and a separate local validation step.

References: [Microsoft Agent Framework structured output](https://learn.microsoft.com/en-us/agent-framework/agents/structured-outputs), [official OpenAI .NET SDK](https://github.com/openai/openai-dotnet), and [OpenRouter structured output](https://openrouter.ai/docs/guides/features/structured-outputs).
