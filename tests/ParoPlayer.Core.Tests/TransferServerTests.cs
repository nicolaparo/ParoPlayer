using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using ParoPlayer.Core.Data;
using ParoPlayer.Core.Import;
using ParoPlayer.Core.Models;
using ParoPlayer.Core.Services;
using ParoPlayer.Core.Storage;
using ParoPlayer.Core.Transfer;

namespace ParoPlayer.Core.Tests;

public sealed class TransferServerTests : IAsyncLifetime
{
    private readonly string root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly FakeTimeProvider time = new();
    private readonly InMemoryRepository<Track> repository = new();
    private readonly HttpClient client = new(new HttpClientHandler { UseCookies = false });
    private TransferSessions sessions = null!;
    private TransferServer server = null!;
    private string baseUrl = "";

    public Task InitializeAsync() => StartAsync(new TransferOptions { MaxUploadBytes = 1024 });

    public async Task DisposeAsync()
    {
        client.Dispose();
        await server.StopAsync();
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private async Task StartAsync(TransferOptions options)
    {
        var folder = new LibraryFolder(Path.Combine(root, "library"));
        var library = new LibraryService(repository, time);
        var import = new TrackImportService(folder, new FileNameMetadataReader(), library, new CoverStore(folder, new PassThroughCoverImageProcessor()));
        sessions = new TransferSessions(time, options);
        server = new TransferServer(sessions, repository, import, options, time);
        var port = await server.StartAsync(localhostOnly: true);
        baseUrl = $"http://localhost:{port}";
    }

    private async Task<(string RequestId, string Code)> RequestAccessAsync()
    {
        using var response = await client.PostAsync($"{baseUrl}/api/access", new StringContent("""{"deviceName":"Test PC"}""", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (json.RootElement.GetProperty("requestId").GetString()!, json.RootElement.GetProperty("code").GetString()!);
    }

    private async Task<string> ConnectAsync()
    {
        var (requestId, _) = await RequestAccessAsync();
        Assert.True(sessions.Approve());
        using var poll = await client.GetAsync($"{baseUrl}/api/access/{requestId}");
        var cookie = Assert.Single(poll.Headers.GetValues("Set-Cookie"));
        return cookie.Split(';')[0];
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string cookie, HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, baseUrl + path) { Content = content };
        if (cookie.Length > 0)
        {
            request.Headers.Add("Cookie", cookie);
        }

        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Page_IsServedWithoutToken()
    {
        using var response = await client.GetAsync(baseUrl + "/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("html", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("GET", "/api/tracks")]
    [InlineData("PUT", "/api/upload?name=a.mp3")]
    [InlineData("POST", "/api/disconnect")]
    [InlineData("GET", "/api/tracks/00000000-0000-0000-0000-000000000000/file")]
    public async Task ProtectedRoutes_WithoutToken_AreRejected(string method, string path)
    {
        using var response = await SendAsync(new HttpMethod(method), path, "");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedRoutes_WithWrongToken_AreRejected()
    {
        using var response = await SendAsync(HttpMethod.Get, "/api/tracks", "paro_session=00000000000000000000000000000000");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Approve_IssuesCookieOnceAndGrantsAccess()
    {
        AccessRequest? seen = null;
        sessions.AccessRequested += r => seen = r;

        var (requestId, code) = await RequestAccessAsync();

        Assert.Matches(@"^\d{4}$", code);
        Assert.Equal(("Test PC", code), (seen?.DeviceName, seen?.Code));
        using (var pending = await client.GetAsync($"{baseUrl}/api/access/{requestId}"))
        {
            Assert.Contains("pending", await pending.Content.ReadAsStringAsync());
            Assert.False(pending.Headers.Contains("Set-Cookie"));
        }

        sessions.Approve();
        using var approved = await client.GetAsync($"{baseUrl}/api/access/{requestId}");
        var cookie = Assert.Single(approved.Headers.GetValues("Set-Cookie"));
        Assert.Contains("approved", await approved.Content.ReadAsStringAsync());
        Assert.Contains("HttpOnly", cookie);
        Assert.Contains("SameSite=Strict", cookie);
        Assert.Matches(@"^paro_session=[0-9a-f]{32};", cookie);

        using var again = await client.GetAsync($"{baseUrl}/api/access/{requestId}");
        Assert.False(again.Headers.Contains("Set-Cookie"));

        using var tracks = await SendAsync(HttpMethod.Get, "/api/tracks", cookie.Split(';')[0]);
        Assert.Equal(HttpStatusCode.OK, tracks.StatusCode);
    }

    [Fact]
    public async Task Deny_ReportsDeniedAndGrantsNothing()
    {
        var (requestId, _) = await RequestAccessAsync();

        sessions.Deny();

        using var poll = await client.GetAsync($"{baseUrl}/api/access/{requestId}");
        Assert.Contains("denied", await poll.Content.ReadAsStringAsync());
        Assert.False(poll.Headers.Contains("Set-Cookie"));
        Assert.Equal(TransferState.Idle, sessions.State);
    }

    [Fact]
    public async Task SecondDevice_WhileConnected_GetsConflict()
    {
        await ConnectAsync();

        using var response = await client.PostAsync($"{baseUrl}/api/access", new StringContent("""{"deviceName":"Other"}""", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task AccessRequests_AreRateLimited()
    {
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 7; i++)
        {
            using var response = await client.PostAsync($"{baseUrl}/api/access", new StringContent("""{"deviceName":"Spam"}""", Encoding.UTF8, "application/json"));
            statuses.Add(response.StatusCode);
            sessions.Deny();
        }

        Assert.Equal(5, statuses.Count(s => s == HttpStatusCode.OK));
        Assert.Equal(2, statuses.Count(s => s == HttpStatusCode.TooManyRequests));
    }

    [Fact]
    public async Task Disconnect_EndsSession()
    {
        var cookie = await ConnectAsync();

        using var disconnect = await SendAsync(HttpMethod.Post, "/api/disconnect", cookie);
        using var after = await SendAsync(HttpMethod.Get, "/api/tracks", cookie);

        Assert.Equal(HttpStatusCode.NoContent, disconnect.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
        Assert.Equal(TransferState.Idle, sessions.State);
    }

    [Fact]
    public async Task UiDisconnect_EndsSession()
    {
        var cookie = await ConnectAsync();

        sessions.Disconnect();

        using var after = await SendAsync(HttpMethod.Get, "/api/tracks", cookie);
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    [Fact]
    public async Task IdleSession_Expires()
    {
        var cookie = await ConnectAsync();

        time.Advance(TimeSpan.FromMinutes(9));
        using var stillValid = await SendAsync(HttpMethod.Get, "/api/tracks", cookie);
        time.Advance(TimeSpan.FromMinutes(11));
        using var expired = await SendAsync(HttpMethod.Get, "/api/tracks", cookie);

        Assert.Equal(HttpStatusCode.OK, stillValid.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
        Assert.Equal(TransferState.Idle, sessions.State);
    }

    [Fact]
    public async Task PendingRequest_TimesOutAsDenied()
    {
        var (requestId, _) = await RequestAccessAsync();

        time.Advance(TimeSpan.FromSeconds(61));

        using var poll = await client.GetAsync($"{baseUrl}/api/access/{requestId}");
        Assert.Contains("denied", await poll.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Upload_ImportsTrackIntoLibrary()
    {
        var cookie = await ConnectAsync();

        using var response = await SendAsync(HttpMethod.Put, "/api/upload?name=" + Uri.EscapeDataString("My Song.mp3"), cookie, new ByteArrayContent(Encoding.UTF8.GetBytes("audio")));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var track = Assert.Single(await repository.GetAllAsync());
        Assert.Equal("My Song", track.Title);
        Assert.Equal("audio", await File.ReadAllTextAsync(track.FilePath));
        Assert.StartsWith(Path.Combine(root, "library"), track.FilePath);
    }

    [Theory]
    [InlineData("..\\..\\evil.mp3", "evil.mp3")]
    [InlineData("../../etc/passwd.mp3", "passwd.mp3")]
    [InlineData("C:\\Windows\\sys.flac", "sys.flac")]
    public async Task Upload_PathTraversalNames_AreReducedToTheLeafName(string name, string expected)
    {
        var cookie = await ConnectAsync();

        using var response = await SendAsync(HttpMethod.Put, "/api/upload?name=" + Uri.EscapeDataString(name), cookie, new ByteArrayContent([1, 2, 3]));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var track = Assert.Single(await repository.GetAllAsync());
        Assert.Equal(Path.Combine(root, "library", expected), track.FilePath);
    }

    [Theory]
    [InlineData("..")]
    [InlineData("virus.exe")]
    [InlineData("noextension")]
    [InlineData(".mp3")]
    public async Task Upload_BadNames_AreRejected(string name)
    {
        var cookie = await ConnectAsync();

        using var response = await SendAsync(HttpMethod.Put, "/api/upload?name=" + Uri.EscapeDataString(name), cookie, new ByteArrayContent([1]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await repository.GetAllAsync());
    }

    [Fact]
    public async Task Upload_OverSizeCap_IsRejectedAndNothingImported()
    {
        var cookie = await ConnectAsync();

        using var response = await SendAsync(HttpMethod.Put, "/api/upload?name=big.mp3", cookie, new ByteArrayContent(new byte[2048]));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Empty(await repository.GetAllAsync());
    }

    [Fact]
    public async Task Download_ByTrackId_StreamsTheFile()
    {
        var cookie = await ConnectAsync();
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "song.mp3");
        await File.WriteAllTextAsync(path, "song-bytes");
        var track = new Track { Title = "Song", Artist = "A", FilePath = path, DurationSeconds = 3 };
        await repository.AddAsync(track);

        using var list = await SendAsync(HttpMethod.Get, "/api/tracks", cookie);
        using var file = await SendAsync(HttpMethod.Get, $"/api/tracks/{track.Id}/file", cookie);
        using var missing = await SendAsync(HttpMethod.Get, $"/api/tracks/{Guid.NewGuid()}/file", cookie);
        using var notAnId = await SendAsync(HttpMethod.Get, "/api/tracks/..%5C..%5Cwindows/file", cookie);

        using var json = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        var item = json.RootElement[0];
        Assert.Equal(("Song", 3000, 10), (item.GetProperty("title").GetString(), item.GetProperty("durationMs").GetInt32(), item.GetProperty("sizeBytes").GetInt32()));
        Assert.Equal("song-bytes", await file.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, notAnId.StatusCode);
    }

    [Fact]
    public async Task StopAsync_EndsSessionAndStopsListening()
    {
        var cookie = await ConnectAsync();

        await server.StopAsync();

        Assert.False(server.IsRunning);
        Assert.Equal(TransferState.Idle, sessions.State);
        await Assert.ThrowsAnyAsync<HttpRequestException>(() => SendAsync(HttpMethod.Get, "/api/tracks", cookie));
    }

    [Theory]
    [InlineData("song.MP3", "song.MP3")]
    [InlineData("a/b/c.flac", "c.flac")]
    [InlineData("bad<>:name.mp3", "bad___name.mp3")]
    [InlineData("  ", null)]
    [InlineData("x.txt", null)]
    [InlineData("...mp3", null)]
    public void FileName_Sanitize(string input, string? expected) => Assert.Equal(expected, TransferFileName.Sanitize(input));
}
