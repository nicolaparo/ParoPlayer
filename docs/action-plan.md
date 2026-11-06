# Action plan


1. [x] Mockup published, feedback collected per screen (collection `feedback`).
2. [x] `maui-cli` skill written by hand (`.claude/skills/maui-cli/SKILL.md`). No skills installed via `maui ai`/`devflow`.
3. [~] Toolchain proof: Windows build OK, `maui-android` workload installed; Android build waits for the API 37 platform (user action).
4. [x] Scaffold: `global.json`, analyzers, `.editorconfig`, Core + Tests, `CodingRulesTests` (48 tests green). [ ] App project + Tailwind pipeline (`copy-vendor.mjs`).
5. [~] Core: models, SQLite repositories, smart-playlist engine, library/playlist services done. [x] File import into `.nomedia` library folder (`TrackImportService`, TagLibSharp reader). [ ] Queue.
6. [ ] Spike: native Media3 service playing a file with screen off, controlled from Blazor.
7. [~] UI from approved mockup: [x] Library, Playlists (list, detail, smart builder), Settings, Import. [ ] Now Playing, Download, Tag editor (see `docs/ui-implementation.md`).
8. [~] Downloads (YoutubeExplode + ffmpeg + TagLibSharp, queue, Wi-Fi only, foreground service): built and unit-tested, NOT tested on a device. See `docs/download.md`. [ ] Online metadata lookup, [ ] verify on phone.
9. [x] Song tag editor with online re-check (MusicBrainz + Cover Art Archive, iTunes fallback; `Core/Tagging`). Edits can be undone until the user taps Keep. Limitation: undo does not remove a cover that was newly embedded in a file that had none.
10. [ ] Android Auto browse tree + DHU test.
11. [ ] Wi-Fi transfer: server spike, approval flow, web UI, upload/download.

## Open questions

- Android 37 SDK platform must be installed by the user (`sdkmanager "platforms;android-37.0"`); .NET 11 targets only `net11.0-android` (API 37). Windows build already works.
- Mockup feedback pending (the mockup's "Import from device" button means import via file picker, not scanning).

## Commands

```powershell
dotnet test tests/ParoPlayer.Core.Tests
dotnet build src/ParoPlayer.App -f net11.0-android -c Debug -p:EmbedAssembliesIntoApk=true
```

