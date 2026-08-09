# Deployment

A reproducible desktop install: publish, icon, and **elevated launch with no UAC prompt**
on every double-click.

## Why it works this way

The app needs administrator rights to read CPU sensors (the **PawnIO** kernel driver, via
LibreHardwareMonitor). Its manifest is `asInvoker`, so a shortcut with the "run as
administrator" flag would prompt for UAC **every single time**. Instead:

- a **scheduled task** is registered (Task Scheduler) with `RunLevel=Highest`;
- the shortcut does not point at the exe but at a silent `launch.vbs`, which triggers that
  task (`wscript` → `schtasks /run`);
- triggering **your own** task does not require elevation → no UAC prompt.

Administrator rights are needed only to *register* the task, so the script self-elevates
for exactly that step — one UAC prompt for the whole installation.

## Install

```powershell
pwsh -File deploy/Install.ps1
```

With auto-start at Windows sign-in (also without UAC):

```powershell
pwsh -File deploy/Install.ps1 -AutoStart
```

What the script does:

1. `dotnet publish` into `%LOCALAPPDATA%\Programs\SystemBarIndicator` (flat layout — LHM
   0.9.6 ships as a RID-split package).
2. Writes `launch.vbs` (the hidden task launcher).
3. Registers the `SystemBarIndicator` task with highest privileges (**the one UAC prompt**).
4. Creates a `SystemBar Indicator` desktop shortcut with the app icon and the "run as
   administrator" flag explicitly **cleared**.

After that, double-clicking the shortcut starts the indicator elevated, with no prompt.

## Uninstall

```powershell
pwsh -File deploy/Uninstall.ps1               # remove task, shortcut and launch.vbs
pwsh -File deploy/Uninstall.ps1 -RemoveFiles  # also delete the install directory
```

## Requirements

- .NET 10 SDK (for `dotnet publish`).
- **PawnIO** — for CPU temperature, installed once from [pawnio.eu](https://pawnio.eu).
  Without it everything works except CPU temperature, and the vulnerable WinRing0 driver is
  never loaded.

## Note on encoding

`Install.ps1` and `Uninstall.ps1` are deliberately **ASCII-only**. A script containing
non-ASCII characters, saved as UTF-8 without a BOM and re-read by `powershell.exe`
(Windows PowerShell 5.1) under the ANSI code page, gets corrupted — that once caused the
task to be registered under a mojibake folder name. Keep executable `.ps1` text ASCII.
