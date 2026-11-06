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
