# EVE-O-Preview Pro

**Version 3.6.4** · A multi-client window manager for EVE Online.

EVE-O-Preview Pro keeps an eye on several simultaneously running EVE Online clients and lets you switch between them instantly. While running it shows a set of live thumbnails for each active client; clicking a thumbnail — or triggering a bound key, mouse button or gamepad button — brings that client to the front.

It is a task switcher. It does **not** relay keyboard/mouse events into the game, modify the EVE interface, or display a modified interface. It only brings a client's window to the foreground or resizes/minimizes it.

This is a fork of the open-source **EVE-O Preview** project with additional features and fixes (see below). Because window tracking relies on Windows DLLs, Windows is required.

---

## What's New in Pro

### Features

- **Per-thumbnail input bindings.** Right-click any thumbnail (without dragging) to bind an input that instantly switches to that client:
  - **Keyboard / Stream Deck hotkey** — a key combination such as `Ctrl + 1`, registered as a global system hotkey (so a Stream Deck *Hotkey* button works too).
  - **Mouse button** — an extra mouse button (Button 4 / X1, Button 5 / X2, or middle button), captured live.
  - **Gamepad button** — any XInput controller button (A/B/X/Y, D-pad, shoulders, stick clicks, Start/Back), captured live.
- **Force window resolution.** Pin each EVE client's window to an exact size so it no longer drifts or gets cut off on launch. Set a **global default** for all clients, or an **individual size per character** — both are saved and reapplied automatically every time that client starts.
- **Save window position the instant it moves.** Client window positions are persisted immediately when you move them — no need to switch clients or restart to make the new layout stick.

### Fixes

- Client window positions persist reliably across restarts.
- Resolution is applied by exact absolute size each launch (position left untouched), so repeated launches no longer cause window "creep" / growth.

---

## How To Install & Use

1. Download and extract the release `.zip` to a location of your choice (e.g. Desktop or your EVE folder).
   - **Note:** Do not install into *Program Files* or *Program Files (x86)*. EVE-O-Preview Pro stores its configuration file next to its executable and needs write access to that folder.
2. Start EVE-O-Preview Pro and your EVE clients (order does not matter).
3. Adjust settings as you see fit — options are described below.

**Requirements**

- Windows 10 / 11 (Windows 7/8.1 may work but are untested on this build)
- EVE client **Display Mode** set to **Fixed Window** or **Window Mode** — **Fullscreen** is not supported.

---

## Binding Inputs to Thumbnails

Right-click a thumbnail **without dragging it** to open the binding menu:

| Menu item | What it binds |
| --- | --- |
| Assign Keyboard / Stream Deck Hotkey... | A key combination (e.g. `Ctrl + 1`). Registered as a global system hotkey, so a Stream Deck *Hotkey* action works here as well. |
| Assign Mouse Button... | An extra mouse button — Button 4 (X1), Button 5 (X2), or the middle button. Captured live: just press the button you want. |
| Assign Gamepad Button... | A controller button via XInput. Captured live: just press the button on the controller. |
| Clear Bindings | Removes all three bindings from that client. |

Notes:

- Bindings are saved to `EVE-O-Preview.json` immediately and work globally, even while an EVE client has keyboard focus.
- A given mouse or gamepad button can only be assigned to one client at a time; assigning it to a new client automatically clears it from the others.
- Mouse-button detection uses a low-level mouse hook and gamepad detection uses XInput (built into Windows); no extra software is required. The mouse hook is passive and never swallows the click, so normal mouse behaviour is unchanged.

---

## Forcing Window Resolution

Right-click a thumbnail to open the menu, then:

| Menu item | What it does |
| --- | --- |
| Set Window Size... | Set an exact window size for **this** character. Pick a preset (720p … 4K) or enter a custom width/height. Saved per character and reapplied on every launch. |
| Set Window Size for All Clients... | Set a **global default** size applied to every client that doesn't have its own size. |
| Clear Window Size | Remove the per-character size so this client falls back to the global default (or to no forced size if none is set). |

The size is applied as an exact window size while leaving the window's position alone, so repeated launches don't cause the window to creep or grow.

---

## Mouse Gestures and Actions

Mouse gestures apply to the thumbnail currently hovered.

