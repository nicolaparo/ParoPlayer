---
name: adb
description: Use when installing, running, debugging or inspecting ParoPlayer on an Android phone or emulator with adb (wireless pairing, install/uninstall, logcat, dumpsys media_session/notification, run-as file access, screen off tests, Android Auto DHU forwarding).
---

# adb for ParoPlayer

Package: `com.nicolaparo.paroplayer`. If adb is not on PATH, use the full path.

```powershell
$adb = "adb"   # or the full path to adb.exe inside platform-tools
```

Android SDK: `C:\Program Files (x86)\Android\android-sdk` (emulator in `emulator\`, `cmdline-tools\latest\bin\sdkmanager.bat`). JDK: `C:\Program Files\Android\openjdk\jdk-21.0.8`. `ANDROID_HOME`/`JAVA_HOME` are not set. Do not use `maui doctor` or `maui device list` (they hang); use `adb devices -l`.

Do not install skills via `maui ai` or `maui devflow init`. This file is hand-written.

## Targets

Always pick the device explicitly when more than one is attached (`-s <serial>`), otherwise adb fails with "more than one device".

```powershell
& $adb devices -l
& $adb -s <phone-name>:<port> shell getprop ro.build.version.release
```

Known targets: phone `<phone-name>:<connect-port>` (over Wi-Fi) and emulator `emulator-5554` (AVD `pixel_7_-_api_36_0`).

## Wireless debugging (Android 11+)

Verified on a physical Android phone.

1. Phone: Settings > Developer options > Wireless debugging on, then "Pair device with pairing code". It shows `host:pairing-port` and a 6-digit code.
2. `& $adb pair <host>:<pairing-port> <code>` (once per PC; the code is single-use).
3. The main Wireless debugging screen shows a different **connect port**. `& $adb connect <host>:<connect-port>`.
4. The connect port changes when Wi-Fi is toggled or the phone reboots: ask the user for the new port and run `adb connect` again. No re-pairing needed. `adb mdns services` did not list the phone here, so do not rely on discovery.
5. Disconnect: `& $adb disconnect <host>:<port>`.

Never pair or connect with a code or port the user did not give for this purpose.

## Emulator

```powershell
Start-Process "C:\Program Files (x86)\Android\android-sdk\emulator\emulator.exe" -ArgumentList "-avd","pixel_7_-_api_36_0","-no-snapshot"
& $adb -s emulator-5554 wait-for-device
& $adb -s emulator-5554 shell getprop sys.boot_completed   # prints 1 when ready
```

## Build and install

Build (works, API 37 platform installed):

```powershell
dotnet build src/ParoPlayer.App -f net11.0-android -c Debug "-p:AndroidSdkDirectory=C:\Program Files (x86)\Android\android-sdk" "-p:JavaSdkDirectory=C:\Program Files\Android\openjdk\jdk-21.0.8" -p:EmbedAssembliesIntoApk=true
```

`EmbedAssembliesIntoApk=true` is needed for an APK that runs standalone when installed with plain `adb install` (without it, Debug uses fast deployment, which `dotnet build -t:Run/Install` handles but a copied APK does not). Output: `src/ParoPlayer.App/bin/Debug/net11.0-android/com.nicolaparo.paroplayer-Signed.apk`.

```powershell
$apk = "src\ParoPlayer.App\bin\Debug\net11.0-android\com.nicolaparo.paroplayer-Signed.apk"
& $adb -s <serial> install -r $apk                       # update, keeps app data
& $adb -s <serial> shell monkey -p com.nicolaparo.paroplayer -c android.intent.category.LAUNCHER 1   # launch
& $adb -s <serial> shell am force-stop com.nicolaparo.paroplayer
& $adb -s <serial> uninstall com.nicolaparo.paroplayer   # DELETES library and database; ask the user first
```

A signature mismatch ("INSTALL_FAILED_UPDATE_INCOMPATIBLE") means the APK was signed with a different key. Uninstalling fixes it but erases the app's data. Ask first.

## Logs

```powershell
& $adb -s <serial> logcat -c
& $adb -s <serial> logcat -d -s DOTNET MonoDroid AndroidRuntime ParoPlayer *:E   # -d dumps and exits
& $adb -s <serial> logcat -d --pid=$(& $adb -s <serial> shell pidof com.nicolaparo.paroplayer)
```

Never run `logcat` without `-d` or a timeout from the agent: it never exits.

## Playback and background checks

```powershell
& $adb -s <serial> shell dumpsys media_session | Select-String -Pattern "paroplayer|state=PlaybackState" -Context 0,3
& $adb -s <serial> shell dumpsys audio | Select-String -Pattern "paroplayer|AudioPlaybackConfiguration"
& $adb -s <serial> shell dumpsys notification --noredact | Select-String -Pattern "paroplayer" -Context 0,6
& $adb -s <serial> shell dumpsys activity services com.nicolaparo.paroplayer
& $adb -s <serial> shell input keyevent 26     # power: toggles screen (check `dumpsys power | findstr mWakefulness`)
& $adb -s <serial> shell input keyevent 85     # media play/pause
& $adb -s <serial> shell input keyevent 87     # media next
& $adb -s <serial> shell dumpsys deviceidle      # Doze state
```

Screen-off test: start playback, `keyevent 26`, wait 30+ s, re-run the `media_session` check and confirm state is `PLAYING` (3). Some vendors (some OEM skins) kill background apps: also check battery optimisation for the app (Settings > Battery > unrestricted) before blaming the code.

## Files (library, database)

The app is debuggable, so `run-as` works without root.

```powershell
& $adb -s <serial> shell run-as com.nicolaparo.paroplayer ls -la files/library
& $adb -s <serial> shell run-as com.nicolaparo.paroplayer ls -la files/library/.nomedia
& $adb -s <serial> exec-out run-as com.nicolaparo.paroplayer cat files/paroplayer.db3 > paroplayer.db3
```

Paths under `FileSystem.AppDataDirectory` are `/data/data/com.nicolaparo.paroplayer/files/` (verify with `run-as ... pwd` if unsure). Pushing a file into the app dir: `adb push x.mp3 /sdcard/Download/` then `run-as ... cp /sdcard/Download/x.mp3 files/library/` (may be blocked by scoped storage; if so use the in-app Import).

Native libs (ffmpeg spike): `run-as com.nicolaparo.paroplayer ls -la ../lib/arm64` or `dumpsys package com.nicolaparo.paroplayer | findstr nativeLibraryDir`.

## UI and screens

```powershell
& $adb -s <serial> exec-out screencap -p > screen.png
& $adb -s <serial> shell wm size ; & $adb -s <serial> shell wm density
& $adb -s <serial> shell input tap <x> <y>
```

For Blazor WebView DOM inspection, use `chrome://inspect` on the PC (WebView is debuggable in Debug builds) or the `maui devflow` commands described in the `maui-cli` skill.

## Android Auto (Desktop Head Unit)

Unverified here; see `docs/research.md`. Outline: install the DHU via sdkmanager (`extras;google;auto`), phone: Android Auto developer settings > Start head unit server, `adb -s <serial> forward tcp:5277 tcp:5277`, run `desktop-head-unit.exe`. Over wireless adb the forward still works.

## Gotchas

- More than one device attached: always `-s`.
- Wireless connect port changes; pairing code is single-use.
- `uninstall` destroys the library and database.
- `logcat` without `-d` blocks forever.
- PowerShell: quote args containing `:` or `;` such as `"-p:AndroidSdkDirectory=..."`.
