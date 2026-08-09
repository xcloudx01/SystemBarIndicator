# Security Policy

## Reporting a vulnerability

Please report security issues privately through GitHub's
[private vulnerability reporting](https://github.com/coldneuro/SystemBarIndicator/security/advisories/new)
rather than in a public issue. Include what you found, how to reproduce it, and what an
attacker could do with it. This is a hobby project maintained by one person — expect a
reply within a couple of weeks.

## What this app does with privileges

Worth stating plainly, because a system monitor asking for administrator rights deserves
scrutiny:

- **It makes no network connections.** No telemetry, no update check, no analytics. The
  network figures shown are OS counters read locally.
- **It only reads.** Sensors, memory statistics and performance counters — nothing is
  written outside `%LOCALAPPDATA%\SystemBarIndicator\` (settings and log).
- **The manifest is `asInvoker`**, not `requireAdministrator`. The app never elevates
  itself; running it normally just means temperatures are unavailable.
- **Elevation, when used, is explicit.** `deploy/Install.ps1` registers a Task Scheduler
  task with `RunLevel=Highest` and self-elevates only for that step — one UAC prompt during
  installation. Afterwards the desktop shortcut triggers that task, which is why launching
  does not prompt again. If you would rather not have that, skip the install script and run
  the app unelevated.

## Kernel driver

Temperatures are read via
[LibreHardwareMonitorLib](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor)
0.9.6, which accesses CPU sensors through the signed [PawnIO](https://pawnio.eu) kernel
driver. PawnIO is deliberate: the older WinRing0 driver has known vulnerabilities and is
flagged by Microsoft Defender. PawnIO is not bundled here — you install it yourself, once,
from its own site. Without it the app still runs and the vulnerable driver is never loaded;
you simply do not get CPU temperature.

## Supported versions

Only the current `main` branch is supported. There are no tagged releases yet.
