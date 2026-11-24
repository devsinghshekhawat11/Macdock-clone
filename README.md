# MacDock (working name)

An original macOS-style shell for Windows 11: a magnifying dock, and later a menu bar, Control
Center and Dynamic Island, built with **C# / .NET 8 / WPF** and Win32 interop.

> Status: **Milestone 2, running apps.** Every open app appears in the dock with a dot under it. The Windows
> taskbar is still visible; replacing it comes in milestone 3.

**Using the dock**
- **Click** an app: opens it if it isn't running, brings it to the front if it is, and minimizes it if it's already in front.
- **Right-click** an app: Open / New window, Pin to Dock / Unpin from Dock, Quit.
- **Drag** a pinned app left or right to reorder pins (saved to your config).
> Design: [`docs/superpowers/specs/2026-11-24-macdock-design.md`](docs/superpowers/specs/2026-11-24-macdock-design.md)

## Run it

Requirements: Windows 10/11 x64 and the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
git clone https://github.com/devsinghshekhawat11/macdock-clone
cd macdock-clone
dotnet run --project src/MacDock.App -c Release
```

> **"An Application Control policy has blocked this file"?** That's Windows 11 Smart App Control blocking an
> unsigned exe. MacDock deliberately ships no exe (`UseAppHost=false`) and runs inside the Microsoft-signed
> `dotnet` host, so `git pull` and run again. After a build you can also start it directly with
> `dotnet src\MacDock.App\bin\Release\net8.0-windows\MacDock.dll`.
> Don't turn Smart App Control off for this: it can't be turned back on without reinstalling Windows.

- **Quit:** right-click the dock background, then choose **Quit MacDock**.
- **Pins** live in `%APPDATA%\MacDock\config.json` (created on first run). Edit it, then restart:

```json
{
  "Pins": [
    { "Id": "code", "DisplayName": "VS Code", "Target": "C:\\Users\\you\\AppData\\Local\\Programs\\Microsoft VS Code\\Code.exe" },
    { "Id": "edge", "DisplayName": "Edge", "Target": "msedge.exe" }
  ],
  "Settings": { "IconSize": 48, "MaxScale": 1.8, "AutoHide": false }
}
```

`Target` can be a full exe path, a bare exe name found on `PATH`, or a `shell:AppsFolder\<AppUserModelID>` name
(run `Get-StartApps` in PowerShell to list the IDs of Store apps).

## Architecture

```
src/MacDock.Core      net8.0          Pure logic + view models (no Windows APIs) → unit-tested on any OS
src/MacDock.Interop   net8.0-windows  Win32/COM: shell launching, icon extraction, window styles
src/MacDock.App       net8.0-windows  WPF views and the composition root (DI)
tests/MacDock.Core.Tests              xUnit
```

Dependencies point one way: **App → Interop → Core**. View models live in Core (CommunityToolkit.Mvvm is
platform-neutral), so the dock's behavior is tested without a Windows desktop.

How the magnification works:
- Each icon's target scale is `1 + (max − 1) · (cos(π·d/range) + 1) / 2` for distance `d < range` from the cursor
  (`Core/Dock/Magnification.cs`). Distances use **rest** (unscaled) icon positions, so the growing layout can't
  feed back into its own input and jitter.
- Every frame, scales ease toward their targets with frame-rate-independent exponential smoothing
  (`Core/Dock/Smoothing.cs`). The render loop unhooks itself when everything settles, so an idle dock uses
  ~0% CPU.
- The window is sized once to the fully magnified bounds. Transparent pixels of a layered window are
  click-through, so the empty headroom never blocks the apps underneath.

## Develop

```bash
dotnet build
dotnet test
```

CI (GitHub Actions, `windows-latest`) builds and tests every push and PR.

## Manual test checklist (milestone 2)

- [ ] Every app on your taskbar also appears in the dock (pinned ones in place, others after them), each with a dot.
- [ ] Opening or closing an app updates the dock within about a tenth of a second.
- [ ] Two Notepad windows → one Notepad icon. Click: Notepad comes to the front. Click again: it minimizes. Click again: it's restored.
- [ ] Settings and Calculator show as two separate icons with their real icons (not one "ApplicationFrameHost").
- [ ] Task Manager (runs elevated) appears in the dock.
- [ ] Right-click a running unpinned app → Pin to Dock; restart MacDock → it's still pinned.
- [ ] Right-click a running pinned app → Unpin from Dock → it stays (unpinned, with dot) until you quit it.
- [ ] Right-click → Quit closes all of that app's windows (apps may ask to save).
- [ ] Drag a pin to a new spot → order is kept after restarting MacDock. A drag never launches the app.
- [ ] Switching between apps doesn't reorder the unpinned icons.
- [ ] The dock never shows an icon for itself.
- [ ] Task Manager shows ~0% CPU for MacDock while idle.

## Manual test checklist (milestone 1)

- [ ] The dock appears bottom-center, above the taskbar.
- [ ] Hovering magnifies icons smoothly; moving away settles them back to rest.
- [ ] Clicking Notepad launches it and the icon bounces.
- [ ] A pin with a bad path shows a grey placeholder; clicking it does nothing and doesn't crash.
- [ ] At 125% / 150% display scaling the dock still sits on the bottom edge, centered.
- [ ] Changing display scaling while MacDock runs re-centers the dock on the bottom edge.
- [ ] A pin like `"Target": "msedge.exe"` shows the real Edge icon.
- [ ] Icon edges look smooth (no dark fringes).
- [ ] Starting a second copy does nothing.
- [ ] Task Manager shows ~0% CPU when the mouse isn't over the dock.
- [ ] Clicks in the empty area just above the dock reach the window underneath.
