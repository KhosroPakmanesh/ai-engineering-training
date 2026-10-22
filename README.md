# AI Engineering Training

Small C#/.NET 10 console applications for learning how AI systems work. Change an input, run a project, and inspect the result.

## Projects

- [Prompt engineering](01-prompt-engineering/prompt-engineering/README.md): compare three instructions for summarizing the same resume.
- [Structured output](02-structured-output/structured-output/README.md): extract a JSON profile from a resume and check its format and facts.
- [Context engineering](03-context-engineering/context-engineering/README.md): compare answers using all project descriptions or a selected subset.
- [Function calling](04-function-calling/function-calling/README.md): let the AI call a C# calculator to total work experience without counting overlapping dates twice.
- [Embeddings and semantic search](05-embeddings-semantic-search/embeddings-semantic-search/README.md): compare meaning and keyword search over twelve fictional projects.

## Prerequisites

- The [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
- An API key from [OpenRouter](https://openrouter.ai/settings/keys) or [Google AI Studio](https://aistudio.google.com/apikey) for Gemini.

## Choose a suitable model

The first four projects need a chat model. Structured output also needs JSON Schema support, which lets you define the answer's fields and value types. Function calling needs tool support so the model can request a C# function. Check each project's README for its temperature and output limits; the selected model must accept these settings.

**OpenRouter:** Browse the [model catalog](https://openrouter.ai/models) and choose a text model. Check its [supported parameters](https://openrouter.ai/docs/guides/overview/models#supported-parameters): `temperature` and `max_tokens`, plus `structured_outputs` for structured output or `tools` and `tool_choice` for function calling. For structured output, also check `structured_outputs` in the model page's Providers section, as [support varies by endpoint](https://openrouter.ai/docs/guides/features/structured-outputs#model-support). Copy the full model ID, including its provider prefix, into `OPENROUTER_MODEL`.

**Gemini:** Browse the [model catalog](https://ai.google.dev/gemini-api/docs/models), open a text model, and check its capabilities for structured output or function calling as needed. Copy the model code into `GEMINI_MODEL`. These projects use Gemini's [OpenAI-compatible API](https://ai.google.dev/gemini-api/docs/openai), so check compatibility with the lesson's settings there.

For semantic search, choose an embedding model, which converts text into numbers for comparison. Use OpenRouter's **Embeddings** category or the [Gemini embeddings guide](https://ai.google.dev/gemini-api/docs/embeddings). Set `OPENROUTER_EMBEDDING_MODEL` or `GEMINI_EMBEDDING_MODEL` separately from the chat model.

## Quick start

1. Create `shared/shared/.env` from [shared/shared/.env.example](shared/shared/.env.example).
2. Set `AI_PROVIDER` to `openrouter` or `gemini`, then fill in that provider's values:

   | Provider | API key | Chat model | Embedding model |
   | --- | --- | --- | --- |
   | OpenRouter | `OPENROUTER_API_KEY` | `OPENROUTER_MODEL` | `OPENROUTER_EMBEDDING_MODEL` |
   | Gemini | `GEMINI_API_KEY` | `GEMINI_MODEL` | `GEMINI_EMBEDDING_MODEL` |

   Only the selected provider needs valid values. The first four projects use the chat model; semantic search uses the embedding model.

3. From the repository root, run the first project:

   ```sh
   cd 01-prompt-engineering/prompt-engineering
   dotnet run
   ```

To try another project, run `dotnet run` from its application folder. In Visual Studio, open [AIEngineeringTraining.slnx](AIEngineeringTraining.slnx) and choose the application as the startup project.

Every project uses the same `.env` file, which Git ignores. Builds copy it into the executable's folder, so rebuild after changing it. See the [shared setup instructions](shared/shared/README.md#run) for more detail and [AiClientFactory.cs](shared/shared/AiClientFactory.cs) for provider connections.
