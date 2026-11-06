# Download

Paste a video or playlist link (or type a search phrase) on the Download tab. Audio is downloaded with YoutubeExplode, converted with ffmpeg, tagged with TagLibSharp and imported into the library through the same `TrackImportService` as a picked file. Personal use only; the YouTube terms apply to the user.

## Flow

`DownloadRequests.SubmitAsync(text)`: a link is resolved (video, or playlist up to 100 items) and every item is queued; anything else is a search (top 10) and the user taps the ones to download. Then `DownloadQueue` runs one item at a time through `DownloadPipeline`:

1. `IMediaSource.DownloadAudioAsync` saves the best audio stream into a temp work folder (never inside `library`). It picks the highest-bitrate audio stream whatever the codec (usually Opus webm, 130-160 kbps, beats AAC which YouTube caps near 128 kbps) and logs itag, codec and bitrate via `Trace`. AAC is copied by ffmpeg; Opus is re-encoded to AAC 256k so the quality loss stays small and the library keeps one format (the bundled ffmpeg is not known to have the ogg muxer).
2. `IAudioConverter`: with ffmpeg, the output is always `.m4a` (`-vn -map 0:a:0 -c:a copy` for AAC, `-c:a aac -b:a 256k` otherwise, `+faststart`). If conversion fails, the original file is kept and the item shows "Saved without conversion: ...".
3. `ITrackTagWriter` (TagLibSharp) writes title, artist and the YouTube thumbnail as cover. A tagging failure never fails the item.
4. `TrackImportService.ImportAsync("Artist - Title.m4a", stream)` copies into the `.nomedia` library folder and creates the `Track`. The work folder is deleted on success, failure and cancel.

Artist and title come from `TitleParser`: "Artist - Title (Official Video)" is split and noise in brackets removed, otherwise the channel name (minus " - Topic" / "VEVO") is the artist.

Queue: per-item cancel (queued or running), retry for failed or canceled, "Clear finished". Duplicates (same video already queued or running) are ignored. Failures do not stop the queue. Progress is 0 to 1 over all stages (download is 85%).

## Wi-Fi only and offline

`ConnectivityDownloadPolicy` (App) feeds `NetworkRules`: offline gives "No internet connection."; with Settings > "Download on Wi-Fi only" on (default), any connection that is not Wi-Fi or Ethernet is blocked. The policy is checked before resolving and again before each item (the item then fails with the reason, and Retry works after connecting).

## Background

`IDownloadKeepAlive` starts `DownloadService` (Android foreground service, type `dataSync`, permission `FOREGROUND_SERVICE_DATA_SYNC`) when the queue starts working. It shows a low-importance notification with progress and stops itself when nothing is queued or running (the service decides, so there is no start/stop race). The page asks for the notification permission (Android 13+) before the first download; downloading works without it, only the notification is hidden.

## ffmpeg

