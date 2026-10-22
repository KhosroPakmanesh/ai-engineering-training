# Shared Plumbing: one small library

This library hides the connection differences between OpenRouter and Gemini while the learning applications use Microsoft Agent Framework directly. **AiClientFactory.cs** manually registers lightweight provider strategies in its constructor and selects one to create an IChatClient, the framework's common model-client interface. **Providers/OpenRouterProvider.cs** and **Providers/GeminiProvider.cs** own their names, configuration keys, endpoints, and provider-specific options. **SettingsReader.cs** uses those same registrations to read the selected connection into **ProviderSettings**. Chat lessons create their agents, declare prompts and tools, call RunAsync with native cancellation, and check native responses themselves. Embedding lessons use factory.CreateEmbeddingGenerator to obtain `Microsoft.Extensions.AI.IEmbeddingGenerator<string, Embedding<float>>`. Requests, model and vector validation, normalization, and ranking remain visible in the lesson.

| Method or value | Purpose |
| --- | --- |
| new AiClientFactory() | Register internal OpenRouterProvider and GeminiProvider strategy objects. |
| factory.CreateClient | Let the selected strategy create a disposable IChatClient. |
| factory.CreateEmbeddingGenerator | Let the selected strategy create a disposable Microsoft IEmbeddingGenerator. |
| SettingsReader.ReadSettingsAsync and ParseSettings | Check the selected key and chat model; useEmbeddingModel: true selects its separate embedding model. |
| ProviderSettings | Hold the selected provider's connection values without printing credentials. |
| AiClientFactory.RequestTimeoutSeconds | Supply the common 120-second value for the SDK and each lesson's native deadline. |
| AiClientFactory.MaxRetries | Expose the two-retry limit without exposing the internal HTTP policy. |

The factory is the only shared AI entry point. Its constructor creates no SDK clients and needs no credentials. Only the selected strategy creates a client. Adding another provider means implementing an internal strategy and registering it in that constructor; existing lesson requests continue to use IChatClient. Both current strategies reuse the same internal OpenAI-compatible transport without combining their configuration details in the factory.

## Execution flow

Follow the arrows from settings to the selected provider, then through the lesson's own framework call. The factory selects inference clients; the lesson owns its requests and local checks.

```mermaid
flowchart TD
    A["SettingsReader.cs<br/>Read the copied .env file"] --> B["AiClientFactory constructor<br/>Register lightweight provider strategies"]
    B --> C{"Select the configured strategy<br/>Are its key and model usable?"}
    C -->|Yes| K{"Learning application<br/>Chat or embeddings?"}
    K -->|Chat| D["Learning application<br/>Create a factory and call CreateClient"]
    K -->|Embeddings| BE["Selected provider strategy<br/>Call CreateEmbeddingGenerator for Microsoft IEmbeddingGenerator"]
    BE --> BR["Learning application<br/>Link a native deadline and call generator.GenerateAsync"]
    BR --> H
    D --> E["Selected provider strategy<br/>Create its IChatClient with internal HTTP policy"]
    E --> F["Learning application<br/>Create ChatClientAgent and ChatOptions; link one native deadline"]
    F --> G["Learning application<br/>Call agent.RunAsync"]
    G --> H["Provider client<br/>Send the HTTP request"]
    H -->|Temporary HTTP error| R{"Internal retry policy<br/>Retries and deadline remaining?"}
    R -->|Yes| W["Honor Retry-After, add jitter, and resend"]
    W --> H
    R -->|No| X["Learning application<br/>Print the native exception message and exit 1"]
    H -->|Embedding response| BO["Shared SDK policies<br/>Handle provider defaults; check and order raw indexes"]
    BO --> BA["Microsoft adapter<br/>Return GeneratedEmbeddings of Embedding values"]
    BA --> BV["Learning application<br/>Validate count, models, vectors, and compatibility"]
    BO -->|Failure| X
    BV --> P
    BV -->|Failure| X
    H -->|Chat response| T{"Native function loop<br/>Tool requested?"}
    T -->|Yes| U["Lesson + framework<br/>Run the registered C# function and retain its result"]
    U --> H
    U -->|Failure| X
    T -->|No| V["Learning application<br/>Check finish reason, final text, and lesson evidence"]
    V --> P["Print accepted results"]
    C -->|No| X
    V -->|Failure| X
```

SettingsReader reads the provider's metadata from the registered strategy. OpenRouterProvider supplies https://openrouter.ai/api/v1 and the OPENROUTER configuration prefix. GeminiProvider supplies https://generativelanguage.googleapis.com/v1beta/openai/ and the GEMINI prefix. Configuration checks run before any remote request. Unknown providers are rejected; unused providers require no valid settings.

