# ParoPlayer

An offline-first music player for Android, built with **.NET MAUI Blazor Hybrid** and **Tailwind CSS**. It is a personal project, sideloaded as an APK, and the whole app was built together with an AI coding assistant ([Claude Code](https://claude.com/claude-code)).

This README explains two things:

1. [How to build and run the app](#build-and-run) with the `maui` CLI and `dotnet`.
2. [How the app was made with AI](#how-we-work-with-ai): the commands, the skills and the workflow.

## What the app does

- Plays local music, also with the screen off.
- Library, playlists and smart playlists (songs picked by rules instead of by hand).
- Downloads audio from YouTube (YoutubeExplode + ffmpeg) and fixes tags from MusicBrainz / iTunes (no account or token needed).
- Detects BPM and musical key, draws a waveform, edits tags.
- Imports from the phone, or from a computer over Wi-Fi.
- All data stays on the device (SQLite). Fonts and icons are bundled, no CDN.

Android is the product. Windows is only a quick preview target.

## Repository map

```
src/ParoPlayer.Core/          net11 library, no MAUI. Models, SQLite repositories, smart-playlist engine, services.
src/ParoPlayer.App/           MAUI Blazor Hybrid app (Android + Windows), Razor components, Tailwind.
tests/ParoPlayer.Core.Tests/  xUnit tests.
docs/                         all project notes (start at docs/README.md).
mockup/                       interactive HTML mockup made before the real UI.
.claude/skills/               hand-written skills for the AI assistant.
CLAUDE.md                     rules the AI assistant follows in this repo.
```

## Prerequisites

- **.NET 11 preview SDK.** `global.json` pins it. Without the pin, `dotnet` picks the newest stable SDK (10.x), which cannot build `net11.0` targets. Check with `dotnet --version`.
- **MAUI workload:** `dotnet workload restore` (or `dotnet workload install maui-android`).
- **Android SDK** with the API 37 platform, and a JDK (21).
- **Node.js**, only for the Tailwind and vendor asset build.
- **`maui` CLI** (see next section) and **`adb`**.
- A phone with Developer options on, or an Android emulator.

## The `maui` CLI in one minute

The `maui` CLI (package `Microsoft.Maui.Cli`, preview) is a helper for the environment around your app. It does **not** build or run the project: that is still `dotnet`.

```powershell
dotnet tool install -g Microsoft.Maui.Cli --prerelease    # install
dotnet tool update  -g Microsoft.Maui.Cli --prerelease    # update
```

Useful options on every command: `--json` (machine-readable), `--ci` (never ask questions), `--dry-run`, `-v`.

| Task | Command |
| --- | --- |
| Start an emulator | `maui android emulator start pixel_7_-_api_36_0 --wait --ci` |
| Check the Android setup | `maui doctor --platform android` |
| Forward a port to a device | `maui android port forward 9223 9223 --device emulator-5554` |
| Take a screenshot of the running app | `maui devflow ui screenshot --output shot.png --device emulator-5554` |
| Find running apps that DevFlow can drive | `maui devflow list --ci` |
| Check the DevFlow agent | `maui devflow agent status --device emulator-5554` |

Notes from experience:

- `maui doctor` and `maui device list` can hang. Wrap them in `timeout 60 ...` and use `adb devices -l` to list devices.
- `maui devflow` can inspect and screenshot the running app, which the AI uses to check the UI. Do not run `maui ai init/add`, `maui devflow init` or `maui devflow skills` here: they install skills, and this project keeps its own hand-written ones.

## Build and run

Everything below is run from the repo root.

```powershell
# 1. Tests (Core logic)
dotnet test tests/ParoPlayer.Core.Tests

# 2. CSS and vendored fonts/icons (Tailwind)
cd src/ParoPlayer.App; npm install; npm run build; cd ../..

# 3. Windows preview (fastest way to see UI changes)
dotnet build src/ParoPlayer.App -f net11.0-windows10.0.19041.0

# 4. Android debug APK (embeds the assemblies so it runs without a debugger)
dotnet build src/ParoPlayer.App -f net11.0-android -c Debug -p:EmbedAssembliesIntoApk=true
```

The APK ends up in `src/ParoPlayer.App/bin/Debug/net11.0-android/com.nicolaparo.paroplayer-Signed.apk`.

### Install on a phone with adb

Wireless debugging (once per PC, then reconnect when the port changes):

```powershell
# Phone: Settings > Developer options > Wireless debugging > Pair device with pairing code
adb pair <phone-ip>:<pairing-port> <6-digit-code>     # only the first time
adb connect <phone-ip>:<connect-port>                 # the port changes after Wi-Fi toggles or reboot
adb devices -l

adb -s <serial> install -r src/ParoPlayer.App/bin/Debug/net11.0-android/com.nicolaparo.paroplayer-Signed.apk
adb -s <serial> shell monkey -p com.nicolaparo.paroplayer 1     # launch
adb -s <serial> logcat --pid=$(adb -s <serial> shell pidof -s com.nicolaparo.paroplayer)
```

## How we work with AI

The app was built in conversation with Claude Code. The rules and the way of working live in files in the repo, so every new session starts with the same context.

### 1. Starting the project

The first prompt gave the AI a goal and a few constraints, not a design:

- a list of features (offline, library, playlists, smart playlists, download, tags, screen-off playback, Android Auto);
- the stack (Blazor Hybrid + Tailwind, `maui` CLI available);
- "do not install any skills", "make an interactive mockup before implementing", "write an action plan, then start".

From that, the AI ran the same commands you would run by hand:

```powershell
dotnet --list-sdks                     # found that 10.x was picked, so it pinned 11 in global.json
dotnet workload list
dotnet workload install maui-android --skip-manifest-update
dotnet new maui-blazor -n Check        # throwaway project to read the template; it emits net10, so TFMs were changed to net11.0
dotnet build -f net11.0-android        # first Android build
```

The real solution (`ParoPlayer.slnx`, Core library, App, tests) was then created by hand from that template. `net11.0-android` only supports API 37.

### 2. Files the AI reads

- **`CLAUDE.md`**: the project rules (SOLID/KISS/DRY, async everywhere, DI, naming, Tailwind class names written literally, no tables in the UI, offline only) and the working workflow. Read this first if you want to know how the AI behaves here.
- **`docs/`**: architecture, action plan, UI guidelines, download pipeline, Wi-Fi transfer, research notes.
- **`.claude/skills/`**: short, hand-written guides the AI loads when relevant:
  - `maui-cli`: the `maui` and `dotnet` commands for this project.
  - `adb`: wireless pairing, install, logcat, media session checks.
  - `android-ux`: Android quality checklist for UI and playback.
  - `github-issues`: how to create, label and close issues with `gh`.
  - `maui-devflow-*`: driving and debugging the running app.

### 3. The workflow: issue, code, commit and push, close

1. **Issue.** Every piece of work is a GitHub issue first (`gh issue create`). Big ideas are split into one issue per point.
2. **Code.** The AI implements it and runs `dotnet test` and the Windows build. When asked, it builds the APK and installs it on the phone.
3. **Commit and push.** Only when asked. The commit message links the issue (`Closes #N`, or `Part of #N`).
4. **Close the issue** after checking it on the device (`gh issue close N --reason completed --comment "..."`).

Typical prompts: "do issues 16 and 17, use agents", "install it on my phone", "commit and push", "the key shows up, close issue 31".

### 4. Agents and the advisor

For bigger batches the main session delegates:

- **Subagents** (Sonnet for real work, Haiku for small mechanical tasks) take one issue each, in their own git worktree, and commit on their own branch.
- **The advisor** (a stronger Opus model) is consulted before starting, when stuck, and before calling something done.
- The main session reviews each branch, merges them into `main`, runs all tests and builds on the result, pushes, and removes the worktrees.

Prompt example: "proceed on the other issues with multiple agents, consult the advisor, test all, merge and push".

### 5. Tips

- Keep rules in `CLAUDE.md` and knowledge in `docs/`, not in the chat. Chat is forgotten, files are not.
- Ask for a verifiable check (a test, a build, a screenshot) for every change.
- Review what agents report. A subagent saying "fixed" is not proof; run the tests and look on the device.
- Commit only when you ask for it.

## More

- [`docs/README.md`](docs/README.md): index of all notes.
- [`src/README.md`](src/README.md): what each project contains.
- [`CLAUDE.md`](CLAUDE.md): rules for the AI assistant.
