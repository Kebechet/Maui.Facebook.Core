using System.Globalization;
using FacebookCoreiOS;
using Foundation;
using Microsoft.Extensions.Logging;

namespace Maui.Facebook.Core.Services;

//https://developers.facebook.com/docs/app-events/getting-started-app-events-ios
public partial class FacebookCoreService
{
    private bool _isInitialized;

    public partial void Initialize(string appId, string clientToken)
    {
        if (_isInitialized)
        {
            return;
        }

        try
        {
            // Settings must be in place before initializeSDK() reads them; this is the programmatic
            // equivalent of the FacebookAppID / FacebookClientToken Info.plist keys.
            var settings = FBSDKSettings.SharedSettings;
            settings.AppID = appId;
            settings.ClientToken = clientToken;

            FBSDKApplicationDelegate.SharedInstance.InitializeSDK();
            FBSDKAppEvents.Shared.ActivateApp();

            _isInitialized = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{methodName} error in Facebook SDK", nameof(Initialize));
        }
    }

    public partial void SetAutoLogAppEventsEnabled(bool isEnabled)
    {
        try
        {
            FBSDKSettings.SharedSettings.IsAutoLogAppEventsEnabled = isEnabled;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{methodName} error in Facebook SDK", nameof(SetAutoLogAppEventsEnabled));
        }
    }

    public partial void SetAdvertiserIdCollectionEnabled(bool isEnabled)
    {
        try
        {
            FBSDKSettings.SharedSettings.IsAdvertiserIDCollectionEnabled = isEnabled;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{methodName} error in Facebook SDK", nameof(SetAdvertiserIdCollectionEnabled));
        }
    }

    public partial void SetAdvertiserTrackingEnabled(bool isEnabled)
    {
        try
        {
            FBSDKSettings.SharedSettings.IsAdvertiserTrackingEnabled = isEnabled;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{methodName} error in Facebook SDK", nameof(SetAdvertiserTrackingEnabled));
        }
    }

    public partial void LogEvent(string eventName, IReadOnlyDictionary<string, object>? parameters, double? valueToSum)
    {
        try
        {
            var nativeParameters = ToNSDictionary(parameters);
            var appEvents = FBSDKAppEvents.Shared;

            if (valueToSum.HasValue)
            {
                appEvents.LogEvent(eventName, valueToSum.Value, nativeParameters);
            }
            else if (nativeParameters is not null)
            {
                appEvents.LogEvent(eventName, nativeParameters);
            }
            else
            {
                appEvents.LogEvent(eventName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{methodName} error in Facebook SDK", nameof(LogEvent));
        }
    }

    public partial void LogPurchase(decimal amount, string currencyCode, IReadOnlyDictionary<string, object>? parameters)
    {
        try
        {
            var nativeParameters = ToNSDictionary(parameters);
            if (nativeParameters is not null)
            {
                FBSDKAppEvents.Shared.LogPurchase((double)amount, currencyCode, nativeParameters);
            }
            else
            {
                FBSDKAppEvents.Shared.LogPurchase((double)amount, currencyCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{methodName} error in Facebook SDK", nameof(LogPurchase));
        }
    }

    public partial void Flush()
    {
        try
        {
            FBSDKAppEvents.Shared.Flush();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{methodName} error in Facebook SDK", nameof(Flush));
        }
    }

    private partial bool IsInitializedMethod()
    {
        return _isInitialized;
    }

    private partial string? SdkVersionMethod()
    {
        try
        {
            return FBSDKSettings.SharedSettings.SdkVersion;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{methodName} error in Facebook SDK", nameof(SdkVersion));
            return null;
        }
    }

    private partial string? AnonymousIdMethod()
    {
        if (!_isInitialized)
        {
            return null;
        }

        try
        {
            return FBSDKAppEvents.Shared.AnonymousID;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{methodName} error in Facebook SDK", nameof(AnonymousId));
            return null;
        }
    }

    private partial string? GetUserIdMethod()
    {
        if (!_isInitialized)
        {
            return null;
        }

        try
        {
            return FBSDKAppEvents.Shared.UserID;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{methodName} error in Facebook SDK", nameof(UserId));
            return null;
        }
    }

    private partial void SetUserIdMethod(string? userId)
    {
        if (!_isInitialized)
        {
            _logger.LogWarning("{methodName} called before {initialize}; the call was dropped", nameof(UserId), nameof(Initialize));
            return;
        }

        try
        {
            FBSDKAppEvents.Shared.UserID = userId!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{methodName} error in Facebook SDK", nameof(UserId));
        }
    }

    private static NSDictionary<NSString, NSObject>? ToNSDictionary(IReadOnlyDictionary<string, object>? parameters)
    {
        if (parameters is null || parameters.Count == 0)
        {
            return null;
        }

        var keys = new NSString[parameters.Count];
        var values = new NSObject[parameters.Count];
        var i = 0;
        foreach (var (key, value) in parameters)
        {
            keys[i] = new NSString(key);
            values[i] = value switch
            {
                string s => new NSString(s),
                bool b => NSNumber.FromBoolean(b),
                int n => NSNumber.FromInt32(n),
                long n => NSNumber.FromInt64(n),
                float n => NSNumber.FromFloat(n),
                double n => NSNumber.FromDouble(n),
                decimal n => NSNumber.FromDouble((double)n),
                _ => new NSString(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty),
            };
            i++;
        }

        return NSDictionary<NSString, NSObject>.FromObjectsAndKeys(values, keys, parameters.Count);
    }
}
