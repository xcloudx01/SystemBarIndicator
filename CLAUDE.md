# CLAUDE.md

Guidance for working in this repository.

## Overview

SystemBar Indicator — a small Windows 11 tray/taskbar system monitor (CPU, RAM, GPU,
disk, network usage + CPU/GPU temperature) with a pop-up history-graph overlay.
WinForms + GDI+ on .NET 10. Hardware temperatures via LibreHardwareMonitorLib.

## Solution layout

```
SystemBarIndicator.sln
├── src/
│   ├── SystemBarIndicator.Core/   # net10.0 — платформонезависимое ядро (без WinForms)
│   │   ├── Models/                #   SensorData, DataPoint, DataHistory
│   │   ├── Monitoring/            #   LoadLevel + пороги (LoadLevels)
│   │   └── Util/                  #   Formatting
│   └── SystemBarIndicator.App/    # net10.0-windows — WinForms UI + Windows-сенсоры (WinExe)
│       ├── Program.cs             #   MainForm: бар на панели задач, отрисовка, трей
│       ├── GraphOverlay.cs        #   всплывающие графики
│       ├── Sensors/               #   Cpu/Ram/Disk/Network/Hardware (PerfCounter, P/Invoke, LHM)
│       └── Logging/Logger.cs      #   файловый логгер
└── tests/
    └── SystemBarIndicator.Tests/  # net10.0 — xUnit, ссылается только на Core
```

## Commands

```bash
dotnet build -c Release        # сборка (TreatWarningsAsErrors — падает на предупреждениях)
dotnet test  -c Release        # юнит-тесты (xUnit)
dotnet run --project src/SystemBarIndicator.App   # запуск приложения
```

## Architecture notes

- **Core vs App.** Любая логика без зависимости от WinForms/железа живёт в `Core` и
  покрывается тестами. UI и доступ к ОС (P/Invoke, PerformanceCounter, LHM) — в `App`.
  Новые тестируемые правила (форматирование, пороги, агрегации) добавляйте в `Core`.
- **Опрос данных.** `MainForm._timer` (1 c) обновляет единый снимок `SensorData _current`
  через классы `Sensors`, затем кладёт точку в `DataHistory` (кольцевой буфер на 120 точек).
  `HardwareSensor.Update()` обязателен раз за тик — без него LHM отдаёт пустые значения.
- **Температуры и PawnIO.** LHM 0.9.6 читает датчики CPU через подписанный драйвер ядра
  **PawnIO** (вместо уязвимого WinRing0, который помечает Microsoft Defender). PawnIO ставится
  отдельно (pawnio.eu); без него температура CPU недоступна, но уязвимый драйвер не грузится.
- **Один экземпляр.** `Program.Main` удерживает именованный `Mutex`
  (`Local\SystemBarIndicator_SingleInstance`); повторный запуск сразу завершается.
- **Always-on-top.** Удержание поверх — через модификацию z-order в `WM_WINDOWPOSCHANGING`
  (без рекурсивного `SetWindowPos`); таймер 1 c — лишь страховка.
- **Позиционирование.** `SHAppBarMessage(ABM_GETTASKBARPOS)` определяет прямоугольник и край
  панели задач; `MainForm.BarEdge` управляет тем, всплывает оверлей над баром или под ним.
- **Рисование.** GDI+ ресурсы (шрифты/кисти/перья) кэшируются (общие — `static`, цветные —
  по графику), история берётся одним снимком на кадр.
- **Диск/сеть в полоске.** Показаны парой баров (▼ чтение/загрузка, ▲ запись/отдача)
  фиксированной ширины. Рваные мгновенные значения сглаживаются `Ema`, полная шкала —
  адаптивный `AutoScale` по недавнему пику (оба класса в `Core`, покрыты тестами).
  Общий метод отрисовки — `DrawDualBars`.

## Conventions

- `nullable` включён везде; `TreatWarningsAsErrors=true` (см. `Directory.Build.props`).
- Сборка должна оставаться без предупреждений.
- Логи приложения: `%LOCALAPPDATA%\SystemBarIndicator\app.log`.
- `ROADMAP.md` / `SUGGESTIONS.md` — опережающие планы, а не описание текущего кода.
- **Развёртывание для рабочего стола.** `LibreHardwareMonitorLib` 0.9.6 — RID-split пакет
  (реальные сборки в `runtimes/<rid>/lib/<tfm>/`). Чтобы получить чистую плоскую раскладку
  одной версии, публикуйте с RID в отдельную папку (вне OneDrive):
  `dotnet publish src/SystemBarIndicator.App -c Release -r win-x64 --self-contained false -p:PublishSingleFile=false -o <папка>`.
  Обычный `bin` лежит в OneDrive и его содержимое может «уплывать» — на него не опираемся.
