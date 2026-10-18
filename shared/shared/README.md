# Shared Plumbing: one small library

This library holds the setup and checks that all three learning applications need. **SettingsReader.cs** selects an AI provider and loads the shared settings file. **AiProvider.cs** creates the SDK client, checks returned text, supplies a request deadline, and displays API errors. **AiRetryPolicy.cs** retries temporary HTTP failures, and **ProviderSettings.cs** holds the selected connection values. Each lesson keeps its own prompts, generation settings, and actual model request in its experiment source.

| Method or value | Purpose |
| --- | --- |
| SettingsReader.ReadSettingsAsync and ParseSettings | Select the provider and read its key and model from the copied .env file. |
| CreateChatClient | Create an OpenAI SDK client using the selected endpoint and shared retry policy. |
| AiRetryPolicy.MaxRetries | Allow at most two retries after the first HTTP attempt. |
| ReadResponse | Return valid text unchanged; stop if the answer is empty or cut short. |
| AiProvider.RequestTimeoutSeconds | Set the common 120-second request deadline. |
| WriteError | Show the provider's error details and suggest what to check next. |

The learning applications call these helpers before and after their model requests. Changing `AI_PROVIDER` selects the matching key, model, and endpoint for every lesson. Keeping this setup in one library means a common check can be maintained in one place. The prompts, sample data, generation settings, and checks that teach each lesson stay in that lesson's project.

## Execution flow

The flow starts when a learning application calls this library. The application sends its own model request and uses the shared helpers around that request.

```mermaid
flowchart TD
    A["Learning application<br/>Calls SettingsReader.ReadSettingsAsync"] --> B{"SettingsReader.cs<br/>Are the copied .env settings present and filled in?"}
    B -->|Yes| C["AiProvider.cs<br/>CreateChatClient selects the endpoint and retry policy"]
    C --> D["Learning application<br/>Run the agent with a 120-second deadline"]
    D --> R["SDK<br/>Send the HTTP request"]
    R -->|Answer| E{"AiProvider.cs<br/>Does ReadResponse accept this text?"}
    E -->|Yes| F["Learning application<br/>Use the response in its own lesson"]
    R -->|API error| Q{"AiRetryPolicy.cs<br/>Temporary failure and retries left?"}
    Q -->|Yes| W["Log the retry and wait<br/>Honor Retry-After; add jitter"]
    W --> R
    Q -->|No| G["AiProvider.cs<br/>WriteError shows the API error and advice"]
    G --> H["Learning application<br/>Stops with exit code 1"]
    B -->|No| H
    E -->|No| H
```

The library reads and checks the selected provider's settings, creates the client, supplies the timeout value, and checks or explains responses. OpenRouter uses `https://openrouter.ai/api/v1`; Gemini uses Google's OpenAI-compatible endpoint at `https://generativelanguage.googleapis.com/v1beta/openai/`. The model call belongs to the learning application. Retries resend the same HTTP request inside the SDK; they do not restart the agent or switch providers. A permanent error or a failure after the retries stops the lesson. Building the library or running its own tests makes zero real model requests.

AiRetryPolicy allows two retries for HTTP 408, 429, 500, 502, 503, and 504. It waits about 2 seconds before the first retry and 4 seconds before the second. Each wait adds 0-0.5 seconds of random variation, called jitter, so several clients are less likely to retry at the same instant. The SDK reads `Retry-After` headers, including dates; the policy waits at least that long when the provider asks for a longer delay. Cancellation and the lesson's 120-second deadline stop requests and waits. Authentication, configuration, and other HTTP errors are not retried, nor are local checks or network exceptions without an HTTP response.

## Run

Install the .NET 10 SDK. Create `.env` in this folder from [.env.example](.env.example). All learning projects use this one source settings file. Git ignores it so your credentials are not committed.

For OpenRouter, set `AI_PROVIDER=openrouter` and fill in `OPENROUTER_API_KEY` and `OPENROUTER_MODEL`. For direct Gemini access, set `AI_PROVIDER=gemini` and fill in `GEMINI_API_KEY` and `GEMINI_MODEL`. Use a model ID available to your account on that service. Once both providers have valid settings, change only `AI_PROVIDER` to switch every lesson, then rebuild. Only the selected provider requires a key and model; unused values may remain as placeholders. Existing `.env` files without `AI_PROVIDER` continue to use OpenRouter. A blank or unsupported selector is rejected.

From this folder, build the library:

```sh
dotnet build
```

shared.csproj copies `.env` into its build output folder. Because the applications and tests reference this library, they receive the same settings file in their output folders too. Rebuild after changing the source settings. This is a class library, so it has no console entry point to run on its own. To try a lesson, run `dotnet run` from that application's folder or select it as the startup project in Visual Studio.

