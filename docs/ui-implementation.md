# UI implementation notes

Screens built from the mockup: Library, Playlists (list, detail, smart builder), Settings. Logic lives in Core (`LibraryQuery`, `SmartPlaylistDraft`, `TrackImportService`) and is unit-tested; `.razor` files only bind.

- **Playback is not implemented.** Tapping a song only sets `PlayerState.Current` (shown in the accent colour). The equalizer animation comes with playback.
- **No offline badge** on rows: everything in the library is offline by definition.
- **Import:** `ImportButton` -> `IMusicImporter` (MAUI `IFilePicker`, audio, multiple) -> `TrackImportService` copies into the `.nomedia` library folder and reads tags with `TagLibMetadataReader` (falls back to the file name). Cover art is not extracted yet.
- **Tag editor:** `TrackEditor` sheet (Library and playlist song menu, Edit info). Fields, optional cover, Re-check online with a diff preview; Save/Undo/Keep.
- **Demo data:** Settings shows "Load demo songs" only in DEBUG builds (`DemoLibrary`, tracks without audio files).
- **Not done yet:** editing the rules of a saved smart playlist, deleting playlists (needs the Undo pattern), drag to reorder manual playlists (`PlaylistService.ReorderAsync` exists), Albums/Artists drill-down, Wi-Fi transfer entry in Settings.
- Not visually verified in a running app; only build and unit tests were run.
