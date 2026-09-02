using Maui.Facebook.Core.Services;
using Microsoft.Extensions.Logging;

namespace DemoHarness.Tests;

/// <summary>
/// A configurable stand-in for the wrapper. Behaves like a healthy native SDK by default; each knob
/// breaks exactly one thing so a test can prove the runner notices it.
/// </summary>
internal sealed class FakeFacebookCoreService : IFacebookCoreService
{
    private readonly ILogger? _logger;
    private string? _userId;

    public FakeFacebookCoreService(ILogger? logger = null)
    {
        _logger = logger;
    }

    public string? AnonymousIdToReturn { get; set; } = "XZ" + Guid.NewGuid();

    /// <summary>When set, the second read of <see cref="AnonymousId"/> returns a different value.</summary>
    public bool AnonymousIdChangesBetweenReads { get; set; }

    /// <summary>When set, clearing the user id is silently ignored.</summary>
    public bool ClearUserIdIsBroken { get; set; }

    /// <summary>Member names whose call should log an error through the wrapper's logger.</summary>
    public HashSet<string> MembersThatLogErrors { get; } = [];

    /// <summary>Member names whose call should throw <see cref="NotImplementedException"/>.</summary>
    public HashSet<string> MembersNotImplemented { get; } = [];

    /// <summary>Member names whose call should block until the caller gives up.</summary>
    public HashSet<string> MembersThatHang { get; } = [];

    public List<string> Calls { get; } = [];

    private int _anonymousIdReads;

    public void Initialize(string appId, string clientToken) { Touch(nameof(Initialize)); IsInitialized = true; }

    public bool IsInitialized { get; private set; }

    public string? SdkVersion { get { Touch(nameof(SdkVersion)); return "18.0.0-fake"; } }

    public string? AnonymousId
    {
        get
        {
            Touch(nameof(AnonymousId));
            _anonymousIdReads++;
            return AnonymousIdChangesBetweenReads && _anonymousIdReads > 1 ? AnonymousIdToReturn + "-changed" : AnonymousIdToReturn;
        }
    }

    public string? UserId
    {
        get { Touch(nameof(UserId)); return _userId; }
        set
        {
            Touch(nameof(UserId));
            if (value is null && ClearUserIdIsBroken) return;
            _userId = value;
        }
    }

    public void SetAutoLogAppEventsEnabled(bool isEnabled) => Touch(nameof(SetAutoLogAppEventsEnabled));
    public void SetAdvertiserIdCollectionEnabled(bool isEnabled) => Touch(nameof(SetAdvertiserIdCollectionEnabled));
    public void SetAdvertiserTrackingEnabled(bool isEnabled) => Touch(nameof(SetAdvertiserTrackingEnabled));
    public void LogEvent(string eventName, IReadOnlyDictionary<string, object>? parameters = null, double? valueToSum = null) => Touch(nameof(LogEvent));
    public void LogPurchase(decimal amount, string currencyCode, IReadOnlyDictionary<string, object>? parameters = null) => Touch(nameof(LogPurchase));
    public void Flush() => Touch(nameof(Flush));

    private void Touch(string member)
    {
        Calls.Add(member);

        if (MembersNotImplemented.Contains(member))
        {
            throw new NotImplementedException($"{member} is not available on this platform");
        }

        if (MembersThatHang.Contains(member))
        {
            Thread.Sleep(Timeout.Infinite);
        }

        if (MembersThatLogErrors.Contains(member))
        {
            _logger?.LogError(new InvalidOperationException("native SDK exploded"), "{methodName} error in Facebook SDK", member);
        }
    }
}