Building this library makes no model requests. Running a lesson sends its prompts and fictional source text to OpenRouter and its upstream provider, or directly to Google's Gemini API, and may charge your account. Each application's README distinguishes model calls from HTTP attempts: one model call normally sends one request but can send up to three attempts with retries. The shared client uses the pinned OpenAI .NET SDK 2.14.0. Each lesson applies a 120-second total deadline to a model call, including all attempts and waits; the SDK network timeout also uses that value.

Choose a model that accepts the lesson's temperature and output token limit. The structured-output lesson also requires support for its JSON Schema. Gemini supports a subset of JSON Schema. Provider support can differ, so switching the configuration does not guarantee identical answers or feature support. The lessons keep their settings fixed rather than silently adjusting them for another model.

## Understand the results

SettingsReader.ReadSettingsAsync returns a `ProviderSettings` record with the provider's display name, key, model, and endpoint after checking that the selected key and model are filled in and are not example placeholders. `ToString()` shows only the name and model; it omits the key. This local check does not confirm that the key works or the model is available; the application's API request finds those problems. AiProvider.CreateChatClient uses those values to configure the SDK without making a request.

ReadResponse checks whether an answer is present and complete. It returns valid text exactly as received. This does not check whether the facts are correct; each lesson has its own source checks, and you still review the answer's meaning yourself.

If a request fails, WriteError prints the HTTP status, available provider details, and advice about what to check. For authentication or model failures, check the key or model belonging to `AI_PROVIDER`. HTTP 403 can indicate missing access permissions, and HTTP 429 can indicate quota, rate limits, or a busy service. Read that message in the learning application's console.

Before a retry, the policy writes a message such as `HTTP 503. Retry 1/2 in 2.3 seconds.` to the error output. A successful first attempt prints no retry messages. After two retries fail, the application shows the last provider error. Retries may make additional paid requests and do not guarantee capacity will become available. A long `Retry-After` wait can use the remaining deadline and end in cancellation without another attempt.

## Learn by changing a check

1. Read ParseSettings in SettingsReader.cs. Predict what it does with a comment line, a quoted model name, and an equals sign inside a value.
2. Compare your prediction with [AiProviderTests.cs](../shared.tests/AiProviderTests.cs). These tests use made-up settings, so you can explore the parsing rules without a real API key.
3. Change one test input and run the shared tests. Observe why the settings or response are accepted or rejected.

## Files to read

```text
SettingsReader.cs                       Provider selection, settings parsing, and configuration checks
AiProvider.cs                           Client setup, response checks, request deadline, and errors
AiRetryPolicy.cs                        Bounded HTTP retries, provider delays, and retry messages
ProviderSettings.cs                     Selected provider, key, model, and endpoint
shared.csproj                           .NET 10 library, SDK dependency, and settings copy
../shared.tests/AiProviderTests.cs       Client setup and response checks
../shared.tests/SettingsReaderTests.cs   Provider selection and settings parsing checks
../shared.tests/ProviderSettingsTests.cs Settings descriptions that omit credentials
../shared.tests/AiRetryPolicyTests.cs    SDK pipeline retry and cancellation checks
../shared.tests/shared.tests.csproj     xUnit project referencing the library
.env                                    Provider selection, keys, and models
.env.example                            Settings template
```

## Tests

[shared.tests](../shared.tests/shared.tests.csproj) contains local tests in files that match the shared source files.

| Test file | Checks |
| --- | --- |
| [SettingsReaderTests.cs](../shared.tests/SettingsReaderTests.cs) | Parsing, the default OpenRouter selection, unused credentials, unsupported selectors, missing values, and unchanged placeholders. |
| [AiProviderTests.cs](../shared.tests/AiProviderTests.cs) | Selected provider values and the SDK model and endpoint; response checks reject empty or unfinished text and preserve valid text. |
| [ProviderSettingsTests.cs](../shared.tests/ProviderSettingsTests.cs) | Settings descriptions omit credentials. |
| [AiRetryPolicyTests.cs](../shared.tests/AiRetryPolicyTests.cs) | Unchanged request bodies, recovery, exhaustion after three attempts, permanent errors, exponential delays, Retry-After seconds and dates, cancellation, successful first attempts, and synchronous calls. |

Retry tests run the real SDK pipeline against an in-memory HTTP handler. They record scheduled waits without sleeping. These common checks are tested here once for all the lessons.

From the repository root, run:

```sh
dotnet test shared/shared.tests/shared.tests.csproj
```

You can also use Visual Studio's Test Explorer. This test run makes **zero API requests**. It uses made-up settings and responses, so it can check the helpers without contacting a model.

References: [MSBuild project items and references](https://learn.microsoft.com/en-us/visualstudio/msbuild/common-msbuild-project-items), [official OpenAI .NET SDK](https://github.com/openai/openai-dotnet), [ClientRetryPolicy](https://learn.microsoft.com/en-us/dotnet/api/system.clientmodel.primitives.clientretrypolicy), [Gemini retry guidance](https://ai.google.dev/gemini-api/docs/troubleshooting#retry-strategy), and [Gemini OpenAI compatibility](https://ai.google.dev/gemini-api/docs/openai).