Both strategies use Providers/OpenAiCompatibleClient.cs to construct native OpenAI .NET SDK chat or embedding clients with the selected model, credentials, endpoint, HTTP transport, and retry policy. GeminiProvider also handles an omitted embedding index zero: it restores zero only when exactly one index is absent and every positive index is present once. The common EmbeddingResponseOrderPolicy then rejects ambiguous omissions, duplicates, and invalid indexes and restores input order. Microsoft's standard AsIEmbeddingGenerator adapter converts the ordered SDK response into `GeneratedEmbeddings<Embedding<float>>` without exposing SDK types in the lesson. For chat schema or tool requests, the internal chat adapter clones native options and creates fresh underlying options for each model turn. OpenRouterProvider adds provider.require_parameters=true; GeminiProvider supplies no routing extension. The chat adapter preserves opaque assistant tool metadata, including Gemini thought signatures returned directly or through OpenRouter, for the next turn.

Chat lessons use a native linked CancellationTokenSource and pass its token to the complete RunAsync call. The embedding lesson links a native deadline around each generator.GenerateAsync call. Its CancelAfter uses AiClientFactory.RequestTimeoutSeconds. The same 120-second deadline covers model turns, local tools, attempts, and waits. The SDK network timeout uses the same value, without restarting the complete run's deadline. Final-answer and lesson-specific checks remain visible in the learning application.

The internal Providers/AiRetryPolicy.cs permits two retries for HTTP 408, 429, 500, 502, 503, and 504. It waits about 2 seconds before the first retry and 4 seconds before the second, plus 0-0.5 seconds of random variation called jitter. A longer Retry-After value takes priority. Cancellation stops requests and waits. Authentication, configuration, local validation, and network exceptions without an HTTP response are not retried. Retries neither restart the agent nor switch providers.

## Run

Install the .NET 10 SDK. Create .env in this folder from [.env.example](.env.example). It is the only source settings file, and Git ignores it.

Set AI_PROVIDER=openrouter with OPENROUTER_API_KEY and OPENROUTER_MODEL, or AI_PROVIDER=gemini with GEMINI_API_KEY and GEMINI_MODEL. For the embedding lesson, configure OPENROUTER_EMBEDDING_MODEL or GEMINI_EMBEDDING_MODEL in this same file. ReadSettingsAsync(folder, useEmbeddingModel: true) requires the selected embedding model and key, without requiring or changing its chat model. Chat lessons continue to use their existing model keys. Only the selected provider needs valid values. An absent AI_PROVIDER defaults to OpenRouter; blank and unsupported names are rejected.

From this folder, build the library:

```sh
dotnet build
```

shared.csproj copies .env into build output, and application/test project references carry that same file into their outputs. Rebuild after editing the original settings. This is a class library; use dotnet run from a learning application's folder or select that application as the Visual Studio startup project.

The shared project retains its pinned dependencies: OpenAI 2.14.0, Microsoft.Agents.AI 1.25.0, Microsoft.Extensions.AI.OpenAI 10.11.0, and System.ClientModel 1.16.0. Applications and tests use those dependencies through their shared project reference.

Building and running shared tests makes zero real model requests. A lesson sends fictional inputs to OpenRouter and its upstream provider, or directly to Google's Gemini API, and may charge your account. A model call normally sends one HTTP request and permits up to three attempts. Each lesson's README gives its exact call count.

Choose a model supporting the lesson's temperature and output limit. Structured output also needs its JSON Schema, and function calling needs function tools. Changing provider configuration keeps lesson code unchanged, but feature support and generated answers can differ. The strategies preserve the requested settings.

## Understand the results

SettingsReader returns ProviderSettings after checking the selected key and model for missing values and example placeholders. ProviderSettings.ToString shows only the provider name and model. Those local checks do not prove that credentials work or that a model is available; the actual request finds those problems.

CreateClient returns a disposable native IChatClient. CreateEmbeddingGenerator returns a disposable `Microsoft.Extensions.AI.IEmbeddingGenerator<string, Embedding<float>>`. Its GenerateAsync returns `GeneratedEmbeddings<Embedding<float>>` in input order, with returned model names and any reported usage. Raw index checks and ordering stay below Microsoft's adapter. The lesson checks count, model names, and vector values before a separate normalization stage. AgentResponse, UsageDetails, and FunctionResultContent remain framework types used by the lessons. Missing token counts stay unknown. ContextComparer maps them into its existing report format. ExperienceAssistant configures its native function loop and iteration limit. Program.cs reads only the final assistant message before the execution and answer checks.

