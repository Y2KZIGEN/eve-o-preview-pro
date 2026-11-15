# Changelog

All notable changes to **EVE-O-Preview Pro** are documented here.

The version scheme is `Features.Subfeatures.Fixes`.

## [3.6.4]

First Pro release. Built on the open-source EVE-O Preview project, retargeted to .NET 8 with the additions below.

### Added

- **Per-thumbnail input bindings.** Right-click a thumbnail (without dragging) to open a binding menu:
  - **Keyboard / Stream Deck hotkey** — bind a key combination (e.g. `Ctrl + 1`), registered as a global system hotkey so Stream Deck *Hotkey* buttons work too.
  - **Mouse button** — bind an extra mouse button (Button 4 / X1, Button 5 / X2, or middle), captured live via a passive low-level mouse hook.
  - **Gamepad button** — bind any XInput controller button (A/B/X/Y, D-pad, shoulders, stick clicks, Start/Back), captured live.
  - **Clear Bindings** removes all three bindings from a client. A given mouse/gamepad button can only be bound to one client at a time.
- **Force window resolution.** Pin an EVE client's window to an exact size from the right-click menu:
  - **Set Window Size...** — an exact size per character (presets 720p–4K or custom), saved and reapplied on every launch.
  - **Set Window Size for All Clients...** — a global default size for clients without their own.
  - **Clear Window Size** — drop a per-character size back to the global default.
- **Save window position the instant it moves.** Client window positions are persisted immediately on move — no client switch or restart required.

### Changed

- Rebranded to **EVE-O-Preview Pro** across the UI (window title, menu, tray, About tab).
- New transparent Pro application icon (executable, window, and tray).
- About tab cleaned of upstream branding/links and made scrollable.
- README rewritten to lead with Pro features and document the local .NET 8 build.
- Retargeted to **.NET 8** (`net8.0-windows8.0`); version set to `3.6.4`.

### Fixed

- Client window positions now persist reliably across restarts.
- Resolution is applied as an exact absolute window size each launch (position left untouched), eliminating the window "creep" / growth seen on repeated launches.

### Notes

- Functional identifiers are unchanged from upstream (config file `EVE-O-Preview.json`, assembly name, process name, mutex), so existing profiles keep loading.
- Windows only — client window tracking relies on Windows DLLs.

[3.6.4]: https://github.com/Y2KZIGEN/eve-o-preview-pro/releases/tag/v3.6.4
