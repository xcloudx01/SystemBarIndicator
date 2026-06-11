using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
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

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr hIcon);

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
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;

        // --- Кэшированные ресурсы рисования (живут на всё время работы приложения) ---
        private static readonly Font MainFont = new Font("Segoe UI", 9f, FontStyle.Regular);
        private static readonly Font SmallFont = new Font("Segoe UI", 7.5f, FontStyle.Regular);
        private static readonly Brush DimBrush = new SolidBrush(Color.FromArgb(150, 150, 150));
        private static readonly Pen SeparatorPen = new Pen(Color.FromArgb(60, 60, 60), 1);
        private static readonly Brush BrushRed = new SolidBrush(Color.FromArgb(244, 67, 54));
        private static readonly Brush BrushOrange = new SolidBrush(Color.FromArgb(255, 152, 0));
        private static readonly Brush BrushYellow = new SolidBrush(Color.FromArgb(255, 193, 7));
        private static readonly Brush BrushGreen = new SolidBrush(Color.FromArgb(76, 175, 80));
        // Цвета сети совпадают с графиками в оверлее (Net DL / Net UL).
        private static readonly Brush NetDownBrush = new SolidBrush(Color.FromArgb(237, 137, 54));
        private static readonly Brush NetUpBrush = new SolidBrush(Color.FromArgb(38, 198, 218));
        // Диск — отдельная цветовая пара (фиолетовый/розовый), чтобы не путать с сетью.
        private static readonly Brush DiskReadBrush = new SolidBrush(Color.FromArgb(171, 71, 188));
        private static readonly Brush DiskWriteBrush = new SolidBrush(Color.FromArgb(236, 64, 122));
        private static readonly Brush NetTrackBrush = new SolidBrush(Color.FromArgb(55, 55, 62));

        private Point _mouseDownLocation;
        private bool _mouseDown;

        // --- Сенсоры ---
        private CpuSensor? _cpuSensor;
        private readonly RamSensor _ramSensor = new();
        private DiskSensor? _diskSensor;
        private NetworkSensor? _networkSensor;
        private HardwareSensor? _hardwareSensor;

        // --- Состояние и UI ---
        private readonly Timer _timer;
        private readonly Timer _topmostTimer;
        private readonly NotifyIcon _trayIcon;
        private readonly DataHistory _history;
        private GraphOverlay? _graphOverlay;
        private IntPtr _iconHandle = IntPtr.Zero;

        private readonly SensorData _current = new();

        // Сглаживание и автомасштаб для «рваных» по природе индикаторов сети и диска.
        private readonly Ema _netDownEma = new(0.4f);
        private readonly Ema _netUpEma = new(0.4f);
        private readonly AutoScale _netScale = new(floor: 64f, decay: 0.97f);   // КБ/с
        private readonly Ema _diskReadEma = new(0.4f);
        private readonly Ema _diskWriteEma = new(0.4f);
        private readonly AutoScale _diskScale = new(floor: 8f, decay: 0.97f);   // МБ/с

        public TaskbarEdge BarEdge { get; private set; } = TaskbarEdge.Bottom;

        private SensorData GetCurrentData() => _current;

        public MainForm()
        {
            InitSensors();

            _history = new DataHistory(120);

            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = true;
            this.TopMost = true;
            this.BackColor = Color.FromArgb(25, 25, 25);
            this.Opacity = 0.92;
            this.DoubleBuffered = true;
            this.Size = new Size(520, 32);
            this.StartPosition = FormStartPosition.Manual;
            this.LocationChanged += (s, e) =>
            {
                if (_graphOverlay != null && !_graphOverlay.IsDisposed)
                    _graphOverlay.PositionAboveParent();
            };

            _timer = new Timer { Interval = 1000 };
            _timer.Tick += TimerTick;
            _timer.Start();

            // Событийное удержание поверх обеспечивает WM_WINDOWPOSCHANGING;
            // таймер — лишь редкая страховка (раньше был агрессивный интервал 200 мс).
            _topmostTimer = new Timer { Interval = 1000 };
            _topmostTimer.Tick += (s, e) => ForceTopMost();
            _topmostTimer.Start();

            _trayIcon = new NotifyIcon
            {
                Visible = true,
                Text = "SystemBar Indicator",
                Icon = CreateIcon()
            };
            _trayIcon.MouseClick += TrayIconClick;

            var menu = new ContextMenuStrip();
            menu.Items.Add("Graphs", null, (s, e) => OpenGraphOverlay());
            menu.Items.Add("Reset Position", null, (s, e) => PositionOnTaskbar());
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

        private Icon CreateIcon()
        {
            using var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                using (var b = new SolidBrush(Color.FromArgb(0, 120, 212)))
                    g.FillEllipse(b, 1, 1, 14, 14);
                using (var f = new Font("Arial", 7f, FontStyle.Bold))
                using (var tb = new SolidBrush(Color.White))
                    g.DrawString("S", f, tb, 4, 3);
            }
            // Icon.FromHandle не владеет HICON — сохраняем хэндл и освобождаем его при закрытии.
            _iconHandle = bmp.GetHicon();
            return Icon.FromHandle(_iconHandle);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            PositionOnTaskbar();
            UpdateData();
        }

        private void PositionOnTaskbar()
        {
            const int barWidth = 520;
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
                this.Location = new Point(sw - 530, sh - 40);
                BarEdge = TaskbarEdge.Bottom;
            }

            ForceTopMost();
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
                _graphOverlay = new GraphOverlay(_history, GetCurrentData, this);
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

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int x = 8;
            int y = (this.Height - 16) / 2;

            x = DrawSection(e.Graphics, "CPU", string.Format("{0:F0}%", _current.CpuUsage), _current.CpuUsage / 100f, x, y);
            if (_current.CpuTemperature > 0)
            {
                x = DrawTemp(e.Graphics, string.Format("{0:F0}C", _current.CpuTemperature), _current.CpuTemperature, x, y);
            }
            x = DrawSeparator(e.Graphics, x, y);

            x = DrawSection(e.Graphics, "RAM", string.Format("{0:F0}%", _current.RamUsage), _current.RamUsage / 100f, x, y);
            x = DrawSeparator(e.Graphics, x, y);

            if (_current.GpuUsage > 0 || _current.GpuTemperature > 0)
            {
                x = DrawSection(e.Graphics, "GPU", string.Format("{0:F0}%", _current.GpuUsage), _current.GpuUsage / 100f, x, y);
                if (_current.GpuTemperature > 0)
                {
                    x = DrawTemp(e.Graphics, string.Format("{0:F0}C", _current.GpuTemperature), _current.GpuTemperature, x, y);
                }
                x = DrawSeparator(e.Graphics, x, y);
            }

            x = DrawDualBars(e.Graphics, "D", _diskScale.Fraction(_diskReadEma.Value), _diskScale.Fraction(_diskWriteEma.Value), DiskReadBrush, DiskWriteBrush, x, y);
            x = DrawSeparator(e.Graphics, x, y);
            x = DrawDualBars(e.Graphics, "N", _netScale.Fraction(_netDownEma.Value), _netScale.Fraction(_netUpEma.Value), NetDownBrush, NetUpBrush, x, y);
        }

        // Диск и сеть рисуем парой горизонтальных баров: ▼ чтение/загрузка, ▲ запись/отдача.
        // Значения сглажены (EMA), ширина баров фиксирована, у каждого ресурса свой
        // автомасштаб — поэтому ничего не «прыгает».
        private int DrawDualBars(Graphics g, string label, float frac1, float frac2, Brush color1, Brush color2, int x, int y)
        {
            const int barW = 34;
            const int barH = 8;
            int barTop = y + (16 - barH) / 2;

            g.DrawString(label, SmallFont, DimBrush, x, y);
            x += (int)g.MeasureString(label, SmallFont).Width + 4;

            x = DrawBar(g, x, barTop, barW, barH, frac1, color1, down: true);
            x += 6;
            x = DrawBar(g, x, barTop, barW, barH, frac2, color2, down: false);

            return x;
        }

        private int DrawBar(Graphics g, int x, int top, int w, int h, float fraction, Brush color, bool down)
        {
            const float ts = 3f;
            float cx = x + ts;
            float cy = top + h / 2f;
            PointF[] tri = down
                ? new[] { new PointF(cx - ts, cy - ts), new PointF(cx + ts, cy - ts), new PointF(cx, cy + ts) }
                : new[] { new PointF(cx - ts, cy + ts), new PointF(cx + ts, cy + ts), new PointF(cx, cy - ts) };
            g.FillPolygon(color, tri);
            x += (int)(ts * 2) + 4;

            FillRoundRect(g, NetTrackBrush, x, top, w, h, 3f);
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

        private int DrawSection(Graphics g, string label, string value, float loadPercent, int x, int y)
        {
            Brush loadBrush = GetLoadBrush(loadPercent);

            g.DrawString(label, SmallFont, DimBrush, x, y);
            SizeF ls = g.MeasureString(label, SmallFont);
            x += (int)ls.Width + 2;

            g.DrawString(value, MainFont, loadBrush, x, y);
            SizeF vs = g.MeasureString(value, MainFont);
            x += (int)vs.Width + 10;

            return x;
        }

        private int DrawTemp(Graphics g, string text, float temp, int x, int y)
        {
            Brush tempBrush = GetTempBrush(temp);
            g.DrawString(text, MainFont, tempBrush, x, y);
            SizeF ts = g.MeasureString(text, MainFont);
            return x + (int)ts.Width + 6;
        }

        private int DrawSeparator(Graphics g, int x, int y)
        {
            g.DrawLine(SeparatorPen, x, y - 2, x, y + 16);
            return x + 8;
        }

        private static Brush BrushFor(LoadLevel level) => level switch
        {
            LoadLevel.Critical => BrushRed,
            LoadLevel.High => BrushOrange,
            LoadLevel.Elevated => BrushYellow,
            _ => BrushGreen
        };

        private static Brush GetLoadBrush(float fraction) => BrushFor(LoadLevels.FromFraction(fraction));

        private static Brush GetTempBrush(float celsius) => BrushFor(LoadLevels.FromTemperature(celsius));

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_LBUTTONDOWN)
            {
                _mouseDown = true;
                _mouseDownLocation = this.PointToClient(Cursor.Position);
                base.WndProc(ref m);
                return;
            }

            if (m.Msg == WM_LBUTTONUP)
            {
                if (_mouseDown)
                {
                    var currentPos = this.PointToClient(Cursor.Position);
                    int dx = currentPos.X - _mouseDownLocation.X;
                    int dy = currentPos.Y - _mouseDownLocation.Y;
                    if (Math.Abs(dx) < 5 && Math.Abs(dy) < 5)
                    {
                        OpenGraphOverlay();
                    }
                }
                _mouseDown = false;
                base.WndProc(ref m);
                return;
            }

            if (m.Msg == WM_NCHITTEST)
            {
                m.Result = (IntPtr)HTCAPTION;
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

            if (_graphOverlay != null && !_graphOverlay.IsDisposed)
                _graphOverlay.Close();

            _cpuSensor?.Dispose();
            _diskSensor?.Dispose();
            _hardwareSensor?.Dispose();

            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            if (_iconHandle != IntPtr.Zero)
            {
                DestroyIcon(_iconHandle);
                _iconHandle = IntPtr.Zero;
            }

            _timer.Dispose();
            _topmostTimer.Dispose();
            base.OnFormClosing(e);
        }
    }
}
