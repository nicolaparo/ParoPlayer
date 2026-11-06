# Research notes (2026-11)

Findings gathered while planning. Unverified items are marked.

## Playback and Android Auto (Media3)
- `Xamarin.AndroidX.Media3.ExoPlayer`, `.Session`, `.Common` 1.11.1 exist (listed targets: net10.0-android36.0; no net11 target listed, verify restore).
- `Session` depends on `Xamarin.AndroidX.Concurrent.Futures`, not Guava. How to expose/build a `ListenableFuture` in C# is unconfirmed: main prototype risk.
- No evidence that Plugin.Maui.Audio, MediaManager or Shiny support Android Auto browse (absence of results, not proof).
- Manifest: [car media configure-manifest](https://developer.android.com/training/cars/media/configure-manifest), [Android for Cars media](https://developer.android.com/training/cars/media/auto), [Media3 serve content](https://developer.android.com/media/media3/session/serve-content).
- Fallback: legacy `MediaBrowserServiceCompat` (Xamarin.AndroidX.Media).

## ffmpeg
- ffmpeg-kit is retired; community forks are Java AARs, no proven .NET binding.
- Chosen: prebuilt CLI binary [hzw1199/Android-FFmpeg-Prebuilt](https://github.com/hzw1199/Android-FFmpeg-Prebuilt) (arm64, NDK r28, minSdk 28, FFmpeg 8.1.1/8.0.1/9.0, `--disable-gpl --disable-nonfree --enable-small`, ~15 MB, 16 KB page size). Renamed `libffmpeg.so`, executed from `ApplicationInfo.NativeLibraryDir`. No mp3lame confirmed, so no MP3. Verify on device: `ffmpeg -encoders`, ELF alignment (`readelf -lW`), exec from nativeLibraryDir.
- Alternatives: self-built ffmpeg with libmp3lame (WSL), ffmpegkit-maintained AARs (JNI only, no CLI), MediaCodec/MediaMuxer (AAC only, no tags).
- Command lines (untested): m4a `-i in -i cover.jpg -map 0:a -map 1 -c:a aac -b:a 192k -c:v mjpeg -disposition:v:0 attached_pic out.m4a`; flac similar with `-c:a flac`; opus `-c:a libopus` or `-c:a opus -strict -2` (cover art in opus unclear).
- `YoutubeExplode.Converter` needs an external CLI; use `SetFFmpegPath`.

## Online metadata (decision: no Discogs)
- Discogs needed a personal token, so it was dropped. Used instead, all without authentication: MusicBrainz `GET /ws/2/recording?query=...&fmt=json` (1 req/s, descriptive User-Agent), Cover Art Archive `GET /release/{mbid}/front-500`, iTunes `GET /search?term=...&entity=song`.

## Toolchain (this machine)
- SDK `11.0.100-rc.3.26522.148` pinned in `global.json`; unpinned `dotnet` picks 10.0.401 which cannot target net11.0.
- `dotnet new maui-blazor` emits net10 TFMs; use `net11.0-android` (API 37 only) and `net11.0-windows10.0.19041.0`. MAUI resolves to `11.0.0-preview.6.26360.8`.
- `maui-android` workload installed (first attempt failed with a pack download error, second succeeded, no admin needed).
- Android SDK at `C:\Program Files (x86)\Android\android-sdk` (has API 36 only; **API 37 platform must be installed**), JDK `C:\Program Files\Android\openjdk\jdk-21.0.8`. `ANDROID_HOME`/`JAVA_HOME` unset: pass `-p:AndroidSdkDirectory=... -p:JavaSdkDirectory=...`.
- `maui doctor` / `maui device list` hang: use `adb devices`.

## UI inspiration
Plexamp (art-derived colour, visualizers), Poweramp, Material 3 Expressive (dynamic colour, expressive shapes, motion). Vinyl and waveform seek bar are our own ideas.
