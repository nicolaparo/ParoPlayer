# Wi-Fi transfer (computer <-> phone)


An activity that starts a temporary web server on the phone so a computer browser on the same Wi-Fi can upload songs (drag and drop) and download songs.

- Server runs only while the activity is open, inside a foreground service while transfers are active. Stops on leave/disconnect. Spike `HttpListener` first (works on Android .NET, no ASP.NET dependency); Kestrel is the alternative.
- **Security flow** (patterns seen in open-source Android transfer apps such as DropDroid and LocalShare; the exact design is ours): the browser requests access, the phone shows device name, IP and a 4-digit code that is also shown in the browser; the user taps **Allow**. Only then is a random 128-bit session token issued (HttpOnly cookie, `SameSite=Strict`). No token, no access to any route.
- One approved device at a time; session expires on idle, on leaving the screen and on **Disconnect**. Rate-limit the approval request; limit upload size and accept audio types only; sanitize file names (no path traversal), write into the library folder only.
- Plain HTTP on a LAN means traffic is unencrypted. The screen says so; optional later: TLS with a self-signed certificate.
- Web UI is a small static page served from `wwwroot/transfer` (offline, no CDN, list layout, no tables), mobile-friendly too.


## v1 contract (shared by server, web page and app)

Plain HTTP, port chosen by the server. All routes except `/`, `POST /api/access` and `GET /api/access/{requestId}` need the session cookie `paro_session`.

| Route | Behaviour |
|---|---|
| `GET /` | Serves the static page (embedded resource `transfer.html`). |
| `POST /api/access` | Body `{"deviceName":"..."}`. Returns `{requestId, code}` (requestId random 128-bit, code 4 digits). One pending request at a time; rate-limited; 409 if another device is connected. |
| `GET /api/access/{requestId}` | Returns `{"status":"pending"|"approved"|"denied"}`. On first `approved` it sets the cookie (HttpOnly, SameSite=Strict, random 128-bit token) exactly once. |
| `GET /api/tracks` | JSON list `[{id,title,artist,album,durationMs,sizeBytes}]`. |
| `GET /api/tracks/{id}/file` | Streams the file; `id` is looked up in the repository, never a client path. |
| `PUT /api/upload?name=<encodeURIComponent>` | Raw file body, streamed to a temp file with a size cap, audio extensions only, sanitized name, then imported through `TrackImportService`. |
| `POST /api/disconnect` | Ends the session. |

Session ends on idle timeout (via `TimeProvider`), on `Disconnect`, and when the server stops. v1 uses `DeviceDisplay.KeepScreenOn` while the page is open instead of a foreground service. On Windows bind `localhost`; on Android bind `*`.


## Browser session: the whole app in a desktop browser (#26)

Status: **built and verified on a phone** (Realme, Android, arm64). Decisions taken with the owner on 2026-11-06.

**Goal.** While Wi-Fi transfer is on, a desktop browser on the same LAN opens the whole app (Library, Playlists, Playback, Download; everything except Settings), plays music in the browser and edits playlists. Edits go to the phone's own database.

### Decisions

