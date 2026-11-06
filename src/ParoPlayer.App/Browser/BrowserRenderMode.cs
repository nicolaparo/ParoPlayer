using Microsoft.AspNetCore.Components.Web;

namespace ParoPlayer.App.Browser;

/// <summary>Interactive Server without prerendering: the page renders once, in the circuit.</summary>
public static class BrowserRenderMode
{
    public static readonly InteractiveServerRenderMode Mode = new(prerender: false);
}
