public partial class AiRetryPolicyTests
{
    // Record each requested delay without waiting, and let tests cancel at that point.
    private sealed class RecordingRetryPolicy(Action? beforeWaitAction = null) : AiRetryPolicy
    {
        public List<TimeSpan> DelaysTimeSpans { get; } = [];

        protected override Task WaitAsync(
            TimeSpan timeSpan,
            CancellationToken cancellationToken)
        {
            Wait(
                timeSpan,
                cancellationToken);
            return Task.CompletedTask;
        }

        protected override void Wait(
            TimeSpan timeSpan,
            CancellationToken cancellationToken)
        {
            DelaysTimeSpans.Add(timeSpan);
            beforeWaitAction?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}