| Action | Gesture |
| --- | --- |
| Activate the EVE client and bring it to front | Click the thumbnail |
| Minimize the EVE client | Hold Control and click the thumbnail |
| Toggle the client's inclusion in the configured cycle groups | Hold Shift and click the thumbnail |
| Switch to the last used non-EVE application | Hold Control + Shift and click any thumbnail |
| Move thumbnail to a new position | Press right mouse button and move the mouse |
| Adjust thumbnail height | Press both left and right mouse buttons and move up/down |
| Adjust thumbnail width | Press both left and right mouse buttons and move left/right |
| Open the binding / window-size menu | Right-click the thumbnail **without** moving it |

---

## Application Options

### General Tab
| Option | Description |
| --- | --- |
| Minimize to System Tray | Whether the main window is minimized to the Windows tray when closed |
| Track client locations | Whether a client's window position is restored when it is activated or started |
| Hide preview of active EVE client | Whether the thumbnail of the active client is hidden |
| Minimize inactive EVE clients | Auto-minimize inactive clients to save CPU/GPU |
| Animation Style | Original animation style (0) or No Animation (1) |
| Hide caption bar on clients | Hide/show the caption bar on EVE clients |
| Previews always on top | Keep thumbnails on top of all other windows |
| Hide previews when EVE client is not active | Show thumbnails only while an EVE client is active |
| Unique layout for each EVE client | Different thumbnail positions depending on the active client |

### Thumbnail Tab
| Option | Description |
| --- | --- |
| Opacity | Inactive thumbnail opacity (20%–100%) |
| Thumbnail Width | 100–640 points |
| Thumbnail Height | 80–400 points |
| Lock Thumbnail Location | Prevent misclicks from moving thumbnails |
| Thumbnail Snap to Grid | Snap thumbnails to a grid when moved |
| Snap X / Snap Y | Grid size in pixels |
| Do not display previews | Hide previews (unless overridden per client) |
| Preview background color | Background color used when a preview is hidden (avoid `#000001` — it clashes with transparency) |

### Zoom Tab
| Option | Description |
| --- | --- |
| Zoom on hover | Zoom a thumbnail while hovered |
| Zoom factor | 2–10 |
| Zoom anchor | Starting point of the thumbnail zoom |

### Overlay Tab
| Option | Description |
| --- | --- |
| Show overlay | Display the client's name on the thumbnail |
| Show frames | Display thumbnails with caption and borders |
| Highlight active client | Highlight the active client's thumbnail with a bright border |
| Color | Highlight color for the active client |
| Cycle Group Indicator Position | Position of the "exclude from cycle group" indicator |
| Label Font / Color / Position | Overlay label font, color and position |

### Active Clients Tab
| Option | Description |
| --- | --- |
| Thumbnails list | Currently active client thumbnails. Checking an element hides that thumbnail (not persisted between runs). |

---

## Configuration File-Only Options

Some options are not exposed in the GUI and are edited directly in `EVE-O-Preview.json`.

**Note:** Edit the configuration file only while EVE-O-Preview Pro is closed, or your changes may be lost. Keep a backup before editing.

| Option | Description |
| --- | --- |
| **ActiveClientHighlightThickness** | Border thickness for the active client's highlight. `1`–`6`. Default `3`. |
| **CompatibilityMode** | Enables the screenshot-based render (see below). Default `false`. |
| **EnableThumbnailSnap** | Set `false` to disable thumbnail snapping. Default `true`. |
| **HideThumbnailsDelay** | Delay before thumbnails hide (in refresh periods) when *Hide previews when EVE client is not active* is on. Default `2` (~1s). |
| **HideLoginClientThumbnail** | Hide previews for clients sitting at the character-selection screen. Default `false`. |
| **PriorityClients** | Clients never auto-minimized on inactivity. Default `[]`. |
| **ThumbnailMinimumSize** | Minimum thumbnail size, `"width, height"`. Default `"100, 80"`. |
| **ThumbnailMaximumSize** | Maximum thumbnail size, `"width, height"`. Default `"640, 400"`. |
| **ThumbnailRefreshPeriod** | Refresh period in ms, `300`–`1000`. Default `500`. |
| **ThumbnailResizeTimeoutPeriod** | Resize timeout in ms, `200`–`5000`. Default `500`. Increase if previews resize incorrectly on startup. |
| **ExecutablesToPreview** | Executables to show previews for. Default `["exefile"]`. |
| **IconName** | Application icon. Default `""`. |

