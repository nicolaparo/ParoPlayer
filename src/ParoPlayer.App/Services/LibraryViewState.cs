namespace ParoPlayer.App.Services;

/// <summary>What the Library page showed when the user left it, so Back brings the same tab and search back.</summary>
public class LibraryViewState
{
    public string Tab { get; set; } = "Songs";

    public string Query { get; set; } = "";
}
