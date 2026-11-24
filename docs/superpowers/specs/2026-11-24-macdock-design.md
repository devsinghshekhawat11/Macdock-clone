# MacDock (working name) — Design

Date: 2026-11-24 · Status: draft, awaiting approval

## 1. Intent

An original macOS-style desktop shell for the owner's Windows laptop that **fully replaces the
Windows taskbar for daily use**: a magnifying dock at the bottom, a menu bar at the top, a
Control Center panel and a Dynamic Island. It is also a portfolio project, so the code should be
internship-quality: layered, tested where testable, and easy to explain.

The public "MacDock" app (gadgetcue.in) is used **only as a feature reference**. No code, names,
icons or other assets are copied from it.

## 2. Goals, non-goals, anti-goals

**Goals (v1)**
- Dock: pinned apps + running apps, smooth magnification, launch bounce, running-indicator dots,
  click to launch / focus / minimize, right-click menu (pin, unpin, quit), drag to reorder pins.
- Taskbar replacement: hide the Windows taskbar, reserve screen space so maximized windows don't
  sit under the dock, start at login, optional auto-hide.
- Menu bar: active app name, clock, battery, Wi-Fi, volume, buttons for Control Center and the
  Start menu / search.
- Control Center: volume and brightness sliders, Wi-Fi and Bluetooth toggles, dark-mode toggle,
  now-playing media tile.
- Dynamic Island: pill at top-center showing now-playing media (artwork, title, play/pause),
  volume/brightness HUD, and charging state; expands on hover.
- Translucent "glass" look (acrylic/blur) on dock, menu bar and panels; light and dark themes.
- Settings: dock size, magnification amount, auto-hide, start at login — stored as JSON.

**Non-goals (v1, maybe later)**
- Reading other apps' notifications in the island (Windows only allows this for packaged MSIX
  apps; revisit after packaging).
- System-tray icons of other apps (Discord, OneDrive…) — on Windows 11 these can't be read from
  the hidden taskbar; hosting them means re-implementing the tray. v1 workaround: a menu-bar
  button that temporarily shows the real taskbar.
- Launchpad, window previews on hover, Genie minimize, folder stacks, AirDrop-style sharing.
- Multi-monitor: v1 runs on the primary display only.

**Anti-goals (failure even if it "works")**
- Leaving the user with **no taskbar** after a crash or exit.
- Noticeable CPU/RAM drain while idle (target: < 1% CPU idle, < 120 MB RAM).
- Janky animation (target: 60 fps magnification on the laptop).

## 3. Constraints
- Windows 11 x64 (my call; Windows 10 likely works but is untested).
- .NET 8 SDK on the laptop for building; the cloud environment can compile but cannot run it.
- No admin rights required at runtime.
- Unsigned exe → SmartScreen will warn; acceptable for personal use.

## 4. Approaches considered
| Option | Pros | Cons |
|---|---|---|
| **C# / .NET 8 + WPF + Win32 P/Invoke (chosen)** | True transparent topmost windows, mature animation system, direct Win32/WinRT access, ~60–100 MB idle, strong resume value | WPF is older; blur needs interop |
| WinUI 3 | Native Mica/Acrylic, modern controls | Transparent borderless overlay windows are awkward; fighting the framework |
| Tauri / Electron | CSS animations are easy | Electron idles at 150 MB+; shell work still ends up in native code |

Chosen by the owner: WPF (accepted 2026-11-24).

## 5. Architecture

```
MacDock.sln
├─ src/MacDock.Core        net8.0          Pure logic, no Windows APIs → unit-testable anywhere
│    Magnification math, dock layout, pinned-app & settings models, JSON persistence,
│    service interfaces (IWindowTracker, IAudioService, IMediaService, IBrightnessService …)
├─ src/MacDock.Interop     net8.0-windows  Win32/COM/WinRT implementations of Core interfaces
│    NativeMethods (P/Invoke), TaskbarController, AppBar, WindowTracker (SetWinEventHook),
│    IconExtractor, Blur (DWM/composition), CoreAudio, MediaSession (GSMTC), Brightness (WMI),
│    Radios (Wi-Fi/Bluetooth), Power/Battery, ThemeService
├─ src/MacDock.App         net8.0-windows  WPF exe: windows, views, view models (MVVM)
│    DockWindow, MenuBarWindow, IslandWindow, ControlCenterWindow, SettingsWindow
└─ tests/MacDock.Core.Tests  xUnit tests for Core
```

- **Pattern:** MVVM (CommunityToolkit.Mvvm) + dependency injection (Microsoft.Extensions.Hosting).
  Views never call Win32 directly; they go through Core interfaces, so logic is testable and
  platform code is isolated.
- **Dependency rule:** App → Interop → Core. Core depends on nothing Windows-specific.
- **Event flow:** Interop services raise events (window opened/closed/activated, volume changed,
  media changed) → view models update observable state on the UI thread → views animate.

### Key mechanisms
- **Magnification:** each icon's scale = 1 + (maxScale − 1) · falloff(distance from cursor),
  with a cosine falloff over ~3 icon widths; computed per mouse-move in Core, rendered via
  `ScaleTransform` anchored at the bottom. Easing back to rest when the cursor leaves.
- **Running windows:** `EnumWindows` at startup, then `SetWinEventHook` (create/destroy/show/hide,
  foreground, minimize) to keep a live list; filter to "taskbar-eligible" windows (visible,
  unowned or app-window style, not tool windows, not cloaked). Group by process/AppUserModelID.
- **Taskbar replacement:** hide `Shell_TrayWnd` and `Shell_SecondaryTrayWnd`, register the dock as
  an AppBar (`SHAppBarMessage`) so the work area shrinks. Listen for the `TaskbarCreated`
  message (Explorer restarted) to re-hide.
- **Taskbar safety (anti-goal #1):** restore on normal exit, on unhandled exceptions, and on
  session end; `MacDock.exe --restore` command; a small watchdog that restores the taskbar if the
  main process dies.
- **Blur:** `SetWindowCompositionAttribute` (acrylic) with a fallback to a semi-opaque brush if
  unsupported or if Windows "Transparency effects" is off.
- **Media:** `GlobalSystemMediaTransportControlsSessionManager` (WinRT) for now playing.
- **Volume:** Core Audio `IAudioEndpointVolume` (via NAudio or thin COM interop — builder's call).
- **Brightness:** WMI `WmiMonitorBrightness` (built-in laptop panels only).

## 6. Milestones (each ends usable)
1. **Dock MVP** — window, pinned apps from JSON, real icons, magnification, launch + bounce.
2. **Running apps** — window tracking, dots, focus/minimize toggle, context menu, reorder.
3. **Taskbar replacement** — hide/restore, AppBar, watchdog, start at login, auto-hide.
4. **Menu bar** — clock, battery, Wi-Fi, volume, active app, Start/search, "show taskbar".
5. **Control Center** — sliders, toggles, dark mode, media tile.
6. **Dynamic Island** — media, HUDs, charging, hover expand.
7. **Settings + polish** — settings window, themes, performance pass.

## 7. Testing
- Unit tests for Core (magnification, layout, config persistence, window-eligibility rules
  expressed as pure functions).
- GitHub Actions on `windows-latest`: build + test on every push.
- Manual test checklist per milestone, run by the owner on the laptop.

## 8. Left to the builder
Exact visual values (sizes, radii, colors), NAudio vs raw COM, icon-cache strategy, logging
library, and how the watchdog is implemented (second process vs scheduled task).
