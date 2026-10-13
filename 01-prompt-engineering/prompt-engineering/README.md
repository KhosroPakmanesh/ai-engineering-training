# Prompt Engineering: one small experiment

Read **Program.cs** for the comparison loop and **ResumeSummarizer.cs** for the shared API request. The application loads one fictional resume, reads three instructions, sends each to the same model, and prints the summaries and their word counts.

| Prompt file | Concept |
| --- | --- |
| [A-zero-shot.txt](prompts/A-zero-shot.txt) | Ask directly, without examples. |
| [B-few-shot.txt](prompts/B-few-shot.txt) | Give two examples to demonstrate a style. |
| [C-constrained.txt](prompts/C-constrained.txt) | Specify a word limit and require source-supported facts. |

The shared system instruction is visible in ResumeSummarizer.cs. It tells the model to treat the resume as data and the examples as style guidance. Each call starts a fresh conversation, with the same resume and generation settings.

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

You can also run the project in Visual Studio. Builds copy the resume, prompts, and `.env` beside the executable; the program reads its files from that folder. Edit the files in the project folder and rebuild to use your changes. The first build downloads the .NET package if needed.

ResumeSummarizer.cs reads these settings directly from `.env`, which Git ignores, and prints the selected model. Each run sends the fictional resume to OpenRouter and may charge your account: three requests, up to 2,048 output tokens each, with automatic retries disabled. The same temperature (0.2) is used each time. Optional `top_p` is omitted because some providers reject it.

Set `OPENROUTER_MODEL` to an available chat model ID. HTTP 429 means OpenRouter or the provider is rate limiting requests or is at capacity; this can happen while your daily free quota still has requests remaining. Wait and run again, or change the model in `.env`. See [OpenRouter's rate limit guidance](https://openrouter.ai/docs/api_reference/limits#handling-429-errors).

Use a model that supports chat completions. On failure, the program prints the full response with provider error details. After editing `.env`, stop any running debugging session and rebuild; the configuration is copied on every build.

The token budget includes thinking tokens on reasoning models, so a short summary may require more than 200 output tokens. The program reports an empty response with its finish reason rather than indexing into missing text. See [OpenRouter's explanation](https://openrouter.ai/docs/guides/best-practices/reasoning-tokens#reasoning-tokens-and-max_tokens).

## Learn by changing a prompt

1. Predict the difference between A, B, and C.
2. Generate the summaries and compare them with [the resume](resume.md).
3. Check whether C stayed within 70 words and whether B borrowed any facts from its examples.
4. Change one prompt and repeat; each run makes another three requests.

The word count is a simple whitespace count. Review factual accuracy yourself. Identical settings do not guarantee identical output.

## Tests

The solution includes `prompt-engineering.tests`, a small [xUnit project](https://xunit.net/docs/getting-started/v2/getting-started). Its one source file, [PromptTests.cs](../prompt-engineering.tests/PromptTests.cs), contains three cases. They call the same `ResumeSummarizer.SummarizeAsync` method as the console app, using the same `.env`, resume, and prompt files copied through the project reference.

From the repository root, run:

```sh
dotnet test AIEngineeringTraining.slnx
```

You can also use Visual Studio's Test Explorer. A complete run makes three real API requests. Each case checks for a nonempty summary with no blank lines separating paragraphs; C also checks the 70-word limit. Summaries and word counts appear in the test output. Missing configuration and API failures fail the tests. A rate limit or unavailable provider does not establish a code defect, and a model can fail a formatting constraint on a later run. Factual accuracy and whether examples influence style still need manual review.

## Files to read

```text
Program.cs                 Console comparison loop
ResumeSummarizer.cs         Shared summary request method
.env                       Your OpenRouter key and model
.env.example               Template for creating .env
prompts/A-zero-shot.txt     Direct instruction
prompts/B-few-shot.txt      Instruction with examples
prompts/C-constrained.txt   Instruction with constraints
resume.md                  The fictional source
prompt-engineering.csproj   .NET 10 project and its single SDK dependency
```

The application displays results in the console. It is a small teaching example with direct SDK calls and a single comparison loop.

References: [official OpenAI .NET SDK](https://github.com/openai/openai-dotnet) and [OpenRouter Chat Completions](https://openrouter.ai/docs/api/api-reference/chat/create-a-chat-completion).
