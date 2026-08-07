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
│   │   ├── Configuration/         #   AppSettings, SettingsStore, WindowPlacement, ColorCodec
│   │   └── Util/                  #   Formatting, Ema, AutoScale, SmoothingRates
│   └── SystemBarIndicator.App/    # net10.0-windows — WinForms UI + Windows-сенсоры (WinExe)
│       ├── Program.cs             #   MainForm: бар на панели задач, отрисовка, трей
│       ├── GraphOverlay.cs        #   всплывающие графики
│       ├── SettingsForm.cs        #   окно настроек внешнего вида
│       ├── BarTheme.cs            #   сменяемые GDI+ ресурсы (тема + масштаб шрифта)
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
  панели задач; `MainForm.BarEdge` управляет тем, всплывает оверлей над баром или под ним
  (после перетаскивания пересчитывается по фактическому положению — `UpdateBarEdgeFromPosition`).
  Ширина окна не фиксирована: после обмера контента в `OnPaint` она подгоняется
  (`ApplyPendingBarWidth`, отложенно через `BeginInvoke` с коалесцированием), правый край
  при этом остаётся на месте; 520 px в конструкторе — лишь стартовая оценка.
- **Сохранение положения.** В настройках лежит **правый край** (`BarRight`), а не `Left`:
  ширина подгоняется под контент уже после первого кадра, а подгонка удерживает именно
  правый край — иначе бар при каждом запуске уползал бы. Позиция пишется **только** когда
  пользователь сам перетащил бар (`OnResizeEnd` + сравнение с сохранённой парой): если писать
  её всегда, бар навсегда перестанет следовать за панелью задач. `Reset Position` обнуляет
  `BarRight`/`BarTop`. Валидность точки проверяет `WindowPlacement` (Core, покрыт тестами):
  недостижимая позиция вдвигается в экран, а исправленная — сразу сохраняется.
  Смену конфигурации мониторов ловим сообщением `WM_DISPLAYCHANGE` в `WndProc`
  (а не `SystemEvents`: то приходит не в UI-поток и требует отписки).
- **Клик по бару.** Окно отдаёт `HTCAPTION` на любой hit-test, поэтому нажатие система сразу
  превращает в `WM_SYSCOMMAND`/`SC_MOVE`; клиентские `WM_LBUTTONDOWN/UP` не приходят вовсе,
  `WM_NCLBUTTONUP` съедается, а `WM_ENTERSIZEMOVE` на клике **без движения** не приходит.
  Поэтому жест разбирается в самом `SC_MOVE`: `base.WndProc` возвращает управление после
  отпускания кнопки, и если `Location` не изменился — это клик (открыть графики).
- **Рисование.** GDI+ ресурсы живут в `BarTheme` (шрифты и цвета, зависящие от настроек)
  и пересоздаются при смене темы/масштаба; неизменные цвета уровней нагрузки — `static`,
  цветные ресурсы графиков — по графику. История берётся одним снимком на кадр.
  Числовые значения (проценты, температуры) рисуются в слотах фиксированной ширины
  по шаблонам «100%»/«999C» с прижатием вправо (`DrawValueSlot`), диапазон значений гарантируют
  `Formatting.FormatPercent/FormatTemperature` (Core, покрыты тестами); секции CPU-temp/GPU
  показываются по «защёлкам» доступности датчика — компоновка не прыгает ни при смене
  числа разрядов, ни при кратковременном сбое датчика.
  **Ни ширину слота, ни высоту строки нельзя кэшировать между кадрами:** при PerMonitorV2
  `MeasureString` возвращает пиксели текущего монитора, поэтому и то и другое меряется
  в `OnPaint`. Разделитель рисуется **перед** секцией, если что-то уже нарисовано —
  иначе при выключенных секциях остались бы висящие палки.
- **Диск/сеть в полоске.** Показаны парой баров (▼ чтение/загрузка, ▲ запись/отдача)
  фиксированной ширины. Рваные мгновенные значения сглаживаются `Ema`, полная шкала —
  адаптивный `AutoScale` по недавнему пику (оба класса в `Core`, покрыты тестами).
  Общий метод отрисовки — `DrawDualBars`. Базовые коэффициенты (alpha 0.4, decay 0.97)
  откалиброваны под тик в 1 c; при смене интервала опроса они пересчитываются
  (`SmoothingRates`, Core) — иначе при 0.5 c индикаторы стали бы вдвое инертнее, а при 5 c рваными.
- **Настройки.** `%LOCALAPPDATA%\SystemBarIndicator\settings.json` (рядом с `app.log`;
  задание запускается от того же пользователя, поэтому путь не меняется от повышения прав).
  Загрузка никогда не бросает: битый JSON, `null`-документ или отсутствие файла дают дефолты,
  причина уходит в лог. Запись **атомарная** (`.tmp` + `File.Move(overwrite)`) и всегда
  сохраняет `Normalized()` — NaN/бесконечность в JSON недопустимы. `Normalized()` также
  чинит `null` в ненулевых строковых свойствах: аннотации nullable на десериализацию
  не влияют (`RespectNullableAnnotations` по умолчанию выключен).
  Сериализация — source-generated `JsonSerializerContext`: не обязательна сейчас
  (trim/AOT-анализаторы в проекте не включены и предупреждений нет), но останется
  корректной, если публикацию когда-нибудь переведут на trimming.
  Флаги секций — **автосвойства, а не поля**: поля `System.Text.Json` молча игнорирует.
  Запись на диск отложена (`_saveTimer`, 1.5 c) — перетаскивание и ползунки не должны
  устраивать шторм обращений; окно настроек применяет изменения сразу, а тему пересоздаёт
  только когда изменились влияющие на неё поля (`AppSettings.DiffersInAppearance`).
  `MainForm.ApplySettings` намеренно **игнорирует** `BarRight`/`BarTop` из входящего снимка:
  иначе открытое окно настроек затирало бы позицию своей устаревшей копией.

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
  Воспроизводимая установка целиком — `deploy/Install.ps1` (см. `deploy/README.md`).
- **Запуск от администратора без UAC.** Приложению нужны права админа (драйвер PawnIO),
  но манифест — `asInvoker`. Ярлык с флагом «запуск от администратора» спрашивал бы UAC
  каждый раз, поэтому используется **назначенное задание** (RunLevel=Highest), а ярлык
  запускает тихий `launch.vbs` → `schtasks /run` (запуск своего задания повышения не
  требует). Регистрация задания — единственный шаг с правами админа (`Install.ps1`
  самоповышается на нём).
- **Иконка.** Многоразмерная `src/SystemBarIndicator.App/appicon.ico` вшита и в exe
  (`ApplicationIcon`), и как `EmbeddedResource` — для трея/окна грузится в `LoadAppIcon()`.
- **Скрипты `.ps1`, перезапускаемые с повышением, держим в ASCII.** Кириллица в `.ps1`
  (UTF-8 без BOM), перечитанная `powershell.exe` как ANSI-кодировка, портится и однажды
  сломала задание (регистрация в папке-кракозябре). Тексты для elevated-перезапуска — ASCII.
