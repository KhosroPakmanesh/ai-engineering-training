using System.ClientModel.Primitives;

// Retry HTTP requests inside the SDK so an agent run is not restarted.
public class AiRetryPolicy() : ClientRetryPolicy(MaxRetries)
{
    public const int MaxRetries = 2;

    // Configuration errors and canceled calls cannot be repaired by another attempt.
    protected override bool ShouldRetry(PipelineMessage message, Exception? exception)
    {
        message.CancellationToken.ThrowIfCancellationRequested();
        return exception is null &&
            message.Response?.Status is 408 or 429 or 500 or 502 or 503 or 504 &&
            base.ShouldRetry(message, exception);
    }

    // The SDK reads Retry-After; wait at least that long before adding a little jitter.
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
