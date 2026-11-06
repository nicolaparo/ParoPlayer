# Mobile UI rules


Sources: [Android touch target guidance](https://support.google.com/accessibility/android/answer/7101858) (verified); the rest is established Material/Android practice, not re-verified online.

- **No tables. Ever** (also not in the Wi-Fi web UI). Use lists of rows, cards or definition-style label/value pairs. Never `<table>`; `CodingRulesTests` scans `.razor` for `<table`.
- **Touch targets at least 48x48 dp**, 8 dp apart. Small icons get padding, not a smaller hit area.
- One primary action per screen, thumb-reachable (bottom half); bottom navigation with 4 destinations max.
- Long lists use Blazor `<Virtualize>`; never render the whole library at once. Images lazy-loaded and cached thumbnails.
- Respect safe areas (`env(safe-area-inset-*)`), light and dark themes, system font scaling (use `rem`, not `px`, for text), and `prefers-reduced-motion`.
- Every icon-only button has an `aria-label`; text contrast at least 4.5:1.
- Gestures always have a visible alternative (e.g. swipe to remove also has a menu item). Destructive actions offer Undo, not a confirm dialog.
- Offline is the normal state: no spinner for local data, clear message for features that need a network (download, online tag lookup).
- Keep the WebView light: no heavy JS frameworks, no CDN, small CSS bundle, minimal re-renders (`ShouldRender`, keyed lists).

