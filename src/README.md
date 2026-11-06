# src

Source code of ParoPlayer, an offline-first Android music player (.NET 11 MAUI Blazor Hybrid). Notes and decisions are in [`../docs`](../docs/README.md).

## Projects

- **`ParoPlayer.Core`** (net11.0, no MAUI or UI dependency): models (`Track`, `Playlist`, `SmartPlaylistDefinition`), `IRepository<T>` with SQLite and in-memory implementations, `SmartPlaylistEngine`, `LibraryService`, `PlaylistService`, `ILibraryFolder` (private `library` folder with `.nomedia`), `TrackImportService` + `ITrackMetadataReader` (TagLibSharp, file-name fallback), `Navigation/BackStack` (what Android Back closes first: sheets, then selection), `LibraryQuery` (search, grouping, formatting), `SmartPlaylistDraft` (builder state), `Download/` (`DownloadQueue`, `DownloadPipeline`, `DownloadRequests`, YoutubeExplode source, ffmpeg converter, tag writer, see `../docs/download.md`). Registered with `AddParoCore(dbPath)`.
- **`ParoPlayer.App`** (MAUI Blazor Hybrid, `net11.0-android` and `net11.0-windows10.0.19041.0`): Razor components, Tailwind styles (`Styles/app.css` generates `wwwroot/css/app.css`), Font Awesome and fonts copied locally by `scripts/copy-vendor.mjs`, `Platforms/Android` for native services. `Components/Pages` (Library, Playlists, PlaylistDetail, SmartPlaylistBuilder, Download, Settings), `Components/Shared` (SongRow, ArtTile, PageHeader, ToggleRow, ImportButton, DownloadRow), `Services` (PlayerState, AppSettings, IMusicImporter, DemoLibrary, CoverImages); `Shared/TrackEditor` edits song info, `Shared/BulkTrackEditor` edits artist, album, genre and year on the selected songs; Core `Tagging/` holds TrackEditService and the MusicBrainz/iTunes lookup.

Tests are in `../tests/ParoPlayer.Core.Tests` (xUnit, includes `CodingRulesTests`).

## Build

```powershell
dotnet test tests/ParoPlayer.Core.Tests
dotnet build src/ParoPlayer.App -f net11.0-windows10.0.19041.0      # Windows preview
dotnet build src/ParoPlayer.App -f net11.0-android -c Debug         # needs the Android API 37 platform
cd src/ParoPlayer.App; npm install; npm run build                   # CSS and vendor assets
```

## Rules

No `_`-prefixed members, async everywhere, DI for services, literal Tailwind class names, no `<table>` in UI. See `../CLAUDE.md`.
