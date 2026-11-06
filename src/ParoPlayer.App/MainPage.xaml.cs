using ParoPlayer.Core.Navigation;

namespace ParoPlayer.App;

public partial class MainPage : ContentPage
{
	public MainPage(BackStack backStack)
	{
		InitializeComponent();
#if ANDROID
		// Back closes the newest open sheet, then the selection; only when nothing is open does it leave the page.
		blazorWebView.BlazorWebViewInitialized += (_, _) =>
		{
			if (Platform.CurrentActivity is MainActivity activity)
			{
				activity.BackInterceptor = backStack.PressAsync;
			}
		};
#endif
	}
}
