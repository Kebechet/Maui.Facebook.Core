using System.Diagnostics;
using Maui.Facebook.Core.Services;

namespace DemoApp.Harness;

/// <summary>
/// Sequentially exercises every <see cref="IFacebookCoreService"/> member on the current device and
/// reports one <see cref="HarnessCheckResult"/> per member. Runs <see cref="IFacebookCoreService.Initialize"/>
/// first with the supplied credentials, since every other member depends on it.
/// </summary>
public sealed class HarnessRunner
{
    private static readonly TimeSpan _defaultPerCallTimeout = TimeSpan.FromSeconds(15);

    private readonly IFacebookCoreService _facebookCore;
    private readonly HarnessLog _harnessLog;
    private readonly HarnessLoggerProvider _loggerProvider;
    private readonly TimeSpan _perCallTimeout;

    /// <summary>The anonymous id observed by the run, so the UI can show it prominently.</summary>
    public string? ObservedAnonymousId { get; private set; }

    public HarnessRunner(IFacebookCoreService facebookCore, HarnessLog harnessLog, HarnessLoggerProvider loggerProvider, TimeSpan? perCallTimeout = null)
    {
        _facebookCore = facebookCore;
        _harnessLog = harnessLog;
        _loggerProvider = loggerProvider;
        _perCallTimeout = perCallTimeout ?? _defaultPerCallTimeout;
    }

    public async Task<List<HarnessCheckResult>> RunAllChecksAsync(string appId, string clientToken, IProgress<HarnessCheckResult>? progress = null)
    {
        var results = new List<HarnessCheckResult>();

        foreach (var (checkName, checkAction, onCallerThread) in OrderedChecks(appId, clientToken))
        {
            progress?.Report(new HarnessCheckResult { Name = checkName, Status = HarnessCheckStatus.Running });
            var result = await ExecuteCheck(checkName, checkAction, onCallerThread);
            results.Add(result);
            progress?.Report(result);
        }

        return results;
    }

    private async Task<HarnessCheckResult> ExecuteCheck(string checkName, Func<CancellationToken, Task<string>> checkAction, bool onCallerThread)
    {
        var stopwatch = Stopwatch.StartNew();
        var problemsBefore = _loggerProvider.ProblemCount;
        // Every wrapper member is synchronous, so a native call that never returns would block right here,
        // before there is any Task for WaitAsync to time out. Running the check on the pool turns that block
        // into a timeout the sweep can survive; the stuck thread is abandoned, which a harness can afford.
        // The native App Events loggers are thread-safe, so leaving the UI thread is not itself a problem -
        // except for Initialize, which the wrapper documents as a main-thread call, so it stays where RunAll
        // was invoked (the UI thread) and simply cannot be timed out.
        using var timeoutSource = new CancellationTokenSource(_perCallTimeout);
        try
        {
            var pending = onCallerThread ? checkAction(timeoutSource.Token) : Task.Run(() => checkAction(timeoutSource.Token));
            var summary = await pending.WaitAsync(_perCallTimeout);

            // The wrapper reports native failures through its logger rather than by throwing, so a check
            // only counts as passed when it also produced no warning or error while it ran.
            if (_loggerProvider.ProblemCount > problemsBefore)
            {
                var problem = _loggerProvider.LastProblem ?? "the wrapper logged a warning or error";
                _harnessLog.Add($"FAIL {checkName} ({stopwatch.ElapsedMilliseconds} ms): {problem}");
                return new HarnessCheckResult { Name = checkName, Status = HarnessCheckStatus.Failed, Summary = summary, Error = problem };
            }

            _harnessLog.Add($"PASS {checkName} ({stopwatch.ElapsedMilliseconds} ms): {summary}");
            return new HarnessCheckResult { Name = checkName, Status = HarnessCheckStatus.Passed, Summary = summary };
        }
        catch (NotImplementedException exception)
        {
            _harnessLog.Add($"SKIP {checkName}: {exception.Message}");
            return new HarnessCheckResult { Name = checkName, Status = HarnessCheckStatus.Skipped, Summary = $"not supported on this platform ({exception.Message})" };
        }
        catch (Exception exception) when (exception is TimeoutException or OperationCanceledException)
        {
            _harnessLog.Add($"TIMEOUT {checkName} after {_perCallTimeout.TotalSeconds:0} s");
            return new HarnessCheckResult { Name = checkName, Status = HarnessCheckStatus.TimedOut, Error = $"No response within {_perCallTimeout.TotalSeconds:0} s" };
        }
        catch (Exception exception)
        {
            _harnessLog.Add($"FAIL {checkName} ({stopwatch.ElapsedMilliseconds} ms): {exception.Message}");
            return new HarnessCheckResult { Name = checkName, Status = HarnessCheckStatus.Failed, Error = exception.Message };
        }
    }

    private static string Require(bool condition, string ok, string failure)
    {
        return condition ? ok : throw new InvalidOperationException(failure);
    }

