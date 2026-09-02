using Microsoft.Extensions.Logging;

namespace DemoApp.Harness;

/// <summary>
/// Routes the wrapper's <see cref="ILogger"/> output into the <see cref="HarnessLog"/> and counts every
/// entry at <see cref="LogLevel.Warning"/> or above.
/// </summary>
/// <remarks>
/// The wrapper deliberately swallows native SDK exceptions and reports them through its logger, so a
/// check that merely "did not throw" would pass against a completely broken SDK. The runner snapshots
/// <see cref="ProblemCount"/> before and after each check instead: any warning or error logged while the
/// check ran fails it, with the logged message as the reason.
/// </remarks>
public sealed class HarnessLoggerProvider : ILoggerProvider
{
    private readonly HarnessLog _harnessLog;
    private int _problemCount;
    private string? _lastProblem;

    public HarnessLoggerProvider(HarnessLog harnessLog)
    {
        _harnessLog = harnessLog;
    }

    public int ProblemCount => Volatile.Read(ref _problemCount);

    public string? LastProblem => _lastProblem;

    public ILogger CreateLogger(string categoryName)
    {
        return new HarnessLogger(this, categoryName);
    }

    public void Dispose()
    {
    }

    private void Record(LogLevel logLevel, string categoryName, string message, Exception? exception)
    {
        var shortCategory = categoryName.Contains('.') ? categoryName[(categoryName.LastIndexOf('.') + 1)..] : categoryName;
        var text = exception is null ? message : $"{message} -> {exception.GetType().Name}: {exception.Message}";
        _harnessLog.Add($"[{logLevel}] {shortCategory}: {text}");

        if (logLevel >= LogLevel.Warning)
        {
            _lastProblem = text;
            Interlocked.Increment(ref _problemCount);
        }
    }

    private sealed class HarnessLogger : ILogger
    {
        private readonly HarnessLoggerProvider _provider;
        private readonly string _categoryName;

        public HarnessLogger(HarnessLoggerProvider provider, string categoryName)
        {
            _provider = provider;
            _categoryName = categoryName;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            _provider.Record(logLevel, _categoryName, formatter(state, exception), exception);
        }
    }
}
