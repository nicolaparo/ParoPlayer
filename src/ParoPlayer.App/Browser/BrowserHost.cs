using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ParoPlayer.Core.Browser;
using ParoPlayer.Core.Data;
using ParoPlayer.Core.Models;

namespace ParoPlayer.App.Browser;

/// <summary>
/// Kestrel + Blazor Server hosted inside the app (#26). It runs only while Wi-Fi transfer is on and serves the same
/// Razor components as the WebView, with a player of its own per browser circuit. Everything but the login page and
/// the static assets needs the PIN cookie.
/// </summary>
public sealed class BrowserHost(IServiceProvider appServices, BrowserAccess access, IRepository<Track> tracks) : IAsyncDisposable
{
    private const string CookieName = "paro_browser";
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    private WebApplication? app;

    public bool IsRunning => app is not null;

    public int Port { get; private set; }

    /// <summary>The PIN to type in the browser while the host runs.</summary>
    public string Pin => access.Pin;

    public async Task<int> StartAsync(bool localhostOnly, CancellationToken cancellationToken = default)
    {
        if (app is not null)
        {
            return Port;
        }

        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            ContentRootPath = FileSystem.AppDataDirectory,
            EnvironmentName = Environments.Production,
        });
        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            if (localhostOnly)
            {
                kestrel.ListenLocalhost(0);
            }
            else
            {
                kestrel.ListenAnyIP(0);
            }
        });
        builder.Services.AddSingleton<IHostLifetime, AppHostLifetime>();
        builder.Logging.AddDebug();
        builder.Services.AddRazorComponents().AddInteractiveServerComponents();
        BrowserServices.Add(builder.Services, appServices);

        var built = builder.Build();
        built.Use(ServePackagedAssetAsync);
        built.MapGet("/login", () => Results.Content(LoginPage.Render(null), "text/html; charset=utf-8"));
        built.MapPost("/login", (RequestDelegate)(context => RunAsync(context, LoginAsync)));
        built.Use(RequirePinAsync);
        built.UseAntiforgery();
        built.MapGet("/media/{id:guid}", (RequestDelegate)(context => RunAsync(context, ServeTrackAsync)));
        built.MapRazorComponents<BrowserApp>().AddInteractiveServerRenderMode();

        access.Open();
        await built.StartAsync(cancellationToken);
        Port = new Uri(built.Urls.First()).Port;
        app = built;
        return Port;
    }

    public async Task StopAsync()
    {
        if (app is not { } running)
        {
            return;
        }

        app = null;
        Port = 0;
        access.Close();
        await running.StopAsync();
        await running.DisposeAsync();
    }

    public async ValueTask DisposeAsync() => await StopAsync();

    private async Task<IResult> LoginAsync(HttpContext context)
    {
        var form = await context.Request.ReadFormAsync(context.RequestAborted);
        var client = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var result = access.TryLogin(client, form["pin"].ToString().Trim());
        switch (result.Outcome)
        {
            case LoginOutcome.Success:
                context.Response.Cookies.Append(CookieName, result.Token!, new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Strict, Path = "/" });
                return Results.Redirect("/");
            case LoginOutcome.LockedOut:
                context.Response.Headers.RetryAfter = ((int)result.RetryAfter.TotalSeconds).ToString();
                return Results.Content(
                    LoginPage.Render($"Too many attempts. Try again in {Math.Ceiling(result.RetryAfter.TotalMinutes)} min."),
                    "text/html; charset=utf-8",
                    statusCode: StatusCodes.Status429TooManyRequests);
            default:
                return Results.Content(LoginPage.Render("Wrong PIN."), "text/html; charset=utf-8", statusCode: StatusCodes.Status401Unauthorized);
        }
    }

    // Everything past the assets and the login page needs the cookie, including the SignalR connection and the audio.
    private async Task RequirePinAsync(HttpContext context, RequestDelegate next)
    {
        if (context.Request.Path.StartsWithSegments("/login") || access.IsValid(context.Request.Cookies[CookieName]))
        {
            await next(context);
        }
        else if (HttpMethods.IsGet(context.Request.Method) && context.Request.Headers.Accept.ToString().Contains("text/html", StringComparison.Ordinal))
        {
            context.Response.Redirect("/login");
        }
        else
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        }
    }

    // Handlers return an IResult that is executed here: the endpoint builder's generic return handling does not
    // work without runtime code generation (Android), a plain RequestDelegate does.
    private static async Task RunAsync(HttpContext context, Func<HttpContext, Task<IResult>> handler)
    {
        var result = await handler(context);
        await result.ExecuteAsync(context);
    }

    private async Task<IResult> ServeTrackAsync(HttpContext context)
    {
        var track = Guid.TryParse(context.Request.RouteValues["id"]?.ToString(), out var id) ? await tracks.GetAsync(id) : null;
        return track is null || !File.Exists(track.FilePath)
            ? Results.NotFound()
            : Results.File(track.FilePath, ContentTypes.TryGetContentType(track.FilePath, out var type) ? type : "audio/mpeg", enableRangeProcessing: true);
    }

    // The MAUI app ships wwwroot as package assets, not as files on disk, so static files are read from there.
    private static async Task ServePackagedAssetAsync(HttpContext context, RequestDelegate next)
    {
        if (!HttpMethods.IsGet(context.Request.Method) || PackagedPath(context.Request.Path.Value) is not { } path)
        {
            await next(context);
            return;
        }

        try
        {
            await using var stream = await FileSystem.OpenAppPackageFileAsync(path);
            context.Response.ContentType = ContentTypes.TryGetContentType(path, out var type) ? type : "application/octet-stream";
            await stream.CopyToAsync(context.Response.Body, context.RequestAborted);
        }
        catch (FileNotFoundException)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
        }
    }

    private static string? PackagedPath(string? requestPath)
    {
        if (requestPath is null || requestPath.Contains("..", StringComparison.Ordinal))
        {
            return null;
        }

        if (requestPath == "/_framework/blazor.web.js")
        {
            return "browser/blazor.web.js";
        }

        return requestPath.StartsWith("/css/", StringComparison.Ordinal)
            || requestPath.StartsWith("/js/", StringComparison.Ordinal)
            || requestPath.StartsWith("/lib/", StringComparison.Ordinal)
            || requestPath.StartsWith("/fonts/", StringComparison.Ordinal)
            ? "wwwroot" + requestPath
            : null;
    }
}
