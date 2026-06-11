# SystemBar Indicator

Системный монитор для Windows 11, закреплённый поверх области панели задач. Отображает в реальном времени загрузку CPU, RAM, GPU, диска и сети, а также температуру CPU и GPU. По клику разворачивает оверлей с графиками истории.

## Возможности

- **CPU** — общая загрузка и температура
- **RAM** — использование оперативной памяти (процент, занято/всего)
- **GPU** — загрузка и температура (NVIDIA / AMD / Intel), при наличии — объём VRAM
- **Диск** — чтение/запись (бар-индикатор со сглаживанием)
- **Сеть** — загрузка/отдача (бар-индикатор со сглаживанием)
- **Графики** — оверлей с историей последних ~120 измерений (плавные кривые)
- **Always on top** — окно удерживается поверх панели задач
- **Трей** — иконка с краткой сводкой и контекстным меню

## Технологический стек

| Слой | Технология |
|------|------------|
| Язык | C# |
| UI-фреймворк | Windows Forms |
| Runtime | .NET 10 (`net10.0-windows`) |
| Графика | GDI+ (`System.Drawing`) |
| Загрузка/RAM/диск/сеть | PerformanceCounter, `GlobalMemoryStatusEx`, `NetworkInterface` |
| Температуры и GPU | [LibreHardwareMonitorLib](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) 0.9.6 |
| Доступ к датчикам CPU | [PawnIO](https://pawnio.eu) — подписанный драйвер ядра (вместо уязвимого WinRing0) |

## Требования

- Windows 10 (Build 19041+) или Windows 11
- .NET 10 SDK
- Запуск с правами администратора — для чтения температур (загрузка драйвера датчиков)
- **PawnIO** — для температуры CPU. Это подписанный драйвер ядра, который LibreHardwareMonitor 0.9.5+
  использует вместо уязвимого WinRing0 (его помечает Microsoft Defender). Установить один раз
  с [pawnio.eu](https://pawnio.eu). Без PawnIO всё работает, кроме температуры CPU — и, что важно,
  уязвимый драйвер не загружается.

## Структура проекта

```
SystemBarIndicator/
├── src/
│   ├── SystemBarIndicator.Core/      # net10.0 — ядро без зависимостей от WinForms (тестируемое)
│   │   ├── Models/
│   │   │   ├── SensorData.cs         #   Текущий снимок показаний
│   │   │   └── DataHistory.cs        #   Кольцевая история измерений
│   │   ├── Monitoring/LoadLevel.cs   #   Пороги нагрузки/температуры
│   │   └── Util/Formatting.cs        #   Форматирование скоростей
│   └── SystemBarIndicator.App/       # net10.0-windows — WinForms UI + Windows-сенсоры
│       ├── Program.cs                #   Точка входа + MainForm (бар на панели задач)
│       ├── GraphOverlay.cs           #   Всплывающий оверлей с графиками
│       ├── Sensors/                  #   Источники данных
│       │   ├── CpuSensor.cs          #     Загрузка CPU (PerformanceCounter)
│       │   ├── RamSensor.cs          #     RAM (GlobalMemoryStatusEx)
│       │   ├── DiskSensor.cs         #     Диск (PerformanceCounter PhysicalDisk)
│       │   ├── NetworkSensor.cs      #     Сеть (NetworkInterface / IPv4-статистика)
│       │   └── HardwareSensor.cs     #     CPU/GPU температура и загрузка (LibreHardwareMonitor)
│       ├── Logging/Logger.cs         #   Файловый логгер
│       ├── app.manifest
│       └── SystemBarIndicator.csproj
├── tests/
│   └── SystemBarIndicator.Tests/     # net10.0 — xUnit, покрывают логику Core
├── .github/workflows/ci.yml          # Сборка + тесты (windows-latest, .NET 10)
├── Directory.Build.props             # Общие настройки (Nullable, TreatWarningsAsErrors)
├── README.md
├── ROADMAP.md                        # Долгосрочный план (опережает текущую реализацию)
└── SUGGESTIONS.md                    # Идеи и предложения
```

> Примечание: `ROADMAP.md` и `SUGGESTIONS.md` описывают желаемое развитие (в т.ч. возможный переход на WinUI 3, окно настроек, автообновление). Текущая реализация — то, что перечислено выше.

## Сборка, тесты и запуск

```bash
git clone <repository-url>
cd "SystemBarIndicator"
dotnet build -c Release          # сборка (предупреждения трактуются как ошибки)
dotnet test  -c Release          # юнит-тесты (xUnit)
dotnet run --project src/SystemBarIndicator.App   # запуск
```

Лог приложения пишется в `%LOCALAPPDATA%\SystemBarIndicator\app.log`.

## Лицензия

MIT
