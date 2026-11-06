---
name: android-ux
description: Use when designing, reviewing or changing ParoPlayer UI or playback behaviour (Razor pages, sheets, navigation, touch, theming, animations, notification, audio focus). Checklist of Android DOs and DONTs taken from Google's core app quality guidelines, plus project conventions.
---

# Android UX and quality checklist (ParoPlayer)

Sources: Google "Core app quality guidelines" (developer.android.com/docs/quality-guidelines/core-app-quality, checklist IDs in brackets), "System bars" design guide, "Media controls" docs. Items marked (project) are our own conventions, items marked (unverified) come from third-party summaries and should be checked against m3.material.io before relying on them.

## Touch and layout
- DO make every touch target at least 48 dp. In CSS that is `min-h-12 min-w-12` (Tailwind) or `size-12`. [Touch_Target_Size]
- DO keep text contrast at 4.5:1 for small text and 3:1 for large text and icons. Muted text (`text-mute`) on `bg-panel2` must still pass. [Visual_Contrast]
- DO support light and dark themes, or state clearly that the app is dark-only. [Theme_Support]
- DO draw edge-to-edge and pad with safe-area insets (`pt-safe`, `pb-safe`). Keep system bars transparent. DON'T add a background behind the gesture bar. DON'T put touch targets under the gesture-navigation area.
- DO keep the keyboard from covering inputs. Check forms inside sheets with the keyboard open.
- DO survive rotation and fold changes without losing state. [Orientation_Transitions]
- DON'T use tables. (project)

## Navigation
- DO use the standard Back button and back gesture. DON'T add custom on-screen "are you sure you want to leave" prompts. [Back_Button_Nav, Back_Gesture_Nav]
- DO close an open sheet or selection mode with Back before leaving the page. (project, to implement)
- DO keep at most five bottom-navigation destinations. Back does not switch between bottom-nav tabs. (unverified)
- DO preserve user state (scroll position, selection, text in inputs) when the app leaves the foreground, and avoid data loss on back. [State_Preservation]

## Sheets, menus and feedback
- DO use a modal bottom sheet for contextual actions, short forms and confirmations: drag handle, scrim, dismiss by tapping outside, by Cancel and by Back. Never rely on a gesture alone to close it. (unverified, project)
- DO keep sheets short. Long lists get a tall sheet with its own scroll, not an inline panel pushed above the list. (project)
- DON'T insert panels at the top of a long list, the user has to scroll back up to reach them. Use a sheet, or a sticky bar at the bottom. (project)
- DO give brief feedback with a snackbar at the bottom and offer Undo for destructive-but-cheap actions. Confirm in a sheet for irreversible ones (delete files). (project)
- DO use haptics for long press and selection only. Respect the system setting, never force vibration. (project)

## Motion
- DO animate enter and exit (about 150 to 300 ms, ease-out in, ease-in out). DON'T animate for longer than 300 ms or block input during it. (project)
- DO honour reduced-motion. `Styles/app.css` already shortens all animations under `prefers-reduced-motion`.
- DON'T animate layout properties on long lists. Prefer `opacity` and `transform`.

## Playback (media apps)
- DO start audio or show a loading indicator within one second of the user pressing play. [Audio_Playback_Start]
- DO request audio focus when playback starts, abandon it when it stops, and handle focus changes: pause for a call, duck or pause for transient loss. [Audio_Focus_Request, Audio_Focus_Change]
- DO pause when headphones unplug or Bluetooth disconnects (becoming noisy). Media3 does this with `setHandleAudioBecomingNoisy(true)`.
- DO play in the background from a foreground service with a `MediaStyle` notification and a `MediaSession`. Media3 `MediaSessionService` publishes both. [Audio_Playback_Background, Audio_Notification_Style]
- DO set title, artist, album art and duration on the media item metadata so the lock screen and notification show them. The seek bar needs a known duration and the seek command.
- DO return to a sensible state when the app comes back to the foreground after a pause. [Audio_Playback_Resume]
- DON'T keep long-running services running when nothing plays. [Background_Service_Optimization]
- Verify on a real device with the screen off: `adb shell dumpsys media_session` must show PLAYING. (project, see the `adb` skill)

## Notifications and permissions
- DO ask for a permission at the moment it is needed and explain why first. Android 13+ needs `POST_NOTIFICATIONS` for the playback notification to show. [Permission_Rationale]
- DON'T use notifications for ads or cross-promotion. Only relevant, user-expected information. [Notification_Quality]

## Stability and performance
- DON'T block the UI thread. No `.Result`, `.Wait()` or long synchronous work in handlers. This is enforced by the analyzers. [Stability_ANR]
- DO keep startup fast. Defer database work and library scans until after the first frame. Use `Virtualize` for long lists.
- DO treat crashes and ANRs as release blockers. Check `adb logcat` for `AndroidRuntime:E` after each feature.

## Blazor Hybrid specifics (project)
- Android WebView does not fire `contextmenu` on long press. Use `touchstart` with a timer and cancel it on `touchend` and `touchcancel` (scrolling fires `touchcancel`).
- Tailwind class names must be literal strings or they are not generated.
- Fonts and icons are local files, no CDN, everything works offline.
- Screenshots and DOM checks: DevFlow with `--device <serial> -ap 9223` (see the `maui-devflow-debug` skill). If a command says another session is driving the app, wait a few seconds and retry.

## Review checklist before saying a UI change is done
1. Touch targets 48 dp, contrast OK, safe areas OK.
2. Back closes sheets and selection first.
3. Nothing inline above a long list that a user must scroll up to reach.
4. Enter and exit animations, reduced-motion respected.
5. Checked on the emulator with a screenshot, and on the phone for touch, haptics and playback.
