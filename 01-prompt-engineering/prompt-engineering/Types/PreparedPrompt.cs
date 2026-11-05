// A complete prompt is ready to send without reopening input files.
public sealed record PreparedPrompt(
    string NameString,
    string PathString,
    string TextString);
