namespace ParoPlayer.App.Services;

/// <summary>User toggles from the Settings screen, persisted in app preferences.</summary>
public class AppSettings(IPreferences preferences)
{
    public bool WifiOnly
    {
        get => preferences.Get(nameof(WifiOnly), true);
        set => preferences.Set(nameof(WifiOnly), value);
    }

    public bool Gapless
    {
        get => preferences.Get(nameof(Gapless), true);
        set => preferences.Set(nameof(Gapless), value);
    }

    public bool Crossfade
    {
        get => preferences.Get(nameof(Crossfade), false);
        set => preferences.Set(nameof(Crossfade), value);
    }

    public bool AndroidAuto
    {
        get => preferences.Get(nameof(AndroidAuto), true);
        set => preferences.Set(nameof(AndroidAuto), value);
    }

    public bool LockScreenControls
    {
        get => preferences.Get(nameof(LockScreenControls), true);
        set => preferences.Set(nameof(LockScreenControls), value);
    }
}