Each chat request method rejects empty final assistant text and ChatFinishReason.Length. Embedding validation in the lesson checks model names, response count, vector sizes, numerical finiteness, and nonzero length. Shared SDK policies check the raw index mapping before Microsoft's adapter drops those indexes. The application prints native SDK exception messages for API failures and simple messages for local failures. Cancellation remains OperationCanceledException. There is no shared request, response, deadline, response-check, or error-presentation facade.

A retry prints a message such as HTTP 503. Retry 1/2 in 2.3 seconds. A successful first attempt prints no retry message. After two retries fail, the final SDK exception reaches the application. A long Retry-After wait can exhaust the complete run's deadline before another attempt.

## Learn by changing a check

1. Read ParseSettings in SettingsReader.cs. Predict what it does with a comment line, a quoted model name, and an equals sign inside a value.
2. Compare your prediction with [SettingsReaderTests.cs](../shared.tests/SettingsReaderTests.cs). These tests use made-up settings, so you can explore the parsing rules without a real API key.
3. Change one test input and run the shared tests. Observe why the settings or response are accepted or rejected.

## Files to read

```text
AiClientFactory.cs                               Manual strategy registration and selected-client creation
SettingsReader.cs                                Selected-provider configuration parsing and checks
ProviderSettings.cs                              Connection record with a credential-safe ToString
Providers/IAiProviderStrategy.cs                  Internal strategy contract for Microsoft chat and embedding interfaces
Providers/OpenRouterProvider.cs                  OpenRouter configuration, endpoint, and routing extension
Providers/GeminiProvider.cs                      Gemini configuration, endpoint, and omitted-zero index handling
Providers/OpenAiCompatibleClient.cs              Common SDK setup, native options, and tool metadata
Providers/EmbeddingResponseOrderPolicy.cs        Raw embedding index checks and input ordering
Providers/AiRetryPolicy.cs                       Internal bounded HTTP retries and provider waits
shared.csproj                                    Provider SDK dependencies and copied settings
../shared.tests/AiClientFactoryTests.cs           Native requests, strategies, metadata, and cancellation
../shared.tests/SettingsReaderTests.cs            Configuration selection and parsing
../shared.tests/ProviderSettingsTests.cs          Credential-safe descriptions
../shared.tests/Providers/EmbeddingResponseOrderPolicyTests.cs  Mapping checks through Microsoft's adapter
../shared.tests/Providers/AiRetryPolicyTests.cs   Retry behavior through the SDK pipeline
.env                                             Provider selection, keys, and models
.env.example                                     Settings template
```

## Tests

Shared tests exercise registered provider strategies through the real SDK and framework with a local HTTP transport. They require no credentials and make zero real API requests.

| Test file | Checks |
| --- | --- |
| [AiClientFactoryTests.cs](../shared.tests/AiClientFactoryTests.cs) | Provider selection, rejection before HTTP, switching without option leakage, chat and embedding endpoints, schemas/tools, thought signatures, usage, native errors, cancellation, and Gemini omitted-zero handling. |
| [SettingsReaderTests.cs](../shared.tests/SettingsReaderTests.cs) | Selected-provider metadata, unused credentials, invalid selectors, missing values, placeholders, and independent chat and embedding model settings. |
| [ProviderSettingsTests.cs](../shared.tests/ProviderSettingsTests.cs) | Descriptions omit credentials. |
| [Providers/EmbeddingResponseOrderPolicyTests.cs](../shared.tests/Providers/EmbeddingResponseOrderPolicyTests.cs) | Input ordering, metadata preservation, and rejection of duplicate, missing, negative, and out-of-range embedding indexes. |
| [Providers/AiRetryPolicyTests.cs](../shared.tests/Providers/AiRetryPolicyTests.cs) | Request preservation, retry limits, permanent errors, Retry-After, cancellation, and synchronous SDK calls. |

From the repository root, run:

```sh
dotnet test shared/shared.tests/shared.tests.csproj
```

You can also use Visual Studio's Test Explorer. Retry tests record requested waits without sleeping. The learning applications retain their own native response checks. Function-calling tests check the native iteration limit, final answer, and calculator evidence.

References: [Microsoft.Extensions.AI embeddings](https://learn.microsoft.com/en-us/dotnet/ai/iembeddinggenerator), [MSBuild project items and references](https://learn.microsoft.com/en-us/visualstudio/msbuild/common-msbuild-project-items), [official OpenAI .NET SDK](https://github.com/openai/openai-dotnet), [Agent Framework function tools](https://learn.microsoft.com/en-us/agent-framework/agents/tools/function-tools), [ClientRetryPolicy](https://learn.microsoft.com/en-us/dotnet/api/system.clientmodel.primitives.clientretrypolicy), [Gemini retry guidance](https://ai.google.dev/gemini-api/docs/troubleshooting#retry-strategy), and [Gemini OpenAI compatibility](https://ai.google.dev/gemini-api/docs/openai).
