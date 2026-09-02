using System.Globalization;
using Android.Content;
using Android.OS;
using Com.Facebook;
using Com.Facebook.Appevents;
using Microsoft.Extensions.Logging;

namespace Maui.Facebook.Core.Services;

//https://developers.facebook.com/docs/app-events/getting-started-app-events-android
public partial class FacebookCoreService
{
    private AppEventsLogger? _appEventsLogger;
    private bool _isInitialized;

    private static Context AppContext => global::Android.App.Application.Context;

    public partial void Initialize(string appId, string clientToken)
    {
        if (_isInitialized)
        {
            return;
        }

        try
        {
            FacebookSdk.ApplicationId = appId;
            FacebookSdk.ClientToken = clientToken;

            // Deprecated upstream in favour of manifest-driven auto-init, but auto-init only runs when the
            // manifest carries ApplicationId/ClientToken meta-data. Programmatic setup has no other entry point.
#pragma warning disable CS0618
            FacebookSdk.SdkInitialize(AppContext);
#pragma warning restore CS0618
            FacebookSdk.FullyInitialize();

            if (AppContext is global::Android.App.Application application)
            {
                AppEventsLogger.ActivateApp(application);
            }

            _appEventsLogger = AppEventsLogger.NewLogger(AppContext);
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
            FacebookSdk.AutoLogAppEventsEnabled = isEnabled;
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
            FacebookSdk.AdvertiserIDCollectionEnabled = isEnabled;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{methodName} error in Facebook SDK", nameof(SetAdvertiserIdCollectionEnabled));
        }
    }

    public partial void SetAdvertiserTrackingEnabled(bool isEnabled)
    {
        // iOS-only concept (App Tracking Transparency). Android expresses the same consent through
        // AdvertiserIDCollectionEnabled, so there is nothing to forward here.
    }

    public partial void LogEvent(string eventName, IReadOnlyDictionary<string, object>? parameters, double? valueToSum)
    {
        if (!EnsureLogger(nameof(LogEvent)))
        {
            return;
        }

        try
        {
            var bundle = ToBundle(parameters);

            if (valueToSum.HasValue)
            {
                _appEventsLogger!.LogEvent(eventName, valueToSum.Value, bundle);
            }
            else if (bundle is not null)
            {
                _appEventsLogger!.LogEvent(eventName, bundle);
            }
            else
            {
                _appEventsLogger!.LogEvent(eventName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{methodName} error in Facebook SDK", nameof(LogEvent));
        }
    }

    public partial void LogPurchase(decimal amount, string currencyCode, IReadOnlyDictionary<string, object>? parameters)
    {
        if (!EnsureLogger(nameof(LogPurchase)))
        {
            return;
        }

        try
        {
            var javaAmount = new Java.Math.BigDecimal(amount.ToString(CultureInfo.InvariantCulture));
            var javaCurrency = Java.Util.Currency.GetInstance(currencyCode);
            _appEventsLogger!.LogPurchase(javaAmount, javaCurrency, ToBundle(parameters));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{methodName} error in Facebook SDK", nameof(LogPurchase));
        }
    }

    public partial void Flush()
    {
        if (!EnsureLogger(nameof(Flush)))
        {
            return;
        }

        try
        {
            _appEventsLogger!.Flush();
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
            return FacebookSdk.SdkVersion;
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
            return AppEventsLogger.GetAnonymousAppDeviceGUID(AppContext);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{methodName} error in Facebook SDK", nameof(AnonymousId));
            return null;
        }
    }

    private partial string? GetUserIdMethod()
    {
        // AppEventsLogger.userID throws FacebookSdkNotInitializedException before sdkInitialize(); null is the
        // documented pre-Initialize answer, and reading state must never log.
        if (!_isInitialized)
        {
            return null;
        }

        try
        {
            return AppEventsLogger.UserID;
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
            if (userId is null)
            {
                AppEventsLogger.ClearUserID();
            }
            else
            {
                AppEventsLogger.UserID = userId;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{methodName} error in Facebook SDK", nameof(UserId));
        }
    }

    private bool EnsureLogger(string methodName)
    {
        if (_appEventsLogger is not null)
        {
            return true;
        }

        _logger.LogWarning("{methodName} called before {initialize}; the call was dropped", methodName, nameof(Initialize));
        return false;
    }

    private static Bundle? ToBundle(IReadOnlyDictionary<string, object>? parameters)
    {
        if (parameters is null || parameters.Count == 0)
        {
            return null;
        }

        var bundle = new Bundle();
        foreach (var (key, value) in parameters)
        {
            switch (value)
            {
                case string s: bundle.PutString(key, s); break;
                case bool b: bundle.PutBoolean(key, b); break;
                case int i: bundle.PutInt(key, i); break;
                case long l: bundle.PutLong(key, l); break;
                case float f: bundle.PutFloat(key, f); break;
                case double d: bundle.PutDouble(key, d); break;
                case decimal m: bundle.PutDouble(key, (double)m); break;
                default: bundle.PutString(key, Convert.ToString(value, CultureInfo.InvariantCulture)); break;
            }
        }

        return bundle;
    }
}
