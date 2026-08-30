# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this
project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- VRAM temperature: shown in the bar next to the GPU temperature (under a dim `VRAM`
  label), as a ninth chart in the history overlay, and as its own section checkbox in the
  settings. Available where LibreHardwareMonitor exposes a GPU memory temperature sensor
  (NVIDIA "GPU Memory Junction", AMD "GPU Memory"); hidden otherwise.

## [1.0.0] - 2026-08-10

First public release. The feature set below grew over the project's private history; the
individual steps are in the commit log.

### Added

- Taskbar bar with CPU, RAM, GPU, disk and network sections plus CPU and GPU temperature,
  coloured by load level.
- History overlay with eight charts over the last ~120 samples — click the bar to open it.
  It follows the bar around the screen and flips to open downwards near the top edge.
- Smoothed disk and network dual bars: an EMA over the raw counters and an auto-scaling
  full-scale value, both recalibrated when the poll interval changes.
- Fixed-width value slots and sensor availability latches, so the layout never jumps when a
  number changes width or a sensor drops out for a tick.
- Content-driven bar width, measured every frame so it stays correct under PerMonitorV2 DPI.
- Appearance settings applied live: theme, opacity, font scale, section visibility, poll
  interval and °C/°F, stored in `%LOCALAPPDATA%\SystemBarIndicator\settings.json`.
- Position persistence, including recovery when the saved spot is no longer on screen
  (monitor unplugged, resolution changed).
- Tray icon with a load tooltip and a context menu; the same menu opens on a right-click on
  the bar itself.
- Always-on-top by adjusting the z-order in `WM_WINDOWPOSCHANGING` rather than looping on
  `SetWindowPos`.
- Single-instance guard (named mutex).
- Hardware temperatures via LibreHardwareMonitorLib 0.9.6 and the signed PawnIO driver, so
  the vulnerable WinRing0 is never loaded.
- `deploy/Install.ps1` — a reproducible install that launches the app elevated **without** a
  UAC prompt on every start (scheduled task plus a silent launcher).
- Prebuilt `win-x64` binaries attached to the release, so running the app no longer requires
  the .NET SDK.
- Unit tests covering all of `Core`; CI builds and tests on `windows-latest` with warnings
  as errors.

[Unreleased]: https://github.com/coldneuro/SystemBarIndicator/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/coldneuro/SystemBarIndicator/releases/tag/v1.0.0
