---
name: maui-cli
description: Use when building, running, debugging or deploying the .NET MAUI Blazor Hybrid app (ParoPlayer), managing the Android SDK/emulator/devices, or driving the running app with maui devflow UI automation.
---

# maui CLI + dotnet workflow (MAUI Blazor Hybrid)

Tool: `maui` (Microsoft.Maui.Cli, 0.1.0-preview.13) (installed as a dotnet global tool).
Install/update: `dotnet tool install -g Microsoft.Maui.Cli --prerelease` / `dotnet tool update -g Microsoft.Maui.Cli --prerelease` (or `maui update --self`).
Docs: https://learn.microsoft.com/dotnet/maui/developer-tools/cli/ (officially "experimental, will change between releases").

Important split: `maui` does NOT build or run the app. Build/run/deploy = `dotnet`. `maui` = environment, Android SDK/emulators, ports, profiling, devflow automation.

Global options on every command: `--json`, `-v/--verbose`, `--dry-run`, `--ci` (non-interactive). Use `--json` for parsing. In bash, strip colors: `| sed 's/\x1b\[[0-9;]*m//g'`.

## 1. Project and build (plain dotnet)

```bash
dotnet new maui-blazor -n ParoPlayer        # MAUI Blazor Hybrid template (needs maui workload)
dotnet workload list
dotnet workload install maui                # or: dotnet workload restore  (in project dir)
dotnet workload update
dotnet restore
dotnet build -f net11.0-android             # Debug build
dotnet build -f net11.0-android -c Release
dotnet build -f net11.0-android -t:Run      # build + install + launch on the connected device/emulator
dotnet publish -f net11.0-android -c Release   # APK/AAB per csproj settings
```
Selecting one device when several are online: `-p:AdbTarget="-s emulator-5554"` (unverified; standard Android targets property).
Windows target (if listed in TargetFrameworks): `dotnet build -f net11.0-windows10.0.19041.0 -t:Run` (unverified).
TFM `net11.0-*` requires the .NET 11 SDK, and the csproj must list it in `<TargetFrameworks>`.

### Pin the SDK (global.json)
SDKs seen on this machine: 7.0.101, 7.0.120, 8.0.126, 10.0.401, 11.0.100-rc.3.26522.148. Unpinned, `dotnet` picks the highest stable (10.0.401), which cannot build net11.0 targets. Put in repo root `global.json`:

```json
{
  "sdk": {
    "version": "11.0.100-rc.3.26522.148",
    "rollForward": "latestFeature",
    "allowPrerelease": true
  }
}
```
Verify with `dotnet --version` in the project dir. After changing SDK run `dotnet workload restore` / `dotnet workload update`. `maui project version show|list|set|use-workload` manages the MAUI NuGet version pinned by the project (section 3).

## 2. Android: adb, install, logs

```bash
adb devices -l
adb -s <serial> install -r path/to/app.apk
adb shell pm list packages | grep -i paro
adb shell monkey -p <package> 1                   # launch without knowing the activity
adb shell am force-stop <package>
adb uninstall <package>
adb logcat -c                                     # clear
adb logcat -s DOTNET:V mono-stdout:V AndroidRuntime:E chromium:V   # typical MAUI/Blazor tags
adb logcat --pid=$(adb shell pidof -s <package>)  # everything from one app
adb logcat -d > logcat.txt                        # dump and exit
adb forward --list ; adb reverse --list
```
`adb logcat` without `-d` never exits: use `-d`, or `timeout`/`head`.
adb lives in `<ANDROID_HOME>\platform-tools`; if not on PATH, `maui android sdk check` should show the SDK root (output not verified).

## 3. maui CLI reference (from `--help`; not executed unless noted)

### Environment
```bash
maui version                               # also: maui --version
timeout 60 maui doctor [--platform dotnet|android|windows] [--fix] [--json]   # HANGS without timeout
maui update --self [--prerelease]
maui project version show   [-p App.csproj]
maui project version list   [-p App.csproj]
maui project version set <ver> [-p App.csproj]
maui project version use-workload [-p App.csproj]   # drop pinned version, use installed workload
```

### Android (`maui android`; all accept --sdk <path> and --jdk <path> overrides)
```bash
maui android sdk check | list | update-tools | accept-licenses(interactive)
maui android sdk install "platform-tools" "platforms;android-36" "system-images;android-36;google_apis;x86_64"
maui android sdk uninstall <pkg>
maui android jdk check | list | install
maui android install --accept-licenses [--sdk-install-path P] [--jdk-path P] [--jdk-version 17|21] [--packages a,b]   # full env setup
maui android emulator list
maui android emulator create <name> [--package <system-image>] [--device <def>] [--force]
maui android emulator start <name> [--wait] [--cold-boot]
maui android emulator stop <name>
maui android emulator delete <name>
maui android port list | clear                     # [-s <serial>]
maui android port forward <hostPort> [<devicePort>]   # adb forward; --agent-port = DevFlow shortcut
maui android port reverse <devicePort> <hostPort>     # adb reverse; default device port = broker 19223
```
Use `--dry-run` first for anything that installs or deletes.

### Devices and profiling
```bash
timeout 60 maui device list --json         # HANGS without timeout; fallback: adb devices -l
maui profile startup --help                # startup trace via dotnet-trace (args not verified)
maui profile manual  --help                # launch, Enter to attach, Enter to stop
maui port --help                           # "Diagnose TCP port usage" (args not verified)
maui apple ...                             # macOS only (xcode, simulator, runtime); irrelevant on Windows
```

