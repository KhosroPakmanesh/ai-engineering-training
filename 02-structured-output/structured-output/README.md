# Structured Output: one small experiment

Read **Program.cs** for the extraction flow, **CvExtractor.cs** for the shared agent request, and **ProfileValidator.cs** for the JSON checks. The application loads one fictional resume, asks an agent for structured JSON, validates it, and prints and saves the typed profile.

| File | Concept |
| --- | --- |
| [profile.schema.json](profile.schema.json) | Define required fields, nullable values, and the JSON structure. |
| [CandidateProfile.cs](CandidateProfile.cs) | Represent the result with C# records. |
| [CvExtractor.cs](CvExtractor.cs) | Request structured JSON through Microsoft Agent Framework and OpenRouter. |
| [ProfileValidator.cs](ProfileValidator.cs) | Parse JSON, validate the schema and evidence, check dates, and deduplicate skills. |

The shared extraction instruction is visible in CvExtractor.cs. It tells the model to treat the resume as data, use only stated facts, and leave unknown values null. Program.cs passes the returned JSON to ProfileValidator.cs before displaying or saving it. Each call starts a fresh agent run.

## Run

Install the .NET 10 SDK. If `.env` is missing, copy `.env.example` to `.env` in this folder. Fill in your key and one available text-chat model, without quotes:

```dotenv
OPENROUTER_API_KEY=your-key
OPENROUTER_MODEL=your-model-id
```

Then run from this folder:

```sh
dotnet run
```

You can also run the project in Visual Studio. Set `structured-output` as the startup project. Builds copy the resumes, schema, and `.env` beside the executable; the program reads its files from that folder. Edit the files in the project folder and rebuild to use your changes. The first build downloads the packages if needed.

CvExtractor.cs reads these settings directly from `.env`, which Git ignores, and prints the selected model. Each run sends the fictional resume to OpenRouter and its remote provider and may charge your account: one request, up to 4,096 output tokens, with automatic retries disabled. The temperature is 0.1, and the network timeout is 120 seconds.

Use a model and provider that support JSON Schema structured output. On API failure, the program prints the HTTP status and provider error message. HTTP 429 means the provider is rate limiting requests or is at capacity. Wait and run again, or change the model in `.env`. After editing `.env`, stop any running debugging session and rebuild; the configuration is copied on every build.

The program reports empty or truncated responses. Invalid JSON or data produces error locations and exit code 1. An accepted profile is printed in the console and saved as `profile.json` beside the executable; the program prints its absolute path. A failed run does not replace a previously saved profile.

## Learn by changing a prompt

1. Predict which facts in [the resume](sample-data/resume.md) should become strings, arrays, or null.
2. Run the program and compare its profile with the resume and [the expected example](sample-data/profile.example.json).
3. Change `resumeFile` at the top of Program.cs to `resume-ambiguous.md` and run again. Check that the missing degree, phone, skill proficiency, and ambiguous start date remain null, and that SQL appears once.
4. Change one instruction in CvExtractor.cs and repeat; each run makes another request. Observe what the instruction changes and what the schema still requires.

The schema checks structure. ProfileValidator.cs checks source quotes and text facts, ISO dates, and chronology, then normalizes duplicate skills. Review factual meaning yourself: a quote containing a qualification name does not necessarily establish that the candidate earned it. Identical settings do not guarantee identical output.

## Tests

The solution includes `structured-output.tests`, a small xUnit project. Its one source file, [StructuredOutputTests.cs](../structured-output.tests/StructuredOutputTests.cs), contains four cases. Two call the same `CvExtractor.ExtractAsync` method as the console app, using the same `.env` and fictional resumes copied through the project reference. The other two check parsing a valid fixture and rejecting malformed or schema-invalid JSON.

From the repository root, run:

```sh
dotnet test 02-structured-output/structured-output.tests/structured-output.tests.csproj
```

You can also use Visual Studio's Test Explorer. A complete Lab 02 run makes two real API requests. The integration cases check for a populated typed profile and preservation of missing facts as null. Profiles appear in test output. Missing configuration, API failures, and invalid model output fail the tests. A rate limit or unavailable provider does not establish a code defect, and factual meaning still needs manual review.

Running `dotnet test AIEngineeringTraining.slnx` also runs Lab 01's three real requests, for five requests across both labs.

## Files to read

```text
Program.cs                         Main extraction flow and selected resume
CvExtractor.cs                     Shared agent request and extraction instruction
CandidateProfile.cs                Typed records and nullable facts
ProfileValidator.cs                JSON parsing, schema and data checks
profile.schema.json                Required JSON structure
.env                               Your OpenRouter key and model
.env.example                       Template for creating .env
sample-data/resume.md              Complete fictional resume
sample-data/resume-ambiguous.md    Missing facts and an ambiguous date
sample-data/profile.example.json   Expected example profile
structured-output.csproj           .NET 10 project and pinned package dependencies
```

The application displays the validated profile in the console and saves it to a file. It is a small teaching example with one agent request and a separate local validation step.

References: [Microsoft Agent Framework structured output](https://learn.microsoft.com/en-us/agent-framework/agents/structured-outputs), [official OpenAI .NET SDK](https://github.com/openai/openai-dotnet), and [OpenRouter structured output](https://openrouter.ai/docs/guides/features/structured-outputs).
