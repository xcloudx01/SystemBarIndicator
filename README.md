# SystemBar Indicator

Системный монитор для Windows 11, интегрированный в панель задач. Отображает загрузку CPU, GPU, RAM, диска, сети, а также температуру CPU и GPU в реальном времени.

## Возможности

- **CPU** — загрузка по ядрам и общая, температура
- **GPU** — загрузка, температура, использование VRAM
- **RAM** — использование оперативной памяти
- **Диск** — чтение/запись, загрузка
- **Сеть** — скорость загрузки и отдачи
- **Температуры** — CPU и GPU в реальном времени
- **Always on top** — всегда поверх панели задач
- **Нативный вид** — совпадает со стилем панели задач Windows 11

## Технологический стек

| Слой | Технология |
|------|------------|
| Язык | C# 13 |
| Фреймворк | WinUI 3 (Windows App SDK) |
| Runtime | .NET 10.0 |
| Графика | Win32 GDI+ |
| Мониторинг | WMI, Performance Counters, OpenHardwareMonitorLib |
| Хранение настроек | JSON |

## Требования

- Windows 11 (рекомендуется) или Windows 10 (Build 19041+)
- .NET 10.0 SDK
- Visual Studio 2022 (17.10+) с рабочей нагрузкой "Windows App Development"

## Структура проекта

```
SystemBarIndicator/
├── src/
│   ├── SystemBarIndicator.App/          # Главное приложение
│   │   ├── App.xaml / App.xaml.cs
│   │   ├── MainWindow.xaml / .cs        # Окно-оверлей панели задач
│   │   └── SystemTray/                  # Иконка в системном трее
│   ├── SystemBarIndicator.Core/         # Ядро мониторинга
│   │   ├── Sensors/
│   │   │   ├── CpuSensor.cs
│   │   │   ├── GpuSensor.cs
│   │   │   ├── RamSensor.cs
│   │   │   ├── DiskSensor.cs
│   │   │   ├── NetworkSensor.cs
│   │   │   └── TemperatureSensor.cs
│   │   ├── SensorManager.cs             # Диспетчер сенсоров
│   │   └── Models/                      # Модели данных
│   ├── SystemBarIndicator.UI/           # Компоненты интерфейса
│   │   ├── TaskbarWidget/               # Виджет на панели задач
│   │   ├── SettingsWindow/              # Окно настроек
│   │   └── Themes/                      # Темы оформления
│   └── SystemBarIndicator.Services/     # Сервисы
│       ├── ConfigService.cs             # Настройки
│       └── UpdateService.cs             # Автообновление
├── tests/
├── docs/
├── assets/
├── README.md
├── ROADMAP.md
└── SUGGESTIONS.md
```

## Сборка

```bash
git clone <repository-url>
cd SystemBarIndicator
dotnet build -c Release
```

## Лицензия

MIT