    private static readonly TimeSpan _userIdSettleTimeout = TimeSpan.FromSeconds(3);

    private async Task<(string? Value, long ElapsedMs)> PollUserId(Func<string?, bool> isSettled, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var value = _facebookCore.UserId;
        while (!isSettled(value) && stopwatch.Elapsed < _userIdSettleTimeout)
        {
            await Task.Delay(50, cancellationToken);
            value = _facebookCore.UserId;
        }
        return (value, stopwatch.ElapsedMilliseconds);
    }

    private IEnumerable<(string Name, Func<CancellationToken, Task<string>> Action, bool OnCallerThread)> OrderedChecks(string appId, string clientToken)
    {
        yield return (nameof(IFacebookCoreService.Initialize), _ =>
        {
            _facebookCore.Initialize(appId, clientToken);
            return Task.FromResult(Require(_facebookCore.IsInitialized, "initialized", $"{nameof(IFacebookCoreService.IsInitialized)} is still false after Initialize"));
        }, true);

        yield return (nameof(IFacebookCoreService.SdkVersion), _ =>
        {
            var version = _facebookCore.SdkVersion;
            return Task.FromResult(Require(!string.IsNullOrWhiteSpace(version), version!, "SdkVersion is null or empty"));
        }, false);

        yield return (nameof(IFacebookCoreService.AnonymousId), _ =>
        {
            var anonymousId = _facebookCore.AnonymousId;
            ObservedAnonymousId = anonymousId;
            return Task.FromResult(Require(
                !string.IsNullOrWhiteSpace(anonymousId) && anonymousId!.Length >= 32,
                anonymousId!,
                $"AnonymousId is '{anonymousId ?? "null"}', expected a persisted 'XZ' + UUID"));
        }, false);

        yield return ($"{nameof(IFacebookCoreService.AnonymousId)} is stable", _ =>
        {
            var again = _facebookCore.AnonymousId;
            return Task.FromResult(Require(again == ObservedAnonymousId, "same value on a second read", $"second read returned '{again}', first was '{ObservedAnonymousId}'"));
        }, false);

        yield return ($"{nameof(IFacebookCoreService.UserId)} set / read / clear", async cancellationToken =>
        {
            // The Android SDK persists userID through its analytics executor, so a read immediately after a
            // write can still return the previous value. Poll briefly rather than assert on the first read.
            const string userId = "harness-user";
            _facebookCore.UserId = userId;
            var (readBack, setAfter) = await PollUserId(x => x == userId, cancellationToken);
            _facebookCore.UserId = null;
            var (cleared, clearedAfter) = await PollUserId(string.IsNullOrEmpty, cancellationToken);
            return Require(
                readBack == userId && string.IsNullOrEmpty(cleared),
                $"set -> '{readBack}' after {setAfter} ms, cleared -> '{cleared ?? "null"}' after {clearedAfter} ms",
                $"set read back '{readBack}' after {setAfter} ms, cleared read back '{cleared}' after {clearedAfter} ms");
        }, false);

        yield return (nameof(IFacebookCoreService.SetAutoLogAppEventsEnabled), _ =>
        {
            _facebookCore.SetAutoLogAppEventsEnabled(true);
            return Task.FromResult("true");
        }, false);

        yield return (nameof(IFacebookCoreService.SetAdvertiserIdCollectionEnabled), _ =>
        {
            _facebookCore.SetAdvertiserIdCollectionEnabled(true);
            return Task.FromResult("true");
        }, false);

        yield return (nameof(IFacebookCoreService.SetAdvertiserTrackingEnabled), _ =>
        {
            _facebookCore.SetAdvertiserTrackingEnabled(false);
            return Task.FromResult("false (no-op on Android)");
        }, false);

        yield return (nameof(IFacebookCoreService.LogEvent), _ =>
        {
            _facebookCore.LogEvent("harness_event");
            return Task.FromResult("harness_event");
        }, false);

        yield return ($"{nameof(IFacebookCoreService.LogEvent)} with parameters + valueToSum", _ =>
        {
            _facebookCore.LogEvent("harness_event_params", new Dictionary<string, object>
            {
                ["source"] = "harness",
                ["run_at"] = DateTime.UtcNow.ToString("O"),
                ["attempt"] = 1,
                ["ratio"] = 0.5,
                ["flag"] = true,
            }, valueToSum: 2.5);
            return Task.FromResult("5 parameters, valueToSum 2.5");
        }, false);

        yield return (nameof(IFacebookCoreService.LogPurchase), _ =>
        {
            _facebookCore.LogPurchase(1.99m, "EUR", new Dictionary<string, object> { ["fb_content_id"] = "harness_sku" });
            return Task.FromResult("1.99 EUR");
        }, false);

        yield return (nameof(IFacebookCoreService.Flush), _ =>
        {
            _facebookCore.Flush();
            return Task.FromResult("flushed");
        }, false);
    }
}
