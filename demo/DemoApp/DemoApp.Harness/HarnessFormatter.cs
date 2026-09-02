namespace DemoApp.Harness;

public static class HarnessFormatter
{
    public static string Glyph(HarnessCheckStatus status) => status switch
    {
        HarnessCheckStatus.Passed => "✓",
        HarnessCheckStatus.Failed => "✗",
        HarnessCheckStatus.TimedOut => "⏱",
        HarnessCheckStatus.Skipped => "–",
        _ => "…",
    };

    public static string Summary(IReadOnlyList<HarnessCheckResult> results)
    {
        var passed = results.Count(x => x.Status == HarnessCheckStatus.Passed);
        var failed = results.Count(x => x.Status is HarnessCheckStatus.Failed or HarnessCheckStatus.TimedOut);
        var skipped = results.Count(x => x.Status == HarnessCheckStatus.Skipped);
        return $"{passed} passed, {failed} failed, {skipped} skipped of {results.Count}";
    }
}