- Source: [hzw1199/Android-FFmpeg-Prebuilt](https://github.com/hzw1199/Android-FFmpeg-Prebuilt), repo commit `90231cc0105aef4f76926b911535f5eb73511b86` (main when downloaded), file `ffmpeg-8.1.1/bin/ffmpeg` (LGPL build, `--disable-gpl --disable-nonfree`, no MP3 encoder).
- Stored as `src/ParoPlayer.App/Platforms/Android/lib/arm64-v8a/libffmpeg.so` (15,520,320 bytes) and packaged with `<AndroidNativeLibrary>`. Android extracts it to `ApplicationInfo.NativeLibraryDir` (`extractNativeLibs="true"` is set), the only place an app may execute from; `AndroidFfmpegLocator` returns that path when the file exists.
- **sha256** `480defd776199b5434008565974de5bf99822371c3cd9c1f130fda2c57de6439`. The git blob SHA-1 `3f53593b836270d151fee4945f61e419724a7a89` computed locally equals the SHA GitHub reports for that file in its API, so the download is intact. The upstream publishes no signed checksum, so this proves integrity, not authorship.
- Checked on the file: ELF, AArch64, `e_type` 3 (PIE), all `PT_LOAD` aligned 0x4000 (16 KB pages), links only system libraries (libc, libm, libdl, libandroid, libz, libmediandk, ...).
- Checked in the built Debug APK: `lib/arm64-v8a/libffmpeg.so` is present and its sha256 equals the value above.
- The file is not tracked by git yet (15 MB). On a fresh clone without it the build still works and the app silently falls back to no conversion. Fetch it with `curl -L -o src/ParoPlayer.App/Platforms/Android/lib/arm64-v8a/libffmpeg.so https://raw.githubusercontent.com/hzw1199/Android-FFmpeg-Prebuilt/90231cc0105aef4f76926b911535f5eb73511b86/ffmpeg-8.1.1/bin/ffmpeg` and compare the sha256.
- **Not verified on a device** (no adb in this session): that Android lets the app execute it, and that the `aac` encoder and `mov` muxer are present. If starting fails, `FfmpegAudioConverter` throws `AudioConversionException` and the pipeline keeps the downloaded file (note shown on the finished row). To check on the phone: download a song; a row that says "Saved without conversion" means ffmpeg did not run.
- Smoke run on Windows (throwaway console app, real network, not part of the tests): a real video link was resolved, downloaded, converted by the winget ffmpeg and imported, with and without ffmpeg. Result: `.m4a` (ffmpeg) or `.mp4` (pass-through), title and artist tags read back correctly, 19 s duration, cover embedded as `image/jpeg`; text search returned results. The merged Android manifest has `extractNativeLibs="true"`.
- x86_64 emulators and Windows have no ffmpeg: a missing locator path makes `FfmpegAudioConverter` delegate to `PassThroughAudioConverter` (the AAC stream is saved as `.mp4`, which TagLib and Media3 handle).

## Code map

Core (`src/ParoPlayer.Core/Download`): `IMediaSource` + `YoutubeMediaSource` (YoutubeExplode adapter, the only untested part because it needs the network), `AudioStreamPicker`, `IAudioConverter` (`PassThroughAudioConverter`, `FfmpegAudioConverter`, `FfmpegArguments`, `IProcessRunner`/`SystemProcessRunner`, `IFfmpegLocator`), `ITrackTagWriter`/`TagLibTrackTagWriter`, `DownloadPipeline`, `DownloadQueue`, `DownloadRequests`, `DownloadInput`, `TitleParser`, `NetworkRules`, `IDownloadPolicy`, `IDownloadKeepAlive`. Registered in `AddParoCore`; the app overrides policy, keep-alive and locator.

App: `Components/Pages/Download.razor`, `Components/Shared/DownloadRow.razor`, `Services/ConnectivityDownloadPolicy.cs`, `Services/NotificationPermission.cs`, `Platforms/Android/Download/*` (`DownloadService`, `AndroidDownloadKeepAlive`, `AndroidFfmpegLocator`).

## Left out (first version)

- Online metadata lookup (MusicBrainz + Cover Art Archive, iTunes fallback): not built. Tags come only from the video title and channel.
- Cover is the YouTube thumbnail as is (16:9 frame, not cropped to a square). With ffmpeg available, cropping could be added to the conversion.
- The queue is in memory: it is lost when the process dies. No resume of partial downloads.
- No FLAC/Opus option, no bitrate setting, no MP3.
- The foreground service is not started when a retry happens while the app is in the background (Android 12+ forbids it); downloading continues while the process lives.
- The Library list does not refresh while open; it shows new songs the next time it is opened.
- The input form is at the top of the page (not the bottom half as in the UI guidelines), because a bottom input would sit under the keyboard with edge-to-edge on Android 15+. It scrolls away on a long queue.
- Not tested on a device: the real YouTube download, the foreground service and notification, ffmpeg execution, the screen layout.
- YoutubeExplode scrapes YouTube, so it can break when YouTube changes; update the NuGet package then.
