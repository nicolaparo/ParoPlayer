# ParoPlayer

Offline-first music player for Android. .NET MAUI **Blazor Hybrid** (Tailwind CSS), English UI, personal use, sideloaded APK. Android is the product; Windows is only a dev/preview target. Local-only data in SQLite. App id `com.nicolaparo.paroplayer`. Target **.NET 11 preview** (SDK pinned in `global.json`).

**All notes live in `docs/`** (start at `docs/README.md`). Code overview in `src/README.md`. Keep notes there, not here.

## Working principles (Karpathy guidelines)

1. **Think before coding.** State assumptions, surface trade-offs, ask when unclear.
2. **Simplicity first.** Minimum code that solves the problem. Nothing speculative.
3. **Surgical changes.** Every changed line traces to the request.
4. **Goal-driven.** Define a verifiable check (usually a test) and loop until it passes.

## Engineering rules

- **SOLID, KISS, DRY.** Small single-purpose classes; abstractions only at real boundaries.
- **C# latest.** Primary constructors, collection expressions, records, pattern matching, file-scoped namespaces.
- **async/await everywhere.** Never `.Result`, `.Wait()`, `.GetAwaiter().GetResult()`, fire-and-forget or `async void`. Enforced by `Microsoft.VisualStudio.Threading.Analyzers` and `CodingRulesTests`.
- **DI everywhere.** Register in `MauiProgram` / `AddParoCore`; no `new` of services, no service locators.
- **Naming.** Never prefix members with `_`; private fields are `camelCase`.
- **Tailwind class names must be literal strings** in Razor.
- **No tables** in any UI (`<table>` is forbidden, see `docs/ui-guidelines.md`).
- Everything works offline: fonts and icons (Font Awesome Free) are copied into `wwwroot`, no CDN.
- Commit only when asked.

## Layout

```
src/ParoPlayer.Core/    net11 library, no MAUI. Models, repositories, smart-playlist engine, services.
src/ParoPlayer.App/     MAUI Blazor Hybrid app (Android + Windows).
tests/ParoPlayer.Core.Tests/   xUnit.
docs/                   all notes.   mockup/  interactive HTML mockup.
```

## Commands

```powershell
dotnet test tests/ParoPlayer.Core.Tests
dotnet build src/ParoPlayer.App -f net11.0-windows10.0.19041.0
dotnet build src/ParoPlayer.App -f net11.0-android -c Debug -p:EmbedAssembliesIntoApk=true   # needs Android API 37 platform
```

## Gotchas

- `maui doctor` and `maui device list` hang; use timeouts or `adb devices`.
- Pin the SDK in `global.json`; unpinned `dotnet` picks 10.0.401, which cannot target net11.0.
- Never run `maui ai init/add`, `maui devflow init` or `maui devflow skills`: the user does not want skills installed.
