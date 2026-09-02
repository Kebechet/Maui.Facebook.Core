using Foundation;
using UIKit;

namespace DemoApp;
[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
    {
        CopyLaunchArguments(NSProcessInfo.ProcessInfo.Arguments);
        return base.FinishedLaunching(application, launchOptions);
    }

    // The iOS counterpart of MainActivity's intent extras, so a device run is scriptable from a Mac:
    //   xcrun devicectl device process launch --console --device <udid> com.kebechet.demoapp \
    //       -- --appId 123 --clientToken abc --autoRun true
    // The page reads these through HarnessLaunchOptions.
    private static void CopyLaunchArguments(string[] arguments)
    {
        string? appId = null, clientToken = null;
        var autoRun = false;
        for (var i = 0; i + 1 < arguments.Length; i++)
        {
            switch (arguments[i])
            {
                case "--appId": appId = arguments[++i]; break;
                case "--clientToken": clientToken = arguments[++i]; break;
                case "--autoRun": autoRun = bool.TryParse(arguments[++i], out var parsed) && parsed; break;
            }
        }

        if (appId is { Length: > 0 })
        {
            Preferences.Default.Set(HarnessLaunchOptions.AppIdKey, appId);
        }
        if (clientToken is not null)
        {
            Preferences.Default.Set(HarnessLaunchOptions.ClientTokenKey, clientToken);
        }
        Preferences.Default.Set(HarnessLaunchOptions.AutoRunKey, autoRun);
    }
}
