namespace DemoApp.Harness;

/// <summary>
/// Append-only, timestamped log the runner writes to and the UI renders. Newest entry last.
/// </summary>
public sealed class HarnessLog
{
    private readonly List<string> _lines = [];

    public IReadOnlyList<string> Lines => _lines;

    public event Action? Changed;

    public void Add(string line)
    {
        var stamped = $"{DateTime.Now:HH:mm:ss.fff} {line}";
        _lines.Add(stamped);
        // Console goes to logcat (tag mono-stdout) on Android and the Xcode console on iOS, so a device run
        // can be read back from the shell without touching the screen.
        Console.WriteLine($"[Harness] {stamped}");
        Changed?.Invoke();
    }

    public void Clear()
    {
        _lines.Clear();
        Changed?.Invoke();
    }
}
