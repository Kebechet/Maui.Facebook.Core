namespace DemoApp;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    // The harness page drives IFacebookCoreService.Initialize with credentials typed at runtime, so nothing
    // Facebook-related happens at startup here on purpose.
    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new MainPage());
    }
}
