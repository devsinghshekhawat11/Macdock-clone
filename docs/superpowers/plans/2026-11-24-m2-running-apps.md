# Milestone 2: Running Apps Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The dock shows every running app (pinned or not) with an indicator dot. Clicking launches, focuses or minimizes it. Right-click offers pin/unpin, new window and quit, and pins can be reordered by dragging.

**Architecture:** Core gains a window model (`WindowInfo`, `IWindowService`), a pure eligibility filter and a pure grouping function, so the logic that decides *which* icons appear and *what a click does* is unit-tested. Interop implements `IWindowService` with `EnumWindows` + `SetWinEventHook`. App adds dots, context menus and drag-reorder.

**Tech Stack:** as M1 (.NET 8, WPF, CommunityToolkit.Mvvm, xUnit). No new packages.

**Spec:** `docs/superpowers/specs/2026-11-24-macdock-design.md` (milestone 2). Builds on M1 (merged in #1).

## Global Constraints
- Core stays free of Windows APIs; Interop stays IL-only with no `unsafe`/`AllowUnsafeBlocks` (Smart App Control on the owner's laptop flags such assemblies). Use classic `[DllImport]`.
- No apphost exe (`UseAppHost=false` stays).
- Idle target unchanged: < 1% CPU. Window events are coalesced; no polling timers.
- The dock never lists its own windows.

## Decisions made in this plan
- **Grouping key:** a window's AppUserModelID if it has one (this is what groups UWP apps hosted in `ApplicationFrameHost.exe`), else its exe path (lower-cased), else `pid:<id>` when the exe path can't be read (e.g. some elevated apps).
- **Pin ↔ window matching:** `shell:AppsFolder\<AUMID>` pins match windows with that AppId. Exe pins match by full path, or by file name when the pin is a bare name (`notepad.exe`).
- **Order:** pins first in config order, then unpinned running apps in first-seen order (kept stable across z-order changes).
- **Click:** not running → launch (bounce). Running and owns the foreground window → minimize that window. Otherwise → restore and focus the app's topmost window.
- **Refresh strategy:** any relevant WinEvent schedules one full re-enumeration 60 ms later (coalesced). Full `EnumWindows` is < 1 ms and far simpler to keep correct than incremental bookkeeping.
- **Unpinned icon target:** exe path, or `shell:AppsFolder\<AUMID>` for packaged apps, so the M1 icon provider works unchanged.
- Window thumbnails/previews remain out of scope (spec non-goal for v1).

## Review Focus
1. Two windows of one app (two Notepads) → one icon; clicking focuses one; clicking again while focused minimizes it. (Task 3 tests)
2. Settings/Calculator (UWP via ApplicationFrameHost) group under their own icon, not as one "ApplicationFrameHost" icon. (Task 2 test)
3. Elevated app whose exe path can't be read (Task Manager) still appears and doesn't merge with other apps. (Task 2 test)
4. Unpinning a running app keeps it in the dock as an unpinned running app; quitting it then removes it. (Task 3 tests)
5. A window closes while the dock animates → item removed, others keep order and scale; no crash. (Task 3 test)

---

### Task 1: Window model and eligibility filter

**Files:** Create `src/MacDock.Core/Windows/{WindowInfo.cs, IWindowService.cs, WindowTraits.cs, WindowFilter.cs}`, `tests/MacDock.Core.Tests/Windows/WindowFilterTests.cs`

**Interfaces — Produces:**
- `record WindowInfo(nint Handle, int ProcessId, string? ExePath, string? AppId, string Title, bool IsMinimized)`
- `interface IWindowService { IReadOnlyList<WindowInfo> Windows { get; } nint Foreground { get; } event EventHandler? Changed; void Activate(nint h); void Minimize(nint h); void Close(nint h); }` — `Windows` is in z-order (topmost first); `Changed` is raised on the UI thread
- `readonly record struct WindowTraits(bool IsVisible, bool IsCloaked, bool HasOwner, bool IsToolWindow, bool IsAppWindow, bool IsNoActivate, string Title, bool IsOwnProcess)`
- `static bool WindowFilter.IsTaskbarWindow(WindowTraits t)` = visible ∧ ¬cloaked ∧ ¬own process ∧ title not blank ∧ (AppWindow ∨ (¬ToolWindow ∧ ¬HasOwner ∧ ¬NoActivate))

- [ ] **Step 1: Failing tests** — `NormalTopLevelWindow_Included`; `Invisible_Excluded`; `Cloaked_Excluded` (other virtual desktops / suspended UWP); `OwnProcess_Excluded`; `EmptyTitle_Excluded`; `ToolWindow_Excluded`; `OwnedWindow_Excluded`; `AppWindowStyle_OverridesOwnerAndToolWindow_Included`.
- [ ] **Steps 2–4:** fail → implement → pass (`dotnet test`).
- [ ] **Step 5:** Commit.

### Task 2: Grouping windows into dock entries

**Files:** Create `src/MacDock.Core/Dock/{AppKey.cs, DockGrouping.cs}`, `tests/.../Dock/DockGroupingTests.cs`

**Interfaces:**
- Consumes: `PinnedApp`, `WindowInfo`
- Produces:
  - `static string AppKey.ForWindow(WindowInfo w)` (AppId → exe path lower → `pid:N`); `static bool AppKey.PinMatches(PinnedApp pin, WindowInfo w)`; `static string AppKey.ForPin(PinnedApp pin)`
  - `record DockEntry(string Key, PinnedApp? Pin, string DisplayName, string IconTarget, IReadOnlyList<WindowInfo> Windows)` with `bool IsRunning => Windows.Count > 0`
  - `static IReadOnlyList<DockEntry> DockGrouping.Group(IReadOnlyList<PinnedApp> pins, IReadOnlyList<WindowInfo> windows, IReadOnlyList<string> previousUnpinnedOrder)` — a window joins the first pin it matches, else an unpinned entry by `AppKey.ForWindow`; unpinned entries ordered by `previousUnpinnedOrder` then first appearance; unpinned `DisplayName` = exe file name without extension (title fallback), `IconTarget` = exe path or `shell:AppsFolder\<AppId>`

- [ ] **Step 1: Failing tests** — `NoWindows_OnlyPins_NotRunning`; `BareExePin_MatchesWindowByFileName` (pin `notepad.exe`, window `C:\Windows\System32\notepad.exe`); `TwoWindowsSameApp_OneEntryTwoWindows`; `AppsFolderPin_MatchesByAppId`; `FrameHostWindows_GroupByAppId_NotByExe` (two `ApplicationFrameHost.exe` windows with different AppIds → two entries); `NullExePath_KeyedByPid_NotMerged`; `UnpinnedOrder_IsStable` (previous order `["b","a"]` with windows in z-order a,b → b,a; new app appended last); `PinsAlwaysFirst_InConfigOrder`.
- [ ] **Steps 2–4:** fail → implement → pass.
- [ ] **Step 5:** Commit.

### Task 3: Dock view models driven by running windows

**Files:** Modify `src/MacDock.Core/Dock/{DockViewModel.cs, DockItemViewModel.cs}`; modify `tests/.../Dock/DockViewModelTests.cs`

**Interfaces:**
- Consumes: Task 1–2, M1 `IConfigStore`, `IAppLauncher`, `Magnification`, `Smoothing`
- Produces:
  - `DockViewModel(IConfigStore store, IAppLauncher launcher, IWindowService windows)` — subscribes to `windows.Changed` → `Refresh()`
  - `void Refresh()` — regroups and updates `Items` **in place**: reuse items by `Key` (keeps `Scale` and bounce subscriptions), `Move` to new positions, remove/insert others, then recompute rest centers
  - `int IndexFromX(double x)` — index of nearest rest center (clamped)
  - `void MovePin(int fromIndex, int toIndex)` — reorders pinned items only (ignores unpinned indices), saves config
  - `event EventHandler? LayoutChanged` — raised when item count changes (view re-sizes window)
  - `DockItemViewModel`: `string Key`, `PinnedApp? Pin`, `string DisplayName`, `string IconTarget`, `[ObservableProperty] bool IsRunning`, `bool IsPinned => Pin is not null`, `IReadOnlyList<WindowInfo> Windows`, commands `ClickCommand` (decision rule above), `NewWindowCommand` (launch, bounce), `TogglePinCommand` (pin running app as `PinnedApp(Key, DisplayName, IconTarget)` / unpin; saves config), `QuitCommand` (Close every window; enabled only when running). `Scale`, `Size`, `TargetScale`, `BounceRequested` unchanged. `App` from M1 is replaced by `Pin`.

- [ ] **Step 1: Failing tests** (fake window service with settable list/foreground and recorded calls) — keep M1 tests (adapted to `Pin`/`ClickCommand`) plus: `Click_NotRunning_Launches_AndBounces`; `Click_RunningNotForeground_ActivatesTopmostWindow`; `Click_RunningAndForeground_MinimizesForeground`; `Click_RunningMinimized_Activates`; `UnpinnedRunningApp_AppearsAfterPins`; `Refresh_ReusesItemInstances_AndKeepsScale`; `WindowClosed_ItemRemoved_OthersKeepOrder`; `Unpin_RunningApp_StaysAsUnpinned_AndSavesConfig`; `Unpin_NotRunning_RemovesItem`; `Pin_RunningApp_SavesConfig`; `Quit_ClosesAllWindowsOfApp`; `MovePin_ReordersAndSaves`; `IndexFromX_NearestCenter_Clamped`; `UpdateCursor_BeforeContainerWidth_DoesNotThrow` (M1 deferred minor).
- [ ] **Steps 2–4:** fail → implement → pass.
- [ ] **Step 5:** Commit.

### Task 4: Interop window tracker

**Files:** Create `src/MacDock.Interop/Native/User32.cs`, `src/MacDock.Interop/Native/AppUserModel.cs`, `src/MacDock.Interop/WindowTracker.cs`

**Interfaces — Produces:** `sealed class WindowTracker : IWindowService, IDisposable` with `void Start()` (must be called on the UI thread).

- Enumeration: `EnumWindows`; per window build `WindowTraits` from `IsWindowVisible`, `DwmGetWindowAttribute(DWMWA_CLOAKED)`, `GetWindow(GW_OWNER)`, `GetWindowLongPtr(GWL_EXSTYLE)`, `GetWindowText`, `GetWindowThreadProcessId` vs `Environment.ProcessId`; keep those passing `WindowFilter`.
- ExePath: `OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION)` + `QueryFullProcessImageName`; null on failure; cache per pid.
- AppId: `SHGetPropertyStoreForWindow` → `IPropertyStore.GetValue(PKEY_AppUserModel_ID)`; read `VT_LPWSTR`, always `PropVariantClear`. Null if absent.
- Events: one `SetWinEventHook(EVENT_MIN, EVENT_MAX, …, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS)`; callback ignores `idObject != OBJID_WINDOW || idChild != 0`, then restarts a 60 ms `DispatcherTimer`; on tick re-enumerate and raise `Changed` only if the list differs. Keep the delegate in a field (GC safety); `UnhookWinEvent` in `Dispose`.
- Actions: `Activate` → `ShowWindow(SW_RESTORE)` if `IsIconic`, then `SetForegroundWindow`; `Minimize` → `ShowWindowAsync(SW_MINIMIZE)`; `Close` → `PostMessage(WM_CLOSE)`.

- [ ] **Step 1:** Implement; `dotnet build -c Release` passes; confirm no `UnverifiableCodeAttribute` on MacDock.Interop.dll.
- [ ] **Step 2:** Commit.

### Task 5: App — dots, context menu, drag-reorder

**Files:** Modify `src/MacDock.App/{App.xaml.cs, Views/DockWindow.xaml, Views/DockWindow.xaml.cs, README.md}`

- DI: register `WindowTracker` as `IWindowService` singleton; call `Start()` before showing the dock; dispose on exit.
- Item template: icon bound to `IconTarget`; 4-DIP white dot centered under the icon, visible when `IsRunning`; ToolTip `DisplayName`; click → `ClickCommand`.
- Per-item ContextMenu: "New window" (when running) / "Open" (when not), "Pin to Dock" / "Unpin from Dock", separator, "Quit" (when running).
- Drag-reorder (pinned items only): mouse down records start; > 6 DIP horizontal move captures mouse and shows the item at 60% opacity; mouse up → `MovePin(from, IndexFromX(x))`; a drag suppresses the click.
- On `LayoutChanged` → `PlaceOnScreen()`.
- README: M2 features + manual checklist (Review Focus 1–5, drag reorder persists after restart, dock ignores its own window, opening/closing apps updates within ~0.1 s, idle CPU ~0%).

- [ ] **Step 1:** Implement; `dotnet build -c Release` passes; `dotnet test` passes.
- [ ] **Step 2:** Commit, push, open PR.
