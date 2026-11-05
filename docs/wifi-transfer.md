# Wi-Fi transfer (computer <-> phone)


An activity that starts a temporary web server on the phone so a computer browser on the same Wi-Fi can upload songs (drag and drop) and download songs.

- Server runs only while the activity is open, inside a foreground service while transfers are active. Stops on leave/disconnect. Spike `HttpListener` first (works on Android .NET, no ASP.NET dependency); Kestrel is the alternative.
- **Security flow** (patterns seen in open-source Android transfer apps such as DropDroid and LocalShare; the exact design is ours): the browser requests access, the phone shows device name, IP and a 4-digit code that is also shown in the browser; the user taps **Allow**. Only then is a random 128-bit session token issued (HttpOnly cookie, `SameSite=Strict`). No token, no access to any route.
- One approved device at a time; session expires on idle, on leaving the screen and on **Disconnect**. Rate-limit the approval request; limit upload size and accept audio types only; sanitize file names (no path traversal), write into the library folder only.
- Plain HTTP on a LAN means traffic is unencrypted. The screen says so; optional later: TLS with a self-signed certificate.
- Web UI is a small static page served from `wwwroot/transfer` (offline, no CDN, list layout, no tables), mobile-friendly too.

