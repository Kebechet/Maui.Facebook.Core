namespace Maui.Facebook.Core.Services;

// Compiled for every target that has no native Facebook SDK: plain net10.0, MacCatalyst and Windows (see the
// PlatformsStandard item group in the csproj). Every member is a documented no-op returning the defaults
// listed on IFacebookCoreService: true for bool, null for nullable strings.
public partial class FacebookCoreService
{
    public partial void Initialize(string appId, string clientToken)
    {
    }

    public partial void SetAutoLogAppEventsEnabled(bool isEnabled)
    {
    }

    public partial void SetAdvertiserIdCollectionEnabled(bool isEnabled)
    {
    }

    public partial void SetAdvertiserTrackingEnabled(bool isEnabled)
    {
    }

    public partial void LogEvent(string eventName, IReadOnlyDictionary<string, object>? parameters, double? valueToSum)
    {
    }

    public partial void LogPurchase(decimal amount, string currencyCode, IReadOnlyDictionary<string, object>? parameters)
    {
    }

    public partial void Flush()
    {
    }

    private partial bool IsInitializedMethod()
    {
        return true;
    }

    private partial string? SdkVersionMethod()
    {
        return null;
    }

    private partial string? AnonymousIdMethod()
    {
        return null;
    }

    private partial string? GetUserIdMethod()
    {
        return null;
    }

    private partial void SetUserIdMethod(string? userId)
    {
    }
}
