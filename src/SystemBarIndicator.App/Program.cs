using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Collections.Generic;
using System.Linq;
using LibreHardwareMonitor.Hardware;
using SystemBarIndicator.Models;

namespace SystemBarIndicator
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.Run(new MainForm());
        }
    }

    public class MainForm : Form
    {
        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        private const int WM_NCHITTEST = 0x0084;
        private const int HTCAPTION = 2;
        private const int WM_WINDOWPOSCHANGING = 0x0046;

        private PerformanceCounter _cpuCounter;
        private List<PerformanceCounter> _diskReadCounters;
        private List<PerformanceCounter> _diskWriteCounters;
        private Dictionary<string, long> _prevBytesReceived = new Dictionary<string, long>();
        private Dictionary<string, long> _prevBytesSent = new Dictionary<string, long>();
        private DateTime _lastNetCheck = DateTime.Now;

        private Computer? _lhmComputer;
        private bool _lhmAvailable;

        private Timer _timer;
        private Timer _topmostTimer;
        private NotifyIcon _trayIcon;
        private string _statusText = "Loading...";
        private DataHistory _history;
        private GraphOverlay? _graphOverlay;

        private float _cpuUsage;
        private float _cpuTemp;
        private float _gpuUsage;
        private float _gpuTemp;
        private float _ramUsage;
        private float _diskRead;
        private float _diskWrite;
        private float _netDown;
        private float _netUp;

        private SensorData GetCurrentData()
        {
            return new SensorData
            {
                CpuUsage = _cpuUsage,
                CpuTemperature = _cpuTemp,
                GpuUsage = _gpuUsage,
                GpuTemperature = _gpuTemp,
                RamUsage = _ramUsage,
                DiskRead = _diskRead,
                DiskWrite = _diskWrite,
                NetDown = _netDown,
                NetUp = _netUp
            };
        }

        public MainForm()
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _cpuCounter.NextValue();

            _diskReadCounters = new List<PerformanceCounter>();
            _diskWriteCounters = new List<PerformanceCounter>();
            try
            {
                var diskCat = new PerformanceCounterCategory("PhysicalDisk");
                foreach (var inst in diskCat.GetInstanceNames())
                {
                    if (!inst.Equals("_total", StringComparison.OrdinalIgnoreCase))
                    {
                        _diskReadCounters.Add(new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", inst));
                        _diskWriteCounters.Add(new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", inst));
                    }
                }
                foreach (var c in _diskReadCounters.Concat(_diskWriteCounters))
                    c.NextValue();
            }
            catch { }

            InitNetworkCounter();

            InitLibreHardwareMonitor();

            _history = new DataHistory(120);

            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = true;
            this.TopMost = true;
            this.BackColor = Color.FromArgb(25, 25, 25);
            this.Opacity = 0.92;
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

            _topmostTimer = new Timer { Interval = 200 };
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

        private void InitNetworkCounter()
        {
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus == OperationalStatus.Up &&
                        ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                        ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                    {
                        try
                        {
                            var stats = ni.GetIPv4Statistics();
                            _prevBytesReceived[ni.Id] = stats.BytesReceived;
                            _prevBytesSent[ni.Id] = stats.BytesSent;
                        }
                        catch { }
                    }
                }
                _lastNetCheck = DateTime.Now;
            }
            catch { }
        }

        private void InitLibreHardwareMonitor()
        {
            try
            {
                _lhmComputer = new Computer
                {
                    IsCpuEnabled = true,
                    IsGpuEnabled = true,
                    IsMemoryEnabled = false,
                    IsMotherboardEnabled = false,
                    IsControllerEnabled = false,
                    IsNetworkEnabled = false,
                    IsStorageEnabled = false
                };
                _lhmComputer.Open();
                _lhmAvailable = true;
            }
            catch
            {
                _lhmAvailable = false;
                _lhmComputer = null;
            }
        }

        private float? GetLhmValue(HardwareType hwType, SensorType sensorType, Func<string, bool>? nameFilter = null)
        {
            if (_lhmComputer == null) return null;
            foreach (var hw in _lhmComputer.Hardware)
            {
                if (hw.HardwareType != hwType) continue;
                hw.Update();
                foreach (var sensor in hw.Sensors)
                {
                    if (sensor.SensorType != sensorType) continue;
                    if (nameFilter != null && !nameFilter(sensor.Name)) continue;
                    if (sensor.Value.HasValue) return sensor.Value.Value;
                }
            }
            return null;
        }

        private Icon CreateIcon()
        {
            var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                using (var b = new SolidBrush(Color.FromArgb(0, 120, 212)))
                    g.FillEllipse(b, 1, 1, 14, 14);
                using (var f = new Font("Arial", 7f, FontStyle.Bold))
                using (var tb = new SolidBrush(Color.White))
                    g.DrawString("S", f, tb, 4, 3);
            }
            return Icon.FromHandle(bmp.GetHicon());
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            PositionOnTaskbar();
            UpdateData();
        }

        private void PositionOnTaskbar()
        {
            IntPtr taskbar = FindWindow("Shell_TrayWnd", "");
            int sw = GetSystemMetrics(SM_CXSCREEN);

            if (taskbar != IntPtr.Zero)
            {
                RECT r;
                GetWindowRect(taskbar, out r);
                int th = r.Bottom - r.Top;
                this.Location = new Point(sw - 530, r.Top + 2);
                this.Size = new Size(520, Math.Max(th - 4, 28));
            }
            else
            {
                int sh = GetSystemMetrics(SM_CYSCREEN);
                this.Location = new Point(sw - 530, sh - 40);
                this.Size = new Size(520, 32);
            }

            ForceTopMost();
        }

        private void ForceTopMost()
        {
            SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        private void TimerTick(object sender, EventArgs e)
        {
            UpdateData();
        }

        private void UpdateData()
        {
            try
            {
                _cpuUsage = _cpuCounter.NextValue();

                if (_lhmAvailable)
                {
                    _cpuTemp = GetLhmValue(HardwareType.Cpu, SensorType.Temperature,
                        n => n.Contains("Core") || n.Contains("Package") || n.Contains("CPU")) ?? 0;
                    _gpuUsage = GetLhmValue(HardwareType.GpuNvidia, SensorType.Load,
                        n => n.Contains("GPU")) ??
                        GetLhmValue(HardwareType.GpuAmd, SensorType.Load,
                        n => n.Contains("GPU")) ??
                        GetLhmValue(HardwareType.GpuIntel, SensorType.Load,
                        n => n.Contains("GPU")) ?? 0;
                    _gpuTemp = GetLhmValue(HardwareType.GpuNvidia, SensorType.Temperature) ??
                        GetLhmValue(HardwareType.GpuAmd, SensorType.Temperature) ??
                        GetLhmValue(HardwareType.GpuIntel, SensorType.Temperature) ?? 0;
                }

                var mem = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX)) };
                GlobalMemoryStatusEx(ref mem);
                _ramUsage = mem.dwMemoryLoad;

                float dr = 0, dw = 0;
                for (int i = 0; i < _diskReadCounters.Count; i++)
                {
                    dr += _diskReadCounters[i].NextValue();
                    dw += _diskWriteCounters[i].NextValue();
                }
                _diskRead = dr / 1048576f;
                _diskWrite = dw / 1048576f;

                _netDown = 0;
                _netUp = 0;
                try
                {
                    var now = DateTime.Now;
                    var elapsed = (now - _lastNetCheck).TotalSeconds;
                    long totalDown = 0, totalUp = 0;

                    foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                    {
                        if (ni.OperationalStatus == OperationalStatus.Up &&
                            ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                            ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                        {
                            try
                            {
                                var stats = ni.GetIPv4Statistics();
                                string id = ni.Id;

                                if (_prevBytesReceived.ContainsKey(id))
                                {
                                    long diffRecv = stats.BytesReceived - _prevBytesReceived[id];
                                    long diffSent = stats.BytesSent - _prevBytesSent[id];
                                    if (diffRecv > 0) totalDown += diffRecv;
                                    if (diffSent > 0) totalUp += diffSent;
                                }

                                _prevBytesReceived[id] = stats.BytesReceived;
                                _prevBytesSent[id] = stats.BytesSent;
                            }
                            catch { }
                        }
                    }

                    if (elapsed > 0)
                    {
                        _netDown = (float)(totalDown / 1024.0 / elapsed);
                        _netUp = (float)(totalUp / 1024.0 / elapsed);
                    }
                    _lastNetCheck = now;
                }
                catch { }

                var parts = new List<string>();
                parts.Add(string.Format("CPU {0:F0}%", _cpuUsage));
                if (_cpuTemp > 0) parts.Add(string.Format("{0:F0}C", _cpuTemp));
                parts.Add(string.Format("RAM {0:F0}%", _ramUsage));
                if (_gpuUsage > 0 || _gpuTemp > 0)
                {
                    parts.Add(string.Format("GPU {0:F0}%", _gpuUsage));
                    if (_gpuTemp > 0) parts.Add(string.Format("{0:F0}C", _gpuTemp));
                }
                parts.Add(string.Format("D {0:F0}/{1:F0}MB", _diskRead, _diskWrite));
                parts.Add(string.Format("N {0}/{1}", FormatSpeed(_netDown), FormatSpeed(_netUp)));

                _statusText = string.Join(" | ", parts);

                _trayIcon.Text = string.Format("CPU: {0:F0}% | RAM: {1:F0}% | GPU: {2:F0}%",
                    _cpuUsage, _ramUsage, _gpuUsage);

                this.Invalidate();

                _history.Add(new DataPoint
                {
                    Timestamp = DateTime.Now,
                    CpuUsage = _cpuUsage,
                    CpuTemperature = _cpuTemp,
                    GpuUsage = _gpuUsage,
                    GpuTemperature = _gpuTemp,
                    RamUsage = _ramUsage,
                    DiskRead = _diskRead,
                    DiskWrite = _diskWrite,
                    NetDown = _netDown,
                    NetUp = _netUp
                });
            }
            catch { }
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

        private void TrayIconClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                OpenGraphOverlay();
            }
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            OpenGraphOverlay();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            using (var mainFont = new Font("Segoe UI", 9f, FontStyle.Regular))
            using (var smallFont = new Font("Segoe UI", 7.5f, FontStyle.Regular))
            using (var dimBrush = new SolidBrush(Color.FromArgb(150, 150, 150)))
            {
                int x = 8;
                int y = (this.Height - 16) / 2;

                x = DrawSection(e.Graphics, "CPU", string.Format("{0:F0}%", _cpuUsage), _cpuUsage / 100f, x, y, mainFont, smallFont, dimBrush);
                if (_cpuTemp > 0)
                {
                    x = DrawTemp(e.Graphics, string.Format("{0:F0}C", _cpuTemp), _cpuTemp, x, y, mainFont);
                }
                x = DrawSeparator(e.Graphics, x, y);

                x = DrawSection(e.Graphics, "RAM", string.Format("{0:F0}%", _ramUsage), _ramUsage / 100f, x, y, mainFont, smallFont, dimBrush);
                x = DrawSeparator(e.Graphics, x, y);

                if (_gpuUsage > 0 || _gpuTemp > 0)
                {
                    x = DrawSection(e.Graphics, "GPU", string.Format("{0:F0}%", _gpuUsage), _gpuUsage / 100f, x, y, mainFont, smallFont, dimBrush);
                    if (_gpuTemp > 0)
                    {
                        x = DrawTemp(e.Graphics, string.Format("{0:F0}C", _gpuTemp), _gpuTemp, x, y, mainFont);
                    }
                    x = DrawSeparator(e.Graphics, x, y);
                }

                x = DrawSection(e.Graphics, "D", string.Format("{0:F0}/{1:F0}MB", _diskRead, _diskWrite), 0f, x, y, mainFont, smallFont, dimBrush);
                x = DrawSeparator(e.Graphics, x, y);
                x = DrawSection(e.Graphics, "N", string.Format("{0}/{1}", FormatSpeed(_netDown), FormatSpeed(_netUp)), 0f, x, y, mainFont, smallFont, dimBrush);
            }
        }

        private int DrawSection(Graphics g, string label, string value, float loadPercent, int x, int y, Font mainFont, Font smallFont, Brush dimBrush)
        {
            using (Brush loadBrush = GetLoadBrush(loadPercent))
            {
                g.DrawString(label, smallFont, dimBrush, x, y);
                SizeF ls = g.MeasureString(label, smallFont);
                x += (int)ls.Width + 2;

                g.DrawString(value, mainFont, loadBrush, x, y);
                SizeF vs = g.MeasureString(value, mainFont);
                x += (int)vs.Width + 10;

                return x;
            }
        }

        private int DrawTemp(Graphics g, string text, float temp, int x, int y, Font mainFont)
        {
            using (Brush tempBrush = GetTempBrush(temp))
            {
                g.DrawString(text, mainFont, tempBrush, x, y);
                SizeF ts = g.MeasureString(text, mainFont);
                return x + (int)ts.Width + 6;
            }
        }

        private int DrawSeparator(Graphics g, int x, int y)
        {
            using (var pen = new Pen(Color.FromArgb(60, 60, 60), 1))
                g.DrawLine(pen, x, y - 2, x, y + 16);
            return x + 8;
        }

        private Brush GetLoadBrush(float p)
        {
            if (p >= 0.9f) return new SolidBrush(Color.FromArgb(244, 67, 54));
            if (p >= 0.8f) return new SolidBrush(Color.FromArgb(255, 152, 0));
            if (p >= 0.6f) return new SolidBrush(Color.FromArgb(255, 193, 7));
            return new SolidBrush(Color.FromArgb(76, 175, 80));
        }

        private Brush GetTempBrush(float t)
        {
            if (t >= 90) return new SolidBrush(Color.FromArgb(244, 67, 54));
            if (t >= 75) return new SolidBrush(Color.FromArgb(255, 152, 0));
            if (t >= 60) return new SolidBrush(Color.FromArgb(255, 193, 7));
            return new SolidBrush(Color.FromArgb(76, 175, 80));
        }

        private string FormatSpeed(float kbps)
        {
            if (kbps >= 1024) return string.Format("{0:F1}GB", kbps / 1024f);
            if (kbps >= 1) return string.Format("{0:F0}KB", kbps);
            return string.Format("{0:F0}B", kbps * 1024);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCHITTEST)
            {
                m.Result = (IntPtr)HTCAPTION;
                return;
            }

            if (m.Msg == WM_WINDOWPOSCHANGING)
            {
                ForceTopMost();
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
            _cpuCounter.Dispose();
            foreach (var c in _diskReadCounters) c.Dispose();
            foreach (var c in _diskWriteCounters) c.Dispose();
            _prevBytesReceived.Clear();
            _prevBytesSent.Clear();
            if (_lhmComputer != null)
                _lhmComputer.Close();
            _trayIcon.Dispose();
            _timer.Dispose();
            _topmostTimer.Dispose();
            base.OnFormClosing(e);
        }
    }
}
