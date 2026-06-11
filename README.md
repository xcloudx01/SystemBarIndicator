# SystemBar Indicator

Системный монитор для Windows 11, закреплённый поверх области панели задач. Отображает в реальном времени загрузку CPU, RAM, GPU, диска и сети, а также температуру CPU и GPU. По клику разворачивает оверлей с графиками истории.

## Возможности

- **CPU** — общая загрузка и температура
- **RAM** — использование оперативной памяти (процент, занято/всего)
- **GPU** — загрузка и температура (NVIDIA / AMD / Intel), при наличии — объём VRAM
- **Диск** — скорость чтения/записи и общая активность
- **Сеть** — скорость загрузки и отдачи
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
| Температуры и GPU | [LibreHardwareMonitorLib](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) 0.9.4 |

## Требования

- Windows 10 (Build 19041+) или Windows 11
- .NET 10 SDK
- Для чтения температур CPU/GPU может потребоваться запуск с правами администратора (зависит от драйверов оборудования)

## Структура проекта

```
SystemBarIndicator/
├── src/
│   └── SystemBarIndicator.App/
│       ├── Program.cs              # Точка входа + MainForm (оверлей на панели задач)
│       ├── GraphOverlay.cs         # Всплывающий оверлей с графиками
│       ├── Sensors/                # Источники данных
│       │   ├── CpuSensor.cs        # Загрузка CPU (PerformanceCounter)
│       │   ├── RamSensor.cs        # RAM (GlobalMemoryStatusEx)
│       │   ├── DiskSensor.cs       # Диск (PerformanceCounter PhysicalDisk)
│       │   ├── NetworkSensor.cs    # Сеть (NetworkInterface / IPv4 статистика)
│       │   └── HardwareSensor.cs   # CPU/GPU температура и загрузка (LibreHardwareMonitor)
│       ├── Models/
│       │   ├── SensorData.cs       # Текущий снимок показаний
│       │   └── DataHistory.cs      # Кольцевая история измерений
│       ├── Logging/
│       │   └── Logger.cs           # Файловый логгер
│       ├── app.manifest
│       └── SystemBarIndicator.csproj
├── README.md
├── ROADMAP.md                      # Долгосрочный план (опережает текущую реализацию)
└── SUGGESTIONS.md                  # Идеи и предложения
```

> Примечание: `ROADMAP.md` и `SUGGESTIONS.md` описывают желаемое развитие (в т.ч. возможный переход на WinUI 3, окно настроек, автообновление). Текущая реализация — единый WinForms-проект, перечисленный выше.

## Сборка и запуск

```bash
git clone <repository-url>
cd "SystemBarIndicator"
dotnet build -c Release
dotnet run --project src/SystemBarIndicator.App
```

Лог приложения пишется в `%LOCALAPPDATA%\SystemBarIndicator\app.log`.

## Лицензия

MIT
