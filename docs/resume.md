# Resume notes (saved 2026-11-05, end of day)

Read this first when resuming. Then `CLAUDE.md`, `docs/action-plan.md`.

## Where we are
- Repo: private GitHub `nicolaparo/ParoPlayer`, branch `main`. Work saved as a WIP commit at end of day.
- Mockup (private artifact, feedback box per screen): https://claude.ai/artifact/WfGvVxMvLCzaXXb3EvJFRo , source `mockup/index.html`. **No feedback received from the user yet** (ask for it).
- Core library: models, SQLite repos, smart-playlist engine, import with TagLibSharp, library query, player queue. Tests green at last check (69 passing before the playback work; more added since).
- App: Library, Playlists (list/detail/smart builder), Settings, Import, bottom nav, Now Playing + mini player (added by the playback task, verify visually).
- Android build works (`net11.0-android`, API 37 installed). Windows build works.
- Native playback (Media3 service) was **in progress**: see `docs/playback-spike.md` for the exact state. Treat it as unverified until `dumpsys media_session` evidence shows PLAYING with screen off.
- Not yet installed on the phone. The user asked to install the current build on the phone once it builds.

## Devices
- Phone: an Android phone over wireless adb. Paired once (pairing survives). The connect port changes after reboot/Wi-Fi change: ask the user for the new port from Settings > Developer options > Wireless debugging, then `adb connect <phone-name>:<port>`.
- Emulator AVD `pixel_7_-_api_36_0`. adb path and commands: `.claude/skills/adb/SKILL.md`.

## Decisions already made (do not re-ask)
- .NET 11 preview, English UI, name ParoPlayer, personal use, sideloaded APK.
- Library only downloaded/imported files in private `library` folder with `.nomedia`.
- ffmpeg wanted: prebuilt arm64 CLI (hzw1199/Android-FFmpeg-Prebuilt) as `libffmpeg.so`, no MP3. Verify checksum and test on device.
- Font Awesome Free icons; no tables anywhere; Wi-Fi transfer feature with approval flow.
- No skills installed via `maui ai` / `devflow`; skills are hand-written (`maui-cli`, `adb`).

## Next steps
1. Check `git status`, build (`dotnet build src/ParoPlayer.App -f net11.0-android ...` see adb skill) and tests.
2. Finish/verify native playback on the emulator, then install on the phone (`adb install -r`, ask before any uninstall).
3. Ask the user for mockup feedback; adjust mockup/UI.
4. Download screen (YoutubeExplode + ffmpeg + TagLibSharp), Now Playing polish (vinyl, waveform), Discogs tag editor, Wi-Fi transfer, Android Auto browse tree + DHU.
5. Known UI gaps: no delete/edit/reorder for playlists, no drill-down in Albums/Artists/Genres/Folders tabs, no cover art extraction.
