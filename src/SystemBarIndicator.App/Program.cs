using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SystemBarIndicator.Configuration;
using SystemBarIndicator.Models;
using SystemBarIndicator.Monitoring;
using SystemBarIndicator.Sensors;
using SystemBarIndicator.Util;

namespace SystemBarIndicator
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            // Один экземпляр на сессию: повторный запуск (в т.ч. двойной клик по ярлыку)
            // просто завершается, чтобы не появлялись две полоски.
            System.Threading.Mutex instanceLock;
            bool isNewInstance;
            try
            {
                instanceLock = new System.Threading.Mutex(initiallyOwned: true, @"Local\SystemBarIndicator_SingleInstance", out isNewInstance);
            }
            catch (UnauthorizedAccessException)
            {
                return; // экземпляр уже запущен (с другим уровнем прав)
            }

            using (instanceLock)
            {
                if (!isNewInstance)
                    return;

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
                Application.Run(new MainForm());
            }
        }
    }

    public class MainForm : Form
    {
        public enum TaskbarEdge { Bottom, Top }

        // --- Win32: управление окном ---
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("shell32.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct APPBARDATA
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uCallbackMessage;
            public uint uEdge;
            public RECT rc;
            public IntPtr lParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WINDOWPOS
        {
            public IntPtr hwnd;
            public IntPtr hwndInsertAfter;
            public int x;
            public int y;
            public int cx;
            public int cy;
            public uint flags;
        }

        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        private const uint ABM_GETTASKBARPOS = 0x00000005;
        private const uint ABE_TOP = 1;
        private const uint ABE_BOTTOM = 3;

        private const int WM_NCHITTEST = 0x0084;
        private const int HTCAPTION = 2;
        private const int WM_WINDOWPOSCHANGING = 0x0046;
        private const int WM_DISPLAYCHANGE = 0x007E;
        // Окно отдаёт HTCAPTION на любой hit-test, поэтому нажатие система сразу
        // превращает в SC_MOVE: клиентских WM_LBUTTONDOWN/UP не бывает, неклиентский
        // WM_NCLBUTTONUP съедается, а WM_ENTERSIZEMOVE на клике без движения не приходит
        // вовсе. Единственная точка, где виден весь жест целиком, — сам SC_MOVE.
        private const int WM_SYSCOMMAND = 0x0112;
        private const int SC_MOVE = 0xF010;

        // --- Геометрия полоски ---
        private const int EdgePad = 8;      // отступ от краёв окна
        private const int ValueGap = 10;    // после процентного значения
        private const int TempGap = 6;      // после температуры
        private const int SeparatorGap = 8; // после разделителя

        // Базовые коэффициенты сглаживания откалиброваны под тик в 1 секунду;
        // при другом интервале пересчитываются (SmoothingRates).
        private const float NetAlphaPerSecond = 0.4f;
        private const float DiskAlphaPerSecond = 0.4f;
        private const float ScaleDecayPerSecond = 0.97f;

        // Числовые значения рисуются в слоте фиксированной ширины с прижатием вправо,
        // чтобы секции не «дышали» при смене числа разрядов (9% ↔ 10%).
        private static readonly StringFormat ValueRightAligned = new StringFormat(StringFormat.GenericDefault)
        {
            Alignment = StringAlignment.Far,
            FormatFlags = StringFormatFlags.NoWrap
        };


        // --- Сенсоры ---
        private CpuSensor? _cpuSensor;
        private readonly RamSensor _ramSensor = new();
        private DiskSensor? _diskSensor;
        private NetworkSensor? _networkSensor;
        private HardwareSensor? _hardwareSensor;

        // --- Состояние и UI ---
        private readonly Timer _timer;
        private readonly Timer _topmostTimer;
        private readonly Timer _saveTimer;
        private readonly NotifyIcon _trayIcon;
        private readonly DataHistory _history;
        private GraphOverlay? _graphOverlay;
        private SettingsForm? _settingsForm;
        private readonly Icon _appIcon;

        private readonly SensorData _current = new();

        // --- Настройки ---
        private readonly string _settingsPath = SettingsStore.DefaultPath;
        private AppSettings _settings;
        private BarTheme _theme;
        private bool _settingsDirty;
        // Позиция считается заданной пользователем, только если он сам перетащил бар.
        // Иначе бар обязан следовать за панелью задач при каждом запуске.
        private bool _positionUserDefined;
        private bool _positionTrackingReady;

        // «Защёлки» доступности датчиков: однажды появившаяся секция больше не исчезает,
        // чтобы кратковременный сбой датчика (null → 0) не сдвигал компоновку.
        private bool _hasCpuTemp;
        private bool _hasGpu;
        private bool _hasGpuTemp;

        private int _pendingBarWidth; // 0 — подгонка ширины окна не запланирована

        // Сглаживание и автомасштаб для «рваных» по природе индикаторов сети и диска.
        private readonly Ema _netDownEma = new(NetAlphaPerSecond);
        private readonly Ema _netUpEma = new(NetAlphaPerSecond);
        private readonly AutoScale _netScale = new(floor: 64f, decay: ScaleDecayPerSecond);   // КБ/с
        private readonly Ema _diskReadEma = new(DiskAlphaPerSecond);
        private readonly Ema _diskWriteEma = new(DiskAlphaPerSecond);
        private readonly AutoScale _diskScale = new(floor: 8f, decay: ScaleDecayPerSecond);   // МБ/с

        public TaskbarEdge BarEdge { get; private set; } = TaskbarEdge.Bottom;

        /// <summary>Доступность датчиков — окно настроек помечает ими недоступные секции.</summary>
        public bool HasCpuTempSensor => _hasCpuTemp;
        public bool HasGpuSensor => _hasGpu;
        public bool HasGpuTempSensor => _hasGpuTemp;

        private SensorData GetCurrentData() => _current;

        public MainForm()
        {
            InitSensors();

            _history = new DataHistory(120);

            _settings = SettingsStore.Load(_settingsPath, out var settingsError);
            if (settingsError != null)
                Logger.Error("SettingsStore.Load", settingsError);
            _positionUserDefined = _settings.BarRight.HasValue && _settings.BarTop.HasValue;
            _theme = BarTheme.Create(_settings);

            _appIcon = LoadAppIcon();
            this.Icon = _appIcon;
            this.FormBorderStyle = FormBorderStyle.None;
            // Всё окно отдаёт HTCAPTION, поэтому двойной клик по нему система приняла бы
            // за двойной клик по заголовку и развернула окно — снимаем саму возможность.
            this.MaximizeBox = false;
            this.ShowInTaskbar = true;
            this.TopMost = true;
            this.BackColor = _theme.Background;
            this.Opacity = _settings.Opacity;
            this.DoubleBuffered = true;
            this.Size = new Size(520, 32);
            this.StartPosition = FormStartPosition.Manual;
            this.LocationChanged += (s, e) =>
            {
                if (_graphOverlay != null && !_graphOverlay.IsDisposed)
                    _graphOverlay.PositionAboveParent();
            };

            _timer = new Timer { Interval = _settings.UpdateIntervalMs };
            _timer.Tick += TimerTick;
            ApplySmoothingRates(_settings.UpdateIntervalMs);
            _timer.Start();

            // Событийное удержание поверх обеспечивает WM_WINDOWPOSCHANGING;
            // таймер — лишь редкая страховка (раньше был агрессивный интервал 200 мс).
            _topmostTimer = new Timer { Interval = 1000 };
            _topmostTimer.Tick += (s, e) => ForceTopMost();
            _topmostTimer.Start();

            // Запись настроек откладывается: перетаскивание бара и движение ползунка
            // не должны устраивать шторм обращений к диску.
            _saveTimer = new Timer { Interval = 1500 };
            _saveTimer.Tick += (s, e) => { _saveTimer.Stop(); FlushSettings(); };

            _trayIcon = new NotifyIcon
            {
                Visible = true,
                Text = "SystemBar Indicator",
                Icon = _appIcon
            };
            _trayIcon.MouseClick += TrayIconClick;

            var menu = new ContextMenuStrip();
            menu.Items.Add("Graphs", null, (s, e) => OpenGraphOverlay());
            menu.Items.Add("Settings...", null, (s, e) => OpenSettings());
            menu.Items.Add("Reset Position", null, (s, e) => ResetPosition());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, (s, e) => { _trayIcon.Visible = false; Application.Exit(); });
            _trayIcon.ContextMenuStrip = menu;
        }

        private void InitSensors()
        {
            try { _cpuSensor = new CpuSensor(); }
            catch (Exception ex) { Logger.Error("CpuSensor.init", ex); }

            try { _diskSensor = new DiskSensor(); }
            catch (Exception ex) { Logger.Error("DiskSensor.init", ex); }

            try { _networkSensor = new NetworkSensor(); }
            catch (Exception ex) { Logger.Error("NetworkSensor.init", ex); }

            try { _hardwareSensor = new HardwareSensor(); }
            catch (Exception ex) { Logger.Error("HardwareSensor.init", ex); }
        }

        // Иконка вшита в сборку как ресурс (см. EmbeddedResource в .csproj). Многоразмерная .ico:
        // Windows сам берёт подходящий кадр (16 px для трея, крупнее — для окна/панели задач).
        private static Icon LoadAppIcon()
        {
            using var stream = typeof(MainForm).Assembly
                .GetManifestResourceStream("SystemBarIndicator.appicon.ico");
            return stream != null ? new Icon(stream) : SystemIcons.Application;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            PositionOnTaskbar();
            TryRestoreSavedPosition();
            _positionTrackingReady = true;
            UpdateData();
        }

        private void PositionOnTaskbar()
        {
            // Фактическая ширина подгоняется под контент после первого обмера (ApplyBarWidth);
            // здесь используем текущую, чтобы «Reset Position» не возвращал стартовые 520.
            int barWidth = this.Width;
            const int margin = 10;

            var abd = new APPBARDATA { cbSize = (uint)Marshal.SizeOf<APPBARDATA>() };
            if (SHAppBarMessage(ABM_GETTASKBARPOS, ref abd) != IntPtr.Zero)
            {
                RECT rc = abd.rc;
                bool horizontal = abd.uEdge == ABE_TOP || abd.uEdge == ABE_BOTTOM;

                if (horizontal)
                {
                    int tbH = rc.Bottom - rc.Top;
                    int height = Math.Max(tbH - 4, 28);
                    this.Size = new Size(barWidth, height);
                    this.Location = new Point(rc.Right - barWidth - margin, rc.Top + (tbH - height) / 2);
                    BarEdge = abd.uEdge == ABE_TOP ? TaskbarEdge.Top : TaskbarEdge.Bottom;
                }
                else
                {
                    // Вертикальная панель задач (слева/справа, Windows 10):
                    // закрепляем у нижнего края рабочей области экрана.
                    var wa = SystemInformation.WorkingArea;
                    this.Size = new Size(barWidth, 30);
                    this.Location = new Point(wa.Right - barWidth - margin, wa.Bottom - 34);
                    BarEdge = TaskbarEdge.Bottom;
                }
            }
            else
            {
                int sw = GetSystemMetrics(SM_CXSCREEN);
                int sh = GetSystemMetrics(SM_CYSCREEN);
                this.Size = new Size(barWidth, 32);
                this.Location = new Point(sw - barWidth - margin, sh - 40);
                BarEdge = TaskbarEdge.Bottom;
            }

            ForceTopMost();
        }

        /// <summary>
        /// Возвращает бар туда, где пользователь его оставил. Восстанавливается ПРАВЫЙ край:
        /// ширина окна ещё стартовая и будет подогнана под контент после первого кадра,
        /// а подгонка (<see cref="ApplyPendingBarWidth"/>) удерживает именно правый край.
        /// Высоту не восстанавливаем — она считается по панели задач.
        /// </summary>
        private void TryRestoreSavedPosition()
        {
            if (!_settings.BarRight.HasValue || !_settings.BarTop.HasValue)
                return;

            var screens = ScreenBounds();
            var candidate = new Rectangle(_settings.BarRight.Value - this.Width, _settings.BarTop.Value, this.Width, this.Height);

            if (!WindowPlacement.IsUsable(candidate, screens))
            {
                var clamped = WindowPlacement.ClampToScreens(candidate, screens);
                if (!WindowPlacement.IsUsable(clamped, screens))
                {
                    Logger.Warn($"Сохранённая позиция бара недоступна ({candidate}), встаём по панели задач.");
                    return;
                }

                // Запоминаем починенную позицию, иначе в файле навсегда останется
                // недостижимая точка и клэмп будет повторяться при каждом запуске.
                Logger.Warn($"Сохранённая позиция бара скорректирована под текущие экраны: {candidate} → {clamped}.");
                candidate = clamped;
                _settings.BarRight = clamped.Right;
                _settings.BarTop = clamped.Top;
                MarkSettingsDirty();
            }

            this.Location = candidate.Location;
            UpdateBarEdgeFromPosition();
            ForceTopMost();
        }

        /// <summary>
        /// Куда раскрывать оверлей, определяем по фактическому положению бара: после
        /// перетаскивания в верхнюю половину экрана графики должны уходить вниз.
        /// </summary>
        private void UpdateBarEdgeFromPosition()
        {
            var area = Screen.FromRectangle(this.Bounds).WorkingArea;
            BarEdge = this.Top + this.Height / 2 < area.Top + area.Height / 2
                ? TaskbarEdge.Top
                : TaskbarEdge.Bottom;
        }

        private static Rectangle[] ScreenBounds()
        {
            var screens = Screen.AllScreens;
            var bounds = new Rectangle[screens.Length];
            for (int i = 0; i < screens.Length; i++)
                bounds[i] = screens[i].Bounds;
            return bounds;
        }

        private void ForceTopMost()
        {
            SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        private void TimerTick(object? sender, EventArgs e)
        {
            UpdateData();
        }

        private void UpdateData()
        {
            try
            {
                _current.CpuUsage = _cpuSensor?.GetUsage() ?? 0f;

                if (_hardwareSensor != null)
                {
                    _hardwareSensor.Update();
                    _current.CpuTemperature = _hardwareSensor.GetCpuTemperature() ?? 0f;
                    _current.GpuUsage = _hardwareSensor.GetGpuUsage() ?? 0f;
                    _current.GpuTemperature = _hardwareSensor.GetGpuTemperature() ?? 0f;

                    var gpuMem = _hardwareSensor.GetGpuMemory();
                    if (gpuMem.HasValue)
                    {
                        _current.GpuMemoryUsed = gpuMem.Value.used ?? 0f;
                        _current.GpuMemoryTotal = gpuMem.Value.total ?? 0f;
                    }
                }

                _hasCpuTemp |= _current.CpuTemperature > 0f;
                _hasGpu |= _current.GpuUsage > 0f || _current.GpuTemperature > 0f;
                _hasGpuTemp |= _current.GpuTemperature > 0f;

                var ram = _ramSensor.GetInfo();
                _current.RamUsage = ram.usagePercent;
                _current.RamUsed = ram.usedBytes;
                _current.RamTotal = ram.totalBytes;

                if (_diskSensor != null)
                {
                    var disk = _diskSensor.GetInfo();
                    _current.DiskRead = disk.readMBps;
                    _current.DiskWrite = disk.writeMBps;
                    _current.DiskUsagePercent = disk.usagePercent;
                }

                float dskR = _diskReadEma.Update(_current.DiskRead);
                float dskW = _diskWriteEma.Update(_current.DiskWrite);
                _diskScale.Update(Math.Max(dskR, dskW));

                if (_networkSensor != null)
                {
                    var net = _networkSensor.GetInfo();
                    _current.NetDown = net.downloadKBps;
                    _current.NetUp = net.uploadKBps;
                }

                // Сглаживаем мгновенную скорость и обновляем общую шкалу баров по пику.
                float netDl = _netDownEma.Update(_current.NetDown);
                float netUl = _netUpEma.Update(_current.NetUp);
                _netScale.Update(Math.Max(netDl, netUl));

                _trayIcon.Text = string.Format("CPU: {0:F0}% | RAM: {1:F0}% | GPU: {2:F0}%",
                    _current.CpuUsage, _current.RamUsage, _current.GpuUsage);

                this.Invalidate();

                _history.Add(new DataPoint
                {
                    Timestamp = DateTime.Now,
                    CpuUsage = _current.CpuUsage,
                    CpuTemperature = _current.CpuTemperature,
                    GpuUsage = _current.GpuUsage,
                    GpuTemperature = _current.GpuTemperature,
                    RamUsage = _current.RamUsage,
                    DiskRead = _current.DiskRead,
                    DiskWrite = _current.DiskWrite,
                    NetDown = _current.NetDown,
                    NetUp = _current.NetUp
                });
            }
            catch (Exception ex)
            {
                Logger.Error("UpdateData", ex);
            }
        }

        private void OpenGraphOverlay()
        {
            if (_graphOverlay == null || _graphOverlay.IsDisposed)
            {
                _graphOverlay = new GraphOverlay(_history, GetCurrentData, this, _theme, _settings);
                _graphOverlay.PositionAboveParent();
                _graphOverlay.FormClosed += (s, e) => { _graphOverlay = null; };
                _graphOverlay.Show();
            }
            else
            {
                _graphOverlay.Close();
                _graphOverlay = null;
            }
        }

        private void TrayIconClick(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                OpenGraphOverlay();
            }
        }

        // --- Настройки ---

        /// <summary>Копия текущих настроек — окно настроек правит её, не трогая рабочую.</summary>
        public AppSettings CurrentSettings => _settings.Clone();

        private void OpenSettings()
        {
            if (_settingsForm != null && !_settingsForm.IsDisposed)
            {
                _settingsForm.Activate();
                return;
            }

            // Владелец, а не TopMost: бар принудительно всплывает в топ z-order раз в секунду,
            // и обычное окно он бы перекрыл, а владение задаёт порядок надёжно.
            _settingsForm = new SettingsForm(this);
            _settingsForm.FormClosed += (s, e) => { _settingsForm = null; FlushSettings(); };
            _settingsForm.Show(this);
        }

        /// <summary>
        /// Применяет настройки внешнего вида. Положение бара сюда не входит:
        /// им управляют только перетаскивание и «Reset Position», иначе открытое окно
        /// настроек затирало бы позицию своим устаревшим снимком.
        /// </summary>
        public void ApplySettings(AppSettings incoming)
        {
            ArgumentNullException.ThrowIfNull(incoming);

            var next = incoming.Normalized();
            next.BarRight = _settings.BarRight;
            next.BarTop = _settings.BarTop;

            bool rebuildTheme = next.DiffersInAppearance(_settings);
            var previous = _settings;
            _settings = next;

            if (rebuildTheme)
            {
                // Сначала подменяем ссылку, потом освобождаем старые ресурсы: отрисовка
                // идёт в этом же потоке синхронно, недорисованного кадра быть не может.
                var oldTheme = _theme;
                _theme = BarTheme.Create(next);
                _graphOverlay?.ApplyTheme(_theme, next);
                oldTheme.Dispose();
                this.BackColor = _theme.Background;
            }
            else
            {
                _graphOverlay?.ApplyTheme(_theme, next);
            }

            if (this.Opacity != next.Opacity)
                this.Opacity = next.Opacity;

            if (previous.UpdateIntervalMs != next.UpdateIntervalMs)
            {
                _timer.Interval = next.UpdateIntervalMs;
                ApplySmoothingRates(next.UpdateIntervalMs);
            }

            this.Invalidate();
            MarkSettingsDirty();
        }

        /// <summary>
        /// Приводит сглаживание к новому шагу опроса: базовые коэффициенты откалиброваны
        /// под 1 секунду, иначе при 0.5 с индикаторы стали бы вдвое инертнее, а при 5 с — рваными.
        /// </summary>
        private void ApplySmoothingRates(int intervalMs)
        {
            float netAlpha = SmoothingRates.AlphaForInterval(NetAlphaPerSecond, intervalMs);
            float diskAlpha = SmoothingRates.AlphaForInterval(DiskAlphaPerSecond, intervalMs);
            float decay = SmoothingRates.DecayForInterval(ScaleDecayPerSecond, intervalMs);

            _netDownEma.Alpha = netAlpha;
            _netUpEma.Alpha = netAlpha;
            _diskReadEma.Alpha = diskAlpha;
            _diskWriteEma.Alpha = diskAlpha;
            _netScale.Decay = decay;
            _diskScale.Decay = decay;
        }

        private void MarkSettingsDirty()
        {
            _settingsDirty = true;
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        private void FlushSettings()
        {
            if (!_settingsDirty)
                return;

            _settingsDirty = false;
            try
            {
                SettingsStore.Save(_settingsPath, _settings);
            }
            catch (Exception ex)
            {
                Logger.Error("SettingsStore.Save", ex);
            }
        }

        // --- Положение бара ---

        private void ResetPosition()
        {
            _settings.BarRight = null;
            _settings.BarTop = null;
            _positionUserDefined = false;
            PositionOnTaskbar();
            MarkSettingsDirty();
            FlushSettings();
        }

        /// <summary>
        /// Конец перетаскивания. Позицию пишем только когда она действительно
        /// изменилась — иначе каждое касание бара тревожило бы диск.
        /// </summary>
        protected override void OnResizeEnd(EventArgs e)
        {
            base.OnResizeEnd(e);
            if (!_positionTrackingReady)
                return;

            int right = this.Left + this.Width;
            if (_settings.BarRight == right && _settings.BarTop == this.Top)
                return;

            _settings.BarRight = right;
            _settings.BarTop = this.Top;
            _positionUserDefined = true;
            UpdateBarEdgeFromPosition();
            MarkSettingsDirty();
        }

        /// <summary>
        /// Смена конфигурации мониторов: бар мог остаться на исчезнувшем экране.
        /// Если позицию не задавал пользователь — просто встаём обратно по панели задач.
        /// </summary>
        private void ReconcilePositionWithScreens()
        {
            if (this.IsDisposed || !this.IsHandleCreated)
                return;

            if (!_positionUserDefined)
            {
                PositionOnTaskbar();
                return;
            }

            var screens = ScreenBounds();
            if (!WindowPlacement.IsUsable(this.Bounds, screens))
            {
                var clamped = WindowPlacement.ClampToScreens(this.Bounds, screens);
                if (WindowPlacement.IsUsable(clamped, screens))
                {
                    this.Location = clamped.Location;
                    _settings.BarRight = this.Left + this.Width;
                    _settings.BarTop = this.Top;
                    MarkSettingsDirty();
                }
                else
                {
                    Logger.Warn("После смены конфигурации мониторов бар не помещается — возврат к панели задач.");
                    PositionOnTaskbar();
                    return;
                }
            }

            UpdateBarEdgeFromPosition();
        }

        // --- Отрисовка ---

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var s = _settings;

            // Высота строки меряется на текущем Graphics: при PerMonitorV2 она следует
            // за DPI монитора, на котором окно сейчас, — кэшировать её нельзя.
            int lineHeight = (int)Math.Ceiling(g.MeasureString("100%", _theme.MainFont).Height);
            int y = (this.Height - lineHeight) / 2;

            int x = EdgePad;
            int trailingGap = 0;
            bool anyDrawn = false;

            bool cpuTemp = s.ShowCpuTemp && _hasCpuTemp;
            if (s.ShowCpu || cpuTemp)
            {
                x = Separate(g, x, y, lineHeight, ref anyDrawn);
                if (s.ShowCpu)
                {
                    x = DrawSection(g, "CPU", Formatting.FormatPercent(_current.CpuUsage), _current.CpuUsage / 100f, x, y, lineHeight);
                    x += ValueGap;
                    trailingGap = ValueGap;
                }
                if (cpuTemp)
                {
                    x = DrawTemp(g, _current.CpuTemperature, x, y, lineHeight);
                    x += TempGap;
                    trailingGap = TempGap;
                }
            }

            if (s.ShowRam)
            {
                x = Separate(g, x, y, lineHeight, ref anyDrawn);
                x = DrawSection(g, "RAM", Formatting.FormatPercent(_current.RamUsage), _current.RamUsage / 100f, x, y, lineHeight);
                x += ValueGap;
                trailingGap = ValueGap;
            }

            bool gpu = s.ShowGpu && _hasGpu;
            bool gpuTemp = s.ShowGpuTemp && _hasGpuTemp;
            if (gpu || gpuTemp)
            {
                x = Separate(g, x, y, lineHeight, ref anyDrawn);
                if (gpu)
                {
                    x = DrawSection(g, "GPU", Formatting.FormatPercent(_current.GpuUsage), _current.GpuUsage / 100f, x, y, lineHeight);
                    x += ValueGap;
                    trailingGap = ValueGap;
                }
                if (gpuTemp)
                {
                    x = DrawTemp(g, _current.GpuTemperature, x, y, lineHeight);
                    x += TempGap;
                    trailingGap = TempGap;
                }
            }

            if (s.ShowDisk)
            {
                x = Separate(g, x, y, lineHeight, ref anyDrawn);
                x = DrawDualBars(g, "D", _diskScale.Fraction(_diskReadEma.Value), _diskScale.Fraction(_diskWriteEma.Value),
                    BarTheme.DiskRead, BarTheme.DiskWrite, x, y, lineHeight);
                trailingGap = 0;
            }

            if (s.ShowNetwork)
            {
                x = Separate(g, x, y, lineHeight, ref anyDrawn);
                x = DrawDualBars(g, "N", _netScale.Fraction(_netDownEma.Value), _netScale.Fraction(_netUpEma.Value),
                    BarTheme.NetDown, BarTheme.NetUp, x, y, lineHeight);
                trailingGap = 0;
            }

            // Ширина контента известна только после обмера в текущем DPI (слоты фиксированы,
            // но их пиксельная ширина зависит от монитора, а состав секций — от настроек
            // и «защёлок»). Подгоняем ширину окна вне цикла отрисовки, удерживая правый край.
            int desiredWidth = x - trailingGap + EdgePad;
            if (desiredWidth != this.Width && _pendingBarWidth != desiredWidth)
            {
                // Коалесцирование: в очереди один callback, применяется последний обмер
                // (при смене DPI устаревшее значение перезаписывается до применения).
                bool schedule = _pendingBarWidth == 0;
                _pendingBarWidth = desiredWidth;
                if (schedule)
                    BeginInvoke(new Action(ApplyPendingBarWidth));
            }
        }

        // Разделитель ставится ПЕРЕД секцией, если что-то уже нарисовано: при выключенных
        // секциях иначе остались бы висящие палки и лишние отступы по краям.
        private int Separate(Graphics g, int x, int y, int lineHeight, ref bool anyDrawn)
        {
            if (anyDrawn)
            {
                g.DrawLine(_theme.Separator, x, y - 2, x, y + lineHeight);
                x += SeparatorGap;
            }
            anyDrawn = true;
            return x;
        }

        // Меняет ширину окна, сохраняя положение правого края (бар прижат к правой
        // стороне панели задач, расти/сжиматься должен влево).
        private void ApplyPendingBarWidth()
        {
            int width = _pendingBarWidth;
            _pendingBarWidth = 0;
            if (width <= 0 || width == this.Width || this.IsDisposed || !this.IsHandleCreated)
                return;

            int right = this.Left + this.Width;
            var target = new Rectangle(right - width, this.Top, width, this.Height);
            // Рост влево не должен утаскивать бар за левый край экрана.
            target = WindowPlacement.ClampTo(target, Screen.FromRectangle(target).Bounds);
            this.SetBounds(target.X, target.Y, target.Width, target.Height);
        }

        // Диск и сеть рисуем парой горизонтальных баров: ▼ чтение/загрузка, ▲ запись/отдача.
        // Значения сглажены (EMA), ширина баров фиксирована, у каждого ресурса свой
        // автомасштаб — поэтому ничего не «прыгает».
        private int DrawDualBars(Graphics g, string label, float frac1, float frac2, Brush color1, Brush color2, int x, int y, int lineHeight)
        {
            float scale = (float)_settings.FontScale;
            int barW = (int)Math.Round(34 * scale);
            int barH = (int)Math.Round(8 * scale);
            int barTop = y + (lineHeight - barH) / 2;

            g.DrawString(label, _theme.SmallFont, _theme.Dim, x, y);
            x += (int)g.MeasureString(label, _theme.SmallFont).Width + 4;

            x = DrawBar(g, x, barTop, barW, barH, frac1, color1, down: true, scale);
            x += 6;
            x = DrawBar(g, x, barTop, barW, barH, frac2, color2, down: false, scale);

            return x;
        }

        private int DrawBar(Graphics g, int x, int top, int w, int h, float fraction, Brush color, bool down, float scale)
        {
            float ts = 3f * scale;
            float cx = x + ts;
            float cy = top + h / 2f;
            PointF[] tri = down
                ? new[] { new PointF(cx - ts, cy - ts), new PointF(cx + ts, cy - ts), new PointF(cx, cy + ts) }
                : new[] { new PointF(cx - ts, cy + ts), new PointF(cx + ts, cy + ts), new PointF(cx, cy - ts) };
            g.FillPolygon(color, tri);
            x += (int)(ts * 2) + 4;

            FillRoundRect(g, _theme.Track, x, top, w, h, 3f);
            float fillW = w * fraction;
            if (fillW >= 1f)
                FillRoundRect(g, color, x, top, fillW, h, 3f);

            return x + w;
        }

        private static void FillRoundRect(Graphics g, Brush brush, float x, float y, float w, float h, float r)
        {
            if (w <= 0f || h <= 0f) return;
            r = Math.Min(r, Math.Min(w, h) / 2f);
            using var path = new System.Drawing.Drawing2D.GraphicsPath();
            if (r <= 0.5f)
            {
                path.AddRectangle(new RectangleF(x, y, w, h));
            }
            else
            {
                float d = r * 2f;
                path.AddArc(x, y, d, d, 180, 90);
                path.AddArc(x + w - d, y, d, d, 270, 90);
                path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
                path.AddArc(x, y + h - d, d, d, 90, 90);
                path.CloseFigure();
            }
            g.FillPath(brush, path);
        }

        private int DrawSection(Graphics g, string label, string value, float loadPercent, int x, int y, int lineHeight)
        {
            Brush loadBrush = GetLoadBrush(loadPercent);

            g.DrawString(label, _theme.SmallFont, _theme.Dim, x, y);
            SizeF ls = g.MeasureString(label, _theme.SmallFont);
            x += (int)ls.Width + 2;

            // Слот по самому широкому значению: правый край и знак «%» неподвижны.
            return DrawValueSlot(g, value, "100%", loadBrush, x, y, lineHeight);
        }

        private int DrawTemp(Graphics g, float celsius, int x, int y, int lineHeight)
        {
            // Цвет всегда считается по Цельсию: пороги LoadLevels заданы в нём,
            // единицы отображения на цветовую индикацию влиять не должны.
            bool fahrenheit = _settings.UseFahrenheit;
            string text = Formatting.FormatTemperature(celsius, fahrenheit);
            string template = Formatting.TemperatureSlotTemplate(fahrenheit);
            return DrawValueSlot(g, text, template, GetTempBrush(celsius), x, y, lineHeight);
        }

        // Рисует значение в слоте фиксированной ширины (по шаблону) с прижатием вправо.
        // Ширина слота измеряется на текущем Graphics: при PerMonitorV2 она следует за DPI,
        // поэтому пиксельную ширину нельзя кэшировать между кадрами.
        private int DrawValueSlot(Graphics g, string value, string template, Brush brush, int x, int y, int lineHeight)
        {
            int slotW = (int)Math.Ceiling(g.MeasureString(template, _theme.MainFont).Width);
            var rect = new RectangleF(x, y, slotW, lineHeight);
            g.DrawString(value, _theme.MainFont, brush, rect, ValueRightAligned);
            return x + slotW;
        }

        private static Brush BrushFor(LoadLevel level) => level switch
        {
            LoadLevel.Critical => BarTheme.Red,
            LoadLevel.High => BarTheme.Orange,
            LoadLevel.Elevated => BarTheme.Yellow,
            _ => BarTheme.Green
        };

        private static Brush GetLoadBrush(float fraction) => BrushFor(LoadLevels.FromFraction(fraction));

        private static Brush GetTempBrush(float celsius) => BrushFor(LoadLevels.FromTemperature(celsius));

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_SYSCOMMAND && ((long)m.WParam & 0xFFF0) == SC_MOVE)
            {
                // Нажатие по бару система превращает в SC_MOVE. Обработчик по умолчанию
                // возвращает управление только после отпускания кнопки, поэтому здесь же
                // видно, чем жест оказался: бар не сдвинулся — это клик (открыть графики).
                var before = this.Location;
                base.WndProc(ref m);
                if (this.Location == before)
                    OpenGraphOverlay();
                return;
            }

            if (m.Msg == WM_NCHITTEST)
            {
                m.Result = (IntPtr)HTCAPTION;
                return;
            }

            if (m.Msg == WM_DISPLAYCHANGE)
            {
                base.WndProc(ref m);
                // Отложенно: к моменту обработки система уже обновит список мониторов.
                if (this.IsHandleCreated && !this.IsDisposed)
                    BeginInvoke(new Action(ReconcilePositionWithScreens));
                return;
            }

            if (m.Msg == WM_WINDOWPOSCHANGING)
            {
                // Удерживаем окно поверх остальных, модифицируя z-order в самом сообщении.
                // Это исключает рекурсию (повторный вызов SetWindowPos из обработчика).
                var wp = Marshal.PtrToStructure<WINDOWPOS>(m.LParam);
                wp.hwndInsertAfter = HWND_TOPMOST;
                wp.flags &= ~SWP_NOZORDER;
                Marshal.StructureToPtr(wp, m.LParam, false);
            }

            base.WndProc(ref m);
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            ForceTopMost();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _timer.Stop();
            _topmostTimer.Stop();
            _saveTimer.Stop();
            FlushSettings();

            if (_settingsForm != null && !_settingsForm.IsDisposed)
                _settingsForm.Close();

            if (_graphOverlay != null && !_graphOverlay.IsDisposed)
                _graphOverlay.Close();

            _cpuSensor?.Dispose();
            _diskSensor?.Dispose();
            _hardwareSensor?.Dispose();

            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _appIcon.Dispose();

            _timer.Dispose();
            _topmostTimer.Dispose();
            _saveTimer.Dispose();
            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            // Шрифты и кисти освобождаем только когда окно уже уничтожено: между
            // FormClosing и FormClosed ещё может прийти WM_PAINT.
            _theme.Dispose();
            base.OnFormClosed(e);
        }
    }
}
