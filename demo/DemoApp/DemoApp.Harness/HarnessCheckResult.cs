namespace DemoApp.Harness;

/// <summary>
/// Outcome of a single wrapper-member check executed by <see cref="HarnessRunner"/>.
/// </summary>
public sealed record HarnessCheckResult
{
    public required string Name { get; init; }
    public required HarnessCheckStatus Status { get; init; }
    public string Summary { get; init; } = string.Empty;
    public string? Error { get; init; }
}

public enum HarnessCheckStatus
{
    Running,
    Passed,
    Failed,
    TimedOut,

    /// <summary>The wrapper member is not supported on the current platform (throws <see cref="NotImplementedException"/>).</summary>
    Skipped,
}
