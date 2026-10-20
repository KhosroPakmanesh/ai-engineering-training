using System.Text.Json;

// Lessons supply ordinary C# methods; only shared code turns them into SDK function tools.
public sealed record AiTool(string Name, string Description, Delegate Function);

// Model calls count conversation turns, while tool calls count local method executions.
public sealed record AiToolCalling(List<AiTool> Tools, int MaxModelCalls, int MaxToolCalls);

// Keep the actual returned value so a lesson can check an answer against executed code.
public sealed record AiToolExecution(string ToolName, JsonElement Result, long DurationMilliseconds);