| Topic | Decision |
|---|---|
| Hosting | Blazor Server (Kestrel + SignalR) inside the MAUI app, reusing the existing Razor components (same assembly, no Razor class library). |
| Playback | In the browser: audio is streamed from the phone and played by an HTML `Audio` element. Choosing phone vs computer output is a separate issue (#32). |
| Access | 6-digit PIN, new for every run of the server, shown on the Wi-Fi transfer screen. |
| Scope | Library, playlists, playback, download. Settings and Wi-Fi transfer are not served. |

### How it works

- `BrowserHost` (`src/ParoPlayer.App/Browser`) starts with the Wi-Fi transfer screen (next to the v1 `TransferServer`, which is unchanged) and stops with it. Kestrel listens on all interfaces on Android, on localhost on Windows, on a free port.
- `BrowserServices` builds the host's container. The data and services are the phone's own singletons (one SQLite connection, so a browser edit is a phone edit). Per browser circuit: `IPlayerService` (`BrowserPlayerService`), `PlayerState`, `BackStack`, `LibraryViewState`, `CoverImages`. Phone-only abilities (`IFilePicker`, `IClipboard`, `IHapticFeedback`, `ITrackSharer`, `IMusicImporter`, `INotificationPermission`) are stubs. `HostShell.ShowSettings` is false: the nav entry and the Library gear are hidden, and `Routes.razor` shows "Page not found" for `/settings` and `/transfer`.
- `BrowserPlayerService` keeps the `PlayQueue` on the server and drives `wwwroot/js/paro-audio.js` through JS interop. It polls the element (position, paused, ended) every 500 ms and moves the queue on when a track ends.
- `GET /media/{trackId}` streams the file with range support (seeking); the id is looked up in the repository, never a client path. Covers keep using data URIs.
- Static files (`css`, `js`, `lib`, `fonts`) and `blazor.web.js` are read from the app package (`FileSystem.OpenAppPackageFileAsync`), because MAUI ships `wwwroot` as package assets, not as files.
- **PIN** (`ParoPlayer.Core.Browser.BrowserAccess`, tested): a correct PIN gives a random 128-bit token in an HttpOnly, `SameSite=Strict` cookie. A middleware requires it for everything except `/login` and the static assets, including the SignalR connection and `/media`. Five wrong PINs lock a client for 5 minutes, 20 wrong PINs in total lock everyone for 5 minutes. Tokens expire after 30 idle minutes, and stopping the server forgets the PIN and all tokens. Plain HTTP on a LAN is unencrypted and the screen says so.

### Android findings (keep these when upgrading .NET)

- **There is no ASP.NET Core runtime pack for Android.** The `Microsoft.AspNetCore.App` framework reference compiles (reference pack) but ships nothing, and the app would crash at startup. The app therefore references the managed assemblies of the **linux-arm64** runtime pack and `blazor.web.js` from `Microsoft.AspNetCore.App.Internal.Assets` (see the csproj; both are downloaded with `PackageDownload` and the version is in `AspNetPackVersion`, keep it equal to the SDK's runtime). The win-x64 pack does not work: its ReadyToRun images fail with `BadImageFormatException` on arm64.
- **Keep `AspNetPackVersion` equal to the version of the `Microsoft.AspNetCore.Components.*` packages that MAUI restores** (see `obj/project.assets.json`, today `11.0.0-preview.6.26359.118`), not to the SDK runtime. The phone's WebView runs the JavaScript that ships with those packages; with a newer pack the shared .NET side and that script disagree (`OnSpacerBeforeVisible expects 4 parameters, but received 3`) and the Library list stays empty in the phone app.
- Consequence: the app now only runs on **arm64 devices**. The assemblies are loaded when the app starts (not only when the browser session starts), and the APK still ships an x86_64 assembly store, so an x86_64 emulator most likely cannot launch the app at all. Restricting the Android RIDs to arm64 would say so explicitly and shrink the APK (it grew from about 165 MB to 188 MB); not done.
- The host's container must not own the phone's singletons: services forwarded with a factory are disposed when the host stops, and the phone then fails with `ObjectDisposedException` (the library showed as empty on the second start). `BrowserServices.Share` registers the instances themselves, which a container never disposes.
- `ConsoleLifetime` registers POSIX signal handlers, which throw `PlatformNotSupportedException` on Android; the host uses `AppHostLifetime` instead.
- Minimal API handlers returning `IResult` through the endpoint builder answered with an empty 200 on Android. The handlers are plain `RequestDelegate`s that execute their result themselves (`BrowserHost.RunAsync`).
- Debug builds under another `-p:ApplicationId=` reuse the cached manifest of the previous id: build with `--no-incremental` (or delete `obj/.../android/AndroidManifest.xml`) when switching ids, and check `adb shell pm list packages`.

### Not done yet

- A foreground service / wake lock for long sessions with the screen off (the Wi-Fi transfer screen keeps the screen on while it is open, as in v1).
- Importing songs from the browser (the Import button is a no-op there; the v1 upload page at the first address still works). Cover replacement in the song editor does nothing in the browser.
- Live refresh: a browser tab shows what the database held when the page was opened.
- A layout for wide windows (the phone layout simply stretches).
- The phone-or-computer playback choice (#32).
- Verified on the phone by script (browser: login, library of 83 songs, playback with moving position, pause/resume, seek to near the end and auto-advance to the next track, a playlist created and filled in the browser appearing on the phone, `/settings` answering "Page not found", the Download page rendering, a second stop/start session). Not exercised: the player's own seek and previous/next controls from the UI, a real download from the browser, removing a song while it plays in the browser. The Windows build compiles but the host was never run there.
- Songs removed in the browser while the phone also queues them: the browser queue is separate from the phone's queue.
