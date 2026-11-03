using System.ClientModel.Primitives;

// Retry only the failed HTTP request, without starting the whole agent call again.
internal class AiRetryPolicy() : ClientRetryPolicy(MaxRetries)
{
    public const int MaxRetries = 2;

    // Try again only for temporary HTTP failures; stop when the caller cancels.
    protected override bool ShouldRetry(PipelineMessage pipelineMessage, Exception? exception)
    {
        pipelineMessage.CancellationToken.ThrowIfCancellationRequested();
        return exception is null &&
            pipelineMessage.Response?.Status is 408 or 429 or 500 or 502 or 503 or 504 &&
            base.ShouldRetry(pipelineMessage, exception);
    }

    // Honor the provider's requested wait, then add a small random delay to spread out retries.
    protected override TimeSpan GetNextDelay(PipelineMessage pipelineMessage, int tryCountInt)
    {
        TimeSpan delayTimeSpan = TimeSpan.FromSeconds(Math.Pow(2, tryCountInt));
        TimeSpan providerDelayTimeSpan = base.GetNextDelay(pipelineMessage, tryCountInt);
        if (providerDelayTimeSpan > delayTimeSpan)
        {
            delayTimeSpan = providerDelayTimeSpan;
        }

        delayTimeSpan += TimeSpan.FromMilliseconds(Random.Shared.Next(501));

        Console.Error.WriteLine($"HTTP {pipelineMessage.Response?.Status}. Retry {tryCountInt}/{MaxRetries} in {delayTimeSpan.TotalSeconds:F1} seconds.");
        return delayTimeSpan;
    }
}
