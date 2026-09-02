namespace Maui.Facebook.Core.Services;

/// <summary>
/// Cross-platform abstraction over the Facebook (Meta) Core SDKs: App Events, SDK settings and the
/// anonymous app-device ID. Call <see cref="Initialize"/> on the main thread during startup; the remaining
/// members are safe from any thread, as the native App Events loggers are.
/// </summary>
/// <remarks>
/// <para>
/// Member descriptions are adapted from Meta's official references:
/// <list type="bullet">
///   <item><description><see href="https://developers.facebook.com/docs/app-events/getting-started-app-events-android"/></description></item>
///   <item><description><see href="https://developers.facebook.com/docs/app-events/getting-started-app-events-ios"/></description></item>
/// </list>
/// </para>
/// <para>
/// Windows and MacCatalyst implementations are no-op stubs that return default values
/// (<see langword="true"/> for <see cref="bool"/>, <see langword="null"/> for nullable strings).
/// </para>
/// <para>
/// Where a setting exists on one platform only, the other platform's implementation is a documented
/// no-op rather than a thrown exception - see <see cref="SetAdvertiserTrackingEnabled"/>.
/// </para>
/// </remarks>
public interface IFacebookCoreService
{
    /// <summary>
    /// Configures and initializes the Facebook SDK programmatically. Call once during app startup on the
    /// main thread, before any other member. Idempotent: a second call is ignored.
    /// </summary>
    /// <param name="appId">
    /// The Facebook App ID from the Meta App Dashboard. Equivalent to the <c>FacebookAppID</c>
    /// Info.plist key / <c>com.facebook.sdk.ApplicationId</c> manifest meta-data.
    /// </param>
    /// <param name="clientToken">
    /// The Client Token from the Meta App Dashboard (Settings → Advanced → Client Token). Required by
    /// SDK 13+; initialization throws natively without it.
    /// </param>
    /// <remarks>
    /// Also activates the app for App Events, so an <c>fb_mobile_activate_app</c> event is logged and the
    /// automatic event logging session starts (when <see cref="SetAutoLogAppEventsEnabled"/> is on).
    /// </remarks>
    void Initialize(string appId, string clientToken);

    /// <summary>
    /// <see langword="true"/> once <see cref="Initialize"/> has completed on this platform.
    /// </summary>
    bool IsInitialized { get; }

    /// <summary>
    /// The native Facebook SDK version string (e.g. <c>18.1.1</c>), or <see langword="null"/> on platforms
    /// without a native SDK.
    /// </summary>
    string? SdkVersion { get; }

    /// <summary>
    /// The anonymous app-device ID the Facebook SDK generates and persists for this install.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the value attribution and analytics partners ask for as the Facebook <c>anon_id</c> - for
    /// example RevenueCat's <c>$fbAnonId</c> subscriber attribute, which its Meta integration needs before
    /// it will deliver a purchase event to Meta. It works with App Tracking Transparency denied, which is
    /// what makes it usable where the IDFA is not.
    /// </para>
    /// <para>
    /// Maps to <c>AppEvents.shared.anonymousID</c> on iOS and
    /// <c>AppEventsLogger.getAnonymousAppDeviceGUID(context)</c> on Android. Returns <see langword="null"/>
    /// before <see cref="Initialize"/> and on stub platforms.
    /// </para>
    /// </remarks>
    string? AnonymousId { get; }

    /// <summary>
    /// A user identifier of your choosing, attached to every subsequent App Event. Set to
    /// <see langword="null"/> to clear it (for example on sign-out).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Maps to <c>AppEvents.shared.userID</c> on iOS and the static <c>AppEventsLogger.userID</c> on
    /// Android. The value is persisted by the native SDK across launches until cleared. Before
    /// <see cref="Initialize"/> the getter returns <see langword="null"/> and the setter logs a warning and
    /// drops the value - the Android SDK throws on both otherwise.
    /// </para>
    /// <para>
    /// ⚠️ On Android the write is asynchronous: the SDK persists it on its analytics executor, so a read
    /// immediately after a set can still return the previous value (measured at ~80 ms on an emulator).
    /// Do not read it back as confirmation; the event stream carries it regardless.
    /// </para>
    /// </remarks>
    string? UserId { get; set; }

    /// <summary>
    /// Controls automatic App Event logging (app launches, in-app purchases, sessions). Defaults to
    /// <see langword="true"/> in the native SDKs. Turn it off before <see cref="Initialize"/> to defer
    /// collection until consent is granted, then turn it back on.
    /// </summary>
    void SetAutoLogAppEventsEnabled(bool isEnabled);

    /// <summary>
    /// Controls collection of the platform advertiser ID (IDFA on iOS, GAID on Android). Defaults to
    /// <see langword="true"/> in the native SDKs.
    /// </summary>
    void SetAdvertiserIdCollectionEnabled(bool isEnabled);

    /// <summary>
    /// iOS only, and effectively obsolete: historically told the SDK whether the user granted App Tracking
    /// Transparency, so events carry the <c>advertiser_tracking_enabled</c> flag Meta uses for matching.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Since Facebook iOS SDK 17 the SDK reads the ATT status itself (<c>ATTrackingManager</c>) and the
    /// underlying setter is deprecated: calling this on the bound 18.x SDK logs
    /// <c>isAdvertiserTrackingEnabled setter has been deprecated and the value will be read from ATT status</c>
    /// and changes nothing. Request tracking authorization with
    /// <c>ATTrackingManager.RequestTrackingAuthorization</c> before the first events instead; the SDK picks the
    /// result up on its own.
    /// </para>
    /// <para>
    /// Android has no equivalent - tracking permission is expressed through
    /// <see cref="SetAdvertiserIdCollectionEnabled"/> - so this is a no-op there.
    /// </para>
    /// </remarks>
    [Obsolete("Facebook iOS SDK 17+ derives this from the App Tracking Transparency status and ignores the setter; request ATT authorization with ATTrackingManager instead. No-op on Android.")]
    void SetAdvertiserTrackingEnabled(bool isEnabled);

    /// <summary>
    /// Logs a custom App Event.
    /// </summary>
    /// <param name="eventName">
    /// The event name. Standard Meta names such as <c>fb_mobile_complete_registration</c> are plain strings;
    /// custom names may contain letters, digits, spaces, <c>_</c> and <c>-</c>, up to 40 characters.
    /// </param>
    /// <param name="parameters">
    /// Optional parameters. Values must be <see cref="string"/> or numeric; anything else is sent as
    /// <c>ToString()</c>. Meta accepts at most 25 parameters per event.
    /// </param>
    /// <param name="valueToSum">
    /// Optional numeric value Meta aggregates across occurrences of this event (e.g. a price).
    /// </param>
    void LogEvent(string eventName, IReadOnlyDictionary<string, object>? parameters = null, double? valueToSum = null);

    /// <summary>
    /// Logs a purchase (<c>fb_mobile_purchase</c>) with an amount and an ISO 4217 currency code.
    /// </summary>
    /// <param name="amount">The purchase amount.</param>
    /// <param name="currencyCode">Three-letter ISO 4217 code such as <c>USD</c>, <c>EUR</c> or <c>CZK</c>.</param>
    /// <param name="parameters">Optional parameters, same rules as <see cref="LogEvent"/>.</param>
    void LogPurchase(decimal amount, string currencyCode, IReadOnlyDictionary<string, object>? parameters = null);

    /// <summary>
    /// Sends any queued App Events to Meta now instead of waiting for the SDK's periodic flush.
    /// </summary>
    void Flush();
}
