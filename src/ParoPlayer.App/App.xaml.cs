namespace ParoPlayer.App;

public partial class App : Application
{
    private const double DesktopWindowWidth = 420;
    private const double DesktopWindowHeight = 860;

    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new MainPage()) { Title = "ParoPlayer" };

#if WINDOWS
        // Phone-shaped window so the desktop build previews the mobile layout.
        window.Width = DesktopWindowWidth;
        window.Height = DesktopWindowHeight;
#endif

        return window;
    }
}
