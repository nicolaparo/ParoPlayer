using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using ParoPlayer.Core.Data;
using ParoPlayer.Core.Import;
using ParoPlayer.Core.Models;

namespace ParoPlayer.Core.Transfer;

/// <summary>
/// Temporary HTTP server that lets a browser on the same Wi-Fi upload and download tracks (see docs/wifi-transfer.md, "v1 contract").
/// Requests are handled one at a time on a single accept loop.
/// </summary>
public sealed class TransferServer(
    TransferSessions sessions,
    IRepository<Track> tracks,
    TrackImportService import,
    TransferOptions options,
    TimeProvider timeProvider) : IAsyncDisposable
{
    private const string CookieName = "paro_session";
    private const int MaxBodyBytes = 4096;
    private const int MaxDeviceNameLength = 64;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private HttpListener? listener;
    private CancellationTokenSource? stopping;
    private Task? loop;
    private ITimer? expiryTimer;

    public bool IsRunning => loop is not null;

    /// <summary>The port the server listens on, or 0 when stopped.</summary>
    public int Port { get; private set; }

    /// <summary>
    /// Starts listening and returns the port. <paramref name="localhostOnly"/> binds localhost (Windows preview),
    /// otherwise all interfaces (Android). Port 0 picks a free port.
    /// </summary>
    public Task<int> StartAsync(bool localhostOnly = false, int port = 0)
    {
        if (loop is not null)
        {
            return Task.FromResult(Port);
        }

        var host = localhostOnly ? "localhost" : "*";
        for (var attempt = 0; ; attempt++)
        {
            var candidate = port != 0 ? port : FreePort();
            var next = new HttpListener();
            next.Prefixes.Add($"http://{host}:{candidate}/");
            try
            {
                next.Start();
            }
            catch (HttpListenerException) when (port == 0 && attempt < 5)
            {
                next.Close();
                continue;
            }

            listener = next;
            Port = candidate;
            break;
        }

        stopping = new CancellationTokenSource();
        expiryTimer = timeProvider.CreateTimer(_ => sessions.ExpireIfIdle(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        loop = RunAsync(listener, stopping.Token);
        return Task.FromResult(Port);
    }

    /// <summary>Stops the server, ends the session and waits for the accept loop to finish.</summary>
    public async Task StopAsync()
    {
        if (loop is null)
        {
            return;
        }

        var running = loop;
        loop = null;
        expiryTimer?.Dispose();
        expiryTimer = null;
        await stopping!.CancelAsync();
        listener!.Close();
        await running;
        stopping.Dispose();
        stopping = null;
        listener = null;
        Port = 0;
        sessions.Disconnect();
    }

    public async ValueTask DisposeAsync() => await StopAsync();

    private static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Any, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private async Task RunAsync(HttpListener active, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await active.GetContextAsync();
            }
            catch (Exception) when (cancellationToken.IsCancellationRequested || !active.IsListening)
            {
                return;
            }
            catch (HttpListenerException)
            {
                continue;
            }

            try
            {
                await HandleAsync(context, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception)
            {
                TryRespond(context, 500);
            }
        }
    }

    private async Task HandleAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var request = context.Request;
        var response = context.Response;
        response.Headers["X-Content-Type-Options"] = "nosniff";
        response.Headers["Cache-Control"] = "no-store";

        var path = request.Url?.AbsolutePath ?? "/";
        var method = request.HttpMethod;

        if (method == "GET" && path == "/")
        {
            await WritePageAsync(response, cancellationToken);
        }
        else if (method == "POST" && path == "/api/access")
        {
            await HandleAccessRequestAsync(context, cancellationToken);
        }
        else if (method == "GET" && path.StartsWith("/api/access/", StringComparison.Ordinal))
        {
            HandleAccessPoll(context, path["/api/access/".Length..]);
        }
        else if (!sessions.Validate(request.Cookies[CookieName]?.Value))
        {
            Respond(response, 401);
        }
        else if (method == "GET" && path == "/api/tracks")
        {
            await WriteTrackListAsync(response);
        }
        else if (method == "GET" && TrackFileId(path) is { } id)
        {
            await WriteTrackFileAsync(response, id, cancellationToken);
        }
        else if (method == "PUT" && path == "/api/upload")
        {
            await HandleUploadAsync(context, cancellationToken);
        }
        else if (method == "POST" && path == "/api/disconnect")
        {
            response.Headers.Add("Set-Cookie", $"{CookieName}=; Path=/; Max-Age=0; HttpOnly; SameSite=Strict");
            sessions.Disconnect();
            Respond(response, 204);
        }
        else
        {
            Respond(response, 404);
        }
    }

    private async Task HandleAccessRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var deviceName = "Unknown device";
        try
        {
            var body = await ReadBodyAsync(context.Request, cancellationToken);
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("deviceName", out var value) && value.ValueKind == JsonValueKind.String)
            {
                var cleaned = new string((value.GetString() ?? "").Where(c => !char.IsControl(c)).ToArray()).Trim();
                if (cleaned.Length > 0)
                {
                    deviceName = cleaned.Length > MaxDeviceNameLength ? cleaned[..MaxDeviceNameLength] : cleaned;
                }
            }
        }
        catch (JsonException)
        {
            Respond(context.Response, 400);
            return;
        }

        var address = context.Request.RemoteEndPoint?.Address.ToString() ?? "";
        var result = sessions.RequestAccess(deviceName, address);
        switch (result.Outcome)
        {
            case AccessOutcome.Created:
                WriteJson(context.Response, new { requestId = result.Request!.RequestId, code = result.Request.Code });
                break;
            case AccessOutcome.Connected:
                Respond(context.Response, 409);
                break;
            default:
                Respond(context.Response, 429);
                break;
        }
    }

    private void HandleAccessPoll(HttpListenerContext context, string requestId)
    {
        var poll = sessions.Poll(requestId);
        if (poll is null)
        {
            Respond(context.Response, 404);
            return;
        }

        if (poll.Token is not null)
        {
            context.Response.Headers.Add("Set-Cookie", $"{CookieName}={poll.Token}; Path=/; HttpOnly; SameSite=Strict");
        }

        var status = poll.Status.ToString().ToLowerInvariant();
        WriteBytes(context.Response, 200, "application/json", Encoding.UTF8.GetBytes($"{{\"status\":\"{status}\"}}"));
    }

    private async Task WriteTrackListAsync(HttpListenerResponse response)
    {
        var all = await tracks.GetAllAsync();
        var list = all.Select(t => new
        {
            id = t.Id,
            title = t.Title,
            artist = t.Artist,
            album = t.Album,
            durationMs = t.DurationSeconds * 1000L,
            sizeBytes = File.Exists(t.FilePath) ? new FileInfo(t.FilePath).Length : 0L,
        });
        WriteJson(response, list);
    }

    private async Task WriteTrackFileAsync(HttpListenerResponse response, Guid id, CancellationToken cancellationToken)
    {
        var track = await tracks.GetAsync(id);
        if (track is null || !File.Exists(track.FilePath))
        {
            Respond(response, 404);
            return;
        }

        var fileName = Path.GetFileName(track.FilePath);
        await using var file = new FileStream(track.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        response.StatusCode = 200;
        response.ContentType = ContentTypeFor(fileName);
        response.ContentLength64 = file.Length;
        response.Headers["Content-Disposition"] = $"attachment; filename*=UTF-8''{Uri.EscapeDataString(fileName)}";
        await file.CopyToAsync(response.OutputStream, cancellationToken);
        response.Close();
    }

    private async Task HandleUploadAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var request = context.Request;
        var name = TransferFileName.Sanitize(request.QueryString["name"]);
        if (name is null)
        {
            Respond(context.Response, 400);
            return;
        }

        if (request.ContentLength64 > options.MaxUploadBytes)
        {
            Respond(context.Response, 413);
            return;
        }

        var tempFolder = Path.Combine(Path.GetTempPath(), "paroplayer-transfer");
        Directory.CreateDirectory(tempFolder);
        var tempPath = Path.Combine(tempFolder, $"{Guid.NewGuid():N}.tmp");
        try
        {
            if (!await CopyWithCapAsync(request.InputStream, tempPath, cancellationToken))
            {
                Respond(context.Response, 413);
                return;
            }

            Track track;
            await using (var content = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true))
            {
                track = await import.ImportAsync(name, content, cancellationToken);
            }

            sessions.Validate(request.Cookies[CookieName]?.Value);
            WriteJson(context.Response, new { id = track.Id, title = track.Title }, 201);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    /// <summary>Streams the body to <paramref name="path"/>. Returns false when it exceeds the upload cap.</summary>
    private async Task<bool> CopyWithCapAsync(Stream source, string path, CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long total = 0;
        await using var target = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, buffer.Length, useAsync: true);
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;
            if (total > options.MaxUploadBytes)
            {
                return false;
            }

            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        return true;
    }

    private static async Task<string> ReadBodyAsync(HttpListenerRequest request, CancellationToken cancellationToken)
    {
        var buffer = new byte[MaxBodyBytes];
        var length = 0;
        int read;
        while (length < buffer.Length && (read = await request.InputStream.ReadAsync(buffer.AsMemory(length), cancellationToken)) > 0)
        {
            length += read;
        }

        return Encoding.UTF8.GetString(buffer, 0, length);
    }

    private static Guid? TrackFileId(string path)
    {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts is ["api", "tracks", var id, "file"] && Guid.TryParse(id, out var guid) ? guid : null;
    }

    private static async Task WritePageAsync(HttpListenerResponse response, CancellationToken cancellationToken)
    {
        await using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("transfer.html");
        if (resource is null)
        {
            WriteBytes(response, 200, "text/html; charset=utf-8", Encoding.UTF8.GetBytes("<!doctype html><title>ParoPlayer</title><p>ParoPlayer transfer page is missing.</p>"));
            return;
        }

        response.StatusCode = 200;
        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = resource.Length;
        await resource.CopyToAsync(response.OutputStream, cancellationToken);
        response.Close();
    }

    private static void WriteJson(HttpListenerResponse response, object value, int status = 200) =>
        WriteBytes(response, status, "application/json", JsonSerializer.SerializeToUtf8Bytes(value, Json));

    private static void WriteBytes(HttpListenerResponse response, int status, string contentType, byte[] bytes)
    {
        response.StatusCode = status;
        response.ContentType = contentType;
        response.ContentLength64 = bytes.Length;
        response.OutputStream.Write(bytes);
        response.Close();
    }

    private static void Respond(HttpListenerResponse response, int status)
    {
        response.StatusCode = status;
        response.ContentLength64 = 0;
        response.Close();
    }

    private static void TryRespond(HttpListenerContext context, int status)
    {
        try
        {
            Respond(context.Response, status);
        }
        catch (Exception)
        {
            context.Response.Abort();
        }
    }

    private static string ContentTypeFor(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".mp3" => "audio/mpeg",
        ".m4a" => "audio/mp4",
        ".aac" => "audio/aac",
        ".flac" => "audio/flac",
        ".wav" => "audio/wav",
        ".ogg" or ".oga" or ".opus" => "audio/ogg",
        ".aif" or ".aiff" => "audio/aiff",
        _ => "application/octet-stream",
    };
}
