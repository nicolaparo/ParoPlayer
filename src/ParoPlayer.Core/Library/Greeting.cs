namespace ParoPlayer.Core.Library;

public static class Greeting
{
    public static string For(DateTimeOffset localTime) => localTime.Hour switch
    {
        >= 5 and < 12 => "Good morning",
        >= 12 and < 18 => "Good afternoon",
        _ => "Good evening",
    };
}
