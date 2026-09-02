namespace DemoApp;

/// <summary>
/// Credentials and the auto-run flag handed to the harness page. Filled from Android intent extras
/// (<c>Platforms/Android/MainActivity.cs</c>) or iOS process arguments (<c>Platforms/iOS/AppDelegate.cs</c>);
/// elsewhere they stay at their defaults and the page is driven by hand.
/// </summary>
public static class HarnessLaunchOptions
{
    public const string AppIdKey = "harness.appId";
    public const string ClientTokenKey = "harness.clientToken";
    public const string AutoRunKey = "harness.autoRun";

    public static string AppId => Preferences.Default.Get(AppIdKey, string.Empty);
    public static string ClientToken => Preferences.Default.Get(ClientTokenKey, string.Empty);

    /// <summary>Read-once: a launch with autoRun runs the sweep exactly one time, not on every re-render.</summary>
    public static bool ConsumeAutoRun()
    {
        var autoRun = Preferences.Default.Get(AutoRunKey, false);
        if (autoRun)
        {
            Preferences.Default.Set(AutoRunKey, false);
        }
        return autoRun;
    }
}
