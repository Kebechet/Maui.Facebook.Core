using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace DemoApp;
[Activity(Name = "com.kebechet.demoapp.MainActivity", Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        CopyLaunchExtras(Intent);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        CopyLaunchExtras(intent);
    }

    // Lets a device run be driven from the shell, so the harness is scriptable rather than tap-only:
    //   adb shell am start -n com.kebechet.demoapp/.MainActivity \
    //       --es appId 123 --es clientToken abc --ez autoRun true
    // The page reads these through HarnessLaunchOptions.
    private static void CopyLaunchExtras(Intent? intent)
    {
        if (intent?.Extras is null)
        {
            return;
        }

        if (intent.GetStringExtra("appId") is { Length: > 0 } appId)
        {
            Preferences.Default.Set(HarnessLaunchOptions.AppIdKey, appId);
        }
        if (intent.GetStringExtra("clientToken") is { } clientToken)
        {
            Preferences.Default.Set(HarnessLaunchOptions.ClientTokenKey, clientToken);
        }
        Preferences.Default.Set(HarnessLaunchOptions.AutoRunKey, intent.GetBooleanExtra("autoRun", false));
    }
}
