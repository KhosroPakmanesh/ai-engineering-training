// Let lessons handle API errors without depending on the AI library's exception type.
public sealed class AiRequestException(int status, string message, string? responseDetails, Exception innerException)
    : Exception(message, innerException)
{
    public int Status { get; } = status;
    public string? ResponseDetails { get; } = responseDetails;
}