### Do NOT run casually (install skills/MCP config or scaffold)
`maui ai ...`, `maui devflow init`, `maui devflow skills ...`, `maui go`. Only on explicit user request.

## 4. DevFlow (UI automation of the running app)

Needs the DevFlow agent embedded in the app (Microsoft.Maui.DevFlow package registered in `MauiProgram`, Debug only). Whether ParoPlayer has it is unverified; check the csproj/MauiProgram. Agent default port 9223, broker 19223. On Android, forward ports first.

```bash
maui android port forward 9223 9223        # or: maui android port forward --agent-port
maui devflow diagnose                      # broker, agents, project integration
maui devflow list                          # connected agents
maui devflow wait -t 60 [--wait-platform Android]
maui devflow agent status
maui devflow commands                      # machine-readable schema of all commands
```
Common options: `-p/--platform android|windows|ios|maccatalyst`, `--device <serial>`, `-ap <agentPort>`, `-ah <host>`, `--json`/`--no-json`.

Native UI (`maui devflow ui ...`): `tree [--depth N --format compact --fields id,type,text,automationId,bounds]`, `query`, `element`, `tap`, `fill`, `clear`, `focus`, `property`, `set-property`, `assert`, `scroll`, `gesture` (tap/swipe/pinch/longpress/...), `hit-test`, `alert`, `navigate <shellRoute>`, `resize`, `status`, `diagnostics` (layout/clipping/truncation), `screenshot --output shot.png [--overwrite --scale native --max-width N]`. Positional args: `maui devflow ui <cmd> --help`.

Other groups:
```bash
maui devflow logs [--limit N] [--source native|webview|all] [-f --replay 50]   # -f streams until Ctrl+C
maui devflow network list [--host H --method GET --limit N] | detail <id> | clear
maui devflow storage preferences|secure-storage|files|roots ...
maui devflow recording start|status|stop [--device <serial>]
maui devflow device app-info|device-info|display|battery|connectivity|permissions|sensors|fold
maui devflow batch                         # JSONL commands on stdin, JSONL results on stdout
maui devflow broker ...                    # broker daemon (subcommands not inspected)
maui devflow mcp                           # MCP stdio server; only for an MCP client config
```

### Blazor WebView debugging via CDP (`maui devflow webview`)
Pick a WebView with `-w <index|automationId>` (default: first).
```bash
maui devflow webview webviews              # list WebViews
maui devflow webview status                # CDP connection
maui devflow webview snapshot              # simplified DOM with element refs
maui devflow webview source                # page HTML
maui devflow webview DOM querySelector "<css>"      # also querySelectorAll, getDocument, getOuterHTML
maui devflow webview Input dispatchClickEvent "<css>"
maui devflow webview Input fill "<css>" "text"      # also insertText
maui devflow webview Runtime evaluate "document.title"
maui devflow webview Page captureScreenshot | reload | navigate <url>
```
Argument order for each is unverified; confirm with `--help`. JS console output also shows in `maui devflow logs --source webview`.
Fallback without devflow: call `builder.Services.AddBlazorWebViewDeveloperTools()` in Debug, then open `chrome://inspect` in desktop Chrome with the device attached via adb.

## 5. Android Auto (DHU) testing pointers
Unverified against this project; standard flow:
1. Install "Android Auto Desktop Head Unit Emulator" via SDK Manager (`maui android sdk list`, then `sdk install` the `extras;google;auto` package).
2. In the Android Auto app on the phone/emulator: tap version 10 times, Developer settings, "Start head unit server".
3. `adb forward tcp:5277 tcp:5277` (or `maui android port forward 5277 5277`).
4. Run `<ANDROID_HOME>\extras\google\auto\desktop-head-unit.exe` (`--usb` for USB mode).
5. Enable "Unknown sources" in developer settings so debug builds of media apps appear. A media app needs a MediaBrowserService + MediaSession and the `com.google.android.gms.car.application` meta-data (automotive_app_desc.xml with `<uses name="media"/>`) in the manifest.

## Gotchas
- Hangs: `maui doctor` and `maui device list` (always `timeout 60`); also `devflow logs -f`, `adb logcat` without `-d`, `maui profile manual` (waits for Enter).
- Experimental, preview.13: flags and subcommands change between releases; re-run `--help` before trusting syntax here. The online docs list `maui devflow cdp` and `devflow MAUI tree`, but the installed build uses `devflow webview` and `devflow ui tree`.
- SDK pinning: the highest stable SDK (10.0.401) cannot target net11.0; pin 11.0.100-rc.3 in global.json with `allowPrerelease`. Workload mismatch shows as NETSDK1147 / missing manifest errors: `dotnet workload restore`.
- Interactive prompts (`sdk accept-licenses`, omitted args) block non-TTY runs: pass args and use `--ci`.
- `maui ai *`, `devflow init`, `devflow skills *`, `go` change config or install skills/MCP; do not run without user consent.
- `devflow list` showing no agents usually means the app is not running in Debug with the agent, or the adb forward is missing.
- `emulator start --wait` blocks until boot; run it in the background. `dotnet build -t:Run` fails with no online device; check `adb devices` first.
- Emulator media/background audio and Android Auto behave differently from real hardware; verify media-session features on a device where possible.
