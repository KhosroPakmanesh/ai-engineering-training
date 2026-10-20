// EndDate is exclusive: a job ending on April 1 covers time up to that day's start.
public sealed record MergedWorkInterval(DateOnly StartDate, DateOnly EndDate);
