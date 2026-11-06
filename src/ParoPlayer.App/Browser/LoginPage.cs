using System.Net;

namespace ParoPlayer.App.Browser;

/// <summary>The PIN form of the browser session. Self-contained so it needs no Tailwind class from the app.</summary>
internal static class LoginPage
{
    public static string Render(string? message)
    {
        var error = message is null ? "" : $"<p class=\"error\" role=\"alert\">{WebUtility.HtmlEncode(message)}</p>";
        return Html.Replace("{error}", error, StringComparison.Ordinal);
    }

    private const string Html = """
        <!DOCTYPE html>
        <html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
        <meta name="color-scheme" content="dark"><title>ParoPlayer</title>
        <style>
        body{margin:0;min-height:100dvh;display:grid;place-items:center;background:#0c0e12;color:#e8eaf0;font-family:system-ui,sans-serif}
        main{width:min(22rem,calc(100% - 2rem))}
        h1{font-size:1.5rem;margin:0 0 .25rem} p{color:#8d93a1;margin:0 0 1rem}
        input,button{box-sizing:border-box;width:100%;min-height:3rem;border-radius:.75rem;border:0;font-size:1.125rem;padding:0 1rem}
        input{background:#1d2129;color:inherit;letter-spacing:.4em;text-align:center;margin-bottom:.75rem}
        button{background:#f4a24a;color:#1b1204;font-weight:600;cursor:pointer}
        .error{color:#f0686d;margin:.75rem 0 0}
        </style></head>
        <body><main>
        <h1>ParoPlayer</h1><p>Type the PIN shown on your phone.</p>
        <form method="post" action="/login">
        <input name="pin" inputmode="numeric" autocomplete="one-time-code" maxlength="6" autofocus aria-label="PIN" required>
        <button type="submit">Open</button></form>
        {error}
        </main></body></html>
        """;
}
