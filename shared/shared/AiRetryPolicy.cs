using System.ClientModel.Primitives;

// Retry only the failed HTTP request, without starting the whole agent call again.
public class AiRetryPolicy() : ClientRetryPolicy(MaxRetries)
{
    public const int MaxRetries = 2;

    // Try again only for temporary HTTP failures; stop when the caller cancels.
    protected override bool ShouldRetry(PipelineMessage message, Exception? exception)
    {
        message.CancellationToken.ThrowIfCancellationRequested();
        return exception is null &&
            message.Response?.Status is 408 or 429 or 500 or 502 or 503 or 504 &&
            base.ShouldRetry(message, exception);
    }

    // Honor the provider's requested wait, then add a small random delay to spread out retries.
    protected override TimeSpan GetNextDelay(PipelineMessage message, int tryCount)
    {
        TimeSpan delay = TimeSpan.FromSeconds(Math.Pow(2, tryCount));
        TimeSpan providerDelay = base.GetNextDelay(message, tryCount);
        if (providerDelay > delay) delay = providerDelay;
        delay += TimeSpan.FromMilliseconds(Random.Shared.Next(501));

        Console.Error.WriteLine($"HTTP {message.Response?.Status}. Retry {tryCount}/{MaxRetries} in {delay.TotalSeconds:F1} seconds.");
        return delay;
    }
}