### Per-Client Config Overrides

The configuration file also supports per-client overrides, each keyed by the client's window title (e.g. `"EVE - Character Name"`). A client not listed falls back to the global setting.

- **ClientHotkey** — per-client jump-to hotkey, e.g. `"EVE - Char": "F1"` or `"Control+Shift+F4"`.
- **PerClientActiveClientHighlightColor** — per-client highlight border color.
- **PerClientThumbnailSize** — per-client thumbnail size, e.g. `"240, 180"`.
- **PerClientZoomAnchor** — per-client zoom anchor (`0`–`8`: 0-NW, 1-N, 2-NE, 3-W, 4-Center, 5-E, 6-SW, 7-S, 8-SE).
- **PerClientPreventPreviews** — per-client `true`/`false` to force hide/show preview.
- **PerClientPreventPreviewColor** — background color when a client's preview is hidden.

### Cycle Groups

Up to five cycle groups let a hotkey rotate focus through a chosen set of clients. Each group has `CycleGroupNForwardHotkeys`, `CycleGroupNBackwardHotkeys` and `CycleGroupNClientsOrder` (a map of window title → order number). Characters not listed are skipped; `"EVE"` matches clients at the character-selection screen. Leaving the hotkey lists empty disables that group; deleting them entirely regenerates sample values on next run.

### Minimize All Clients

`MinimizeAllClientsHotkeys` sets hotkeys that minimize every EVE client at once, e.g. `[ "F22" ]`.

**Hotkey hints**

- Prefer unusual function keys (F13–F24) bound to a gaming mouse/keypad to avoid clashing with in-game keys.
- A key combination captured by EVE-O-Preview Pro is *not* retranslated to EVE — it only switches the window.
- Using the Control modifier can avoid interrupting target locking (Ctrl+Click). Registering both `F14` and `Control+F14` covers both cases.
- Supported key names: <https://learn.microsoft.com/dotnet/api/system.windows.forms.keys>
- Supported color names: <https://learn.microsoft.com/dotnet/api/system.drawing.color#properties>

### Compatibility Mode

An alternate screenshot-based render that works in remote-desktop environments. Trade-offs: significantly more memory, ~1 FPS thumbnails, and possible brief cursor freezes.

---

## Building from Source

This fork targets **.NET 8** (`net8.0-windows8.0`) and builds with the .NET 8 SDK.

```
dotnet build src\Eve-O-Preview.sln -c Release
```

The build output is written to the `bin` folder. The EVE client window-tracking code is Windows-only.

---

## EVE Online EULA / ToS

This type of application is permitted under the EVE Online EULA/ToS.

> CCP Grimmi: Overlays which contain a full, unchanged, EVE Client instance in a view-only mode, no matter how large or small they are scaled — like EVE-O Preview — are fine with us. These overlays do not allow any direct interaction with the EVE Client; you have to bring the respective EVE Client to the front to interact with it.

Do **not** use EVE-O-Preview Pro for anything that breaks the EULA or ToS of EVE Online. If you believe any feature or combination of features could enable ToS-breaking behaviour, treat it as a bug and report it.

---

## Credits

EVE-O-Preview Pro is a fork of the open-source **EVE-O Preview** project, originally created by StinkRay and maintained over the years by the EVE-O Preview community. Thanks to all upstream authors and contributors.

---

## CCP Copyright Notice

EVE Online, the EVE logo, EVE and all associated logos and designs are the intellectual property of CCP hf. All artwork, screenshots, characters, vehicles, storylines, world facts or other recognizable features of the intellectual property relating to these trademarks are likewise the intellectual property of CCP hf. EVE Online and the EVE logo are the registered trademarks of CCP hf. All rights are reserved worldwide. All other trademarks are the property of their respective owners. CCP hf. has granted permission to EVE-O Preview to use EVE Online and all associated logos and designs for promotional and information purposes but does not endorse, and is not in any way affiliated with, EVE-O Preview. CCP is in no way responsible for the content on or functioning of this program, nor can it be liable for any damage arising from the use of this program.
