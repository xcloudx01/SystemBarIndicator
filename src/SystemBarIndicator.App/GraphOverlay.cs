using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using SystemBarIndicator.Models;

namespace SystemBarIndicator
{
    public class GraphOverlay : Form
    {
        private const int ChartHeight = 72;
        private const int OuterPadding = 8;
        private const int Gap = 4;

        // Общие ресурсы рисования — одинаковы для всех графиков, живут на всё время работы.
        private static readonly Font TitleFont = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        private static readonly Brush TitleBrush = new SolidBrush(Color.FromArgb(150, 150, 170));
        private static readonly Font ValueFont = new Font("Segoe UI", 13f, FontStyle.Bold);
        private static readonly Brush CardBrush = new SolidBrush(Color.FromArgb(28, 28, 38));
        private static readonly Pen GridPen = new Pen(Color.FromArgb(38, 38, 52), 1);

        private readonly DataHistory _history;
        private readonly Func<SensorData> _getCurrentData;
        private readonly MainForm _parentForm;
        private readonly Timer _updateTimer;

        private class ChartConfig
        {
            public required string Title;
            public Color LineColor;
            public required Func<DataPoint, float> GetValue;
            public required Func<SensorData, string> FormatValue;
            public float MaxDefault;
            public bool ShowOnlyIfPositive;

            // Цветные ресурсы графика — создаются один раз, освобождаются в OnFormClosing.
            public SolidBrush LineBrush = null!;
            public Pen LinePen = null!;
            public Pen GlowPen = null!;
        }

        private readonly ChartConfig[] _charts;

        public GraphOverlay(DataHistory history, Func<SensorData> getCurrentData, MainForm parentForm)
        {
            _history = history;
            _getCurrentData = getCurrentData;
            _parentForm = parentForm;

            _charts = new ChartConfig[]
            {
                new ChartConfig { Title = "CPU", LineColor = Color.FromArgb(99, 179, 237), GetValue = p => p.CpuUsage, FormatValue = d => string.Format("{0:F0}%", d.CpuUsage), MaxDefault = 100, ShowOnlyIfPositive = false },
                new ChartConfig { Title = "CPU Temp", LineColor = Color.FromArgb(244, 143, 177), GetValue = p => p.CpuTemperature, FormatValue = d => string.Format("{0:F0}C", d.CpuTemperature), MaxDefault = 100, ShowOnlyIfPositive = true },
                new ChartConfig { Title = "RAM", LineColor = Color.FromArgb(129, 140, 248), GetValue = p => p.RamUsage, FormatValue = d => string.Format("{0:F0}%", d.RamUsage), MaxDefault = 100, ShowOnlyIfPositive = false },
                new ChartConfig { Title = "GPU", LineColor = Color.FromArgb(72, 187, 120), GetValue = p => p.GpuUsage, FormatValue = d => string.Format("{0:F0}%", d.GpuUsage), MaxDefault = 100, ShowOnlyIfPositive = true },
                new ChartConfig { Title = "GPU Temp", LineColor = Color.FromArgb(255, 167, 38), GetValue = p => p.GpuTemperature, FormatValue = d => string.Format("{0:F0}C", d.GpuTemperature), MaxDefault = 100, ShowOnlyIfPositive = true },
                new ChartConfig { Title = "Disk R/W", LineColor = Color.FromArgb(171, 71, 188), GetValue = p => p.DiskRead + p.DiskWrite, FormatValue = d => string.Format("{0:F0} / {1:F0} MB", d.DiskRead, d.DiskWrite), MaxDefault = 500, ShowOnlyIfPositive = false },
                new ChartConfig { Title = "Net DL", LineColor = Color.FromArgb(237, 137, 54), GetValue = p => p.NetDown, FormatValue = d => FormatSpeed(d.NetDown), MaxDefault = 10000, ShowOnlyIfPositive = false },
                new ChartConfig { Title = "Net UL", LineColor = Color.FromArgb(38, 198, 218), GetValue = p => p.NetUp, FormatValue = d => FormatSpeed(d.NetUp), MaxDefault = 10000, ShowOnlyIfPositive = false }
            };

            foreach (var c in _charts)
            {
                c.LineBrush = new SolidBrush(c.LineColor);
                c.LinePen = new Pen(c.LineColor, 1.5f) { LineJoin = LineJoin.Round };
                c.GlowPen = new Pen(Color.FromArgb(25, c.LineColor.R, c.LineColor.G, c.LineColor.B), 3.5f) { LineJoin = LineJoin.Round };
            }

            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.BackColor = Color.FromArgb(20, 20, 28);
            this.Opacity = 0.95;
            this.StartPosition = FormStartPosition.Manual;
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer, true);

            UpdateSize();

            // Один таймер: пересчёт размера/позиции + перерисовка. Прежний таймер
            // позиционирования (100 мс) убран — окно следует за панелью через
            // событие LocationChanged родителя, а здесь обновляемся раз в 500 мс.
            _updateTimer = new Timer { Interval = 500 };
            _updateTimer.Tick += (s, e) => { PositionAboveParent(); this.Invalidate(); };
            _updateTimer.Start();
        }

        private void UpdateSize()
        {
            int visibleCharts = GetVisibleChartCount(_history.GetPoints());
            int totalHeight = OuterPadding * 2 + visibleCharts * ChartHeight + Math.Max(visibleCharts - 1, 0) * Gap;
            this.Size = new Size(_parentForm.Width, Math.Min(totalHeight, 650));
        }

        private int GetVisibleChartCount(List<DataPoint> data)
        {
            int count = 0;
            foreach (var c in _charts)
            {
                if (!c.ShowOnlyIfPositive || HasRecentPositive(c, data))
                    count++;
            }
            return count;
        }

        private static bool HasRecentPositive(ChartConfig chart, List<DataPoint> data)
        {
            int checkCount = Math.Min(10, data.Count);
            for (int i = data.Count - 1; i >= data.Count - checkCount; i--)
            {
                if (i >= 0 && chart.GetValue(data[i]) > 0)
                    return true;
            }
            return false;
        }

        public void PositionAboveParent()
        {
            UpdateSize();

            int x = _parentForm.Left;
            int y;
            if (_parentForm.BarEdge == MainForm.TaskbarEdge.Top)
            {
                y = _parentForm.Bottom + Gap;
            }
            else
            {
                y = _parentForm.Top - this.Height - Gap;
                if (y < 0) y = 0;
            }

            var loc = new Point(x, y);
            if (this.Location != loc)
                this.Location = loc;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var data = _history.GetPoints(); // один снимок истории на кадр
            int y = OuterPadding;

            foreach (var chart in _charts)
            {
                if (chart.ShowOnlyIfPositive && !HasRecentPositive(chart, data))
                    continue;

                DrawChart(e.Graphics, chart, data, OuterPadding, y, this.Width - OuterPadding * 2, ChartHeight);
                y += ChartHeight + Gap;
            }
        }

        private void DrawChart(Graphics g, ChartConfig chart, List<DataPoint> data, int x, int y, int w, int h)
        {
            using (var path = CreateRoundRect(x, y, w, h, 8))
            {
                g.FillPath(CardBrush, path);
            }

            string displayVal = chart.FormatValue(_getCurrentData());
            g.DrawString(chart.Title, TitleFont, TitleBrush, x + 10, y + 6);

            var size = g.MeasureString(displayVal, ValueFont);
            g.DrawString(displayVal, ValueFont, chart.LineBrush, x + w - size.Width - 10, y + 4);

            int chartX = x + 10;
            int chartY = y + 24;
            int chartW = w - 20;
            int chartH = h - 32;

            if (data.Count < 2) return;

            float maxVal = chart.MaxDefault;
            foreach (var p in data)
            {
                float v = chart.GetValue(p);
                if (v > maxVal) maxVal = v;
            }
            maxVal = Math.Max(maxVal, 1);

            for (int i = 1; i <= 3; i++)
            {
                int gy = chartY + (int)(chartH * i / 4f);
                g.DrawLine(GridPen, chartX, gy, chartX + chartW, gy);
            }

            var pts = new List<PointF>(data.Count);
            float stepX = (float)chartW / Math.Max(data.Count - 1, 1);
            for (int i = 0; i < data.Count; i++)
            {
                float val = chart.GetValue(data[i]);
                float normalized = Math.Max(0, Math.Min(val / maxVal, 1f));
                pts.Add(new PointF(chartX + i * stepX, chartY + chartH - (normalized * chartH)));
            }

            if (pts.Count >= 2)
            {
                using (var fillPath = CreateSmoothFillPath(pts, chartY + chartH))
                using (var gradientBrush = new LinearGradientBrush(
                    new PointF(0, chartY), new PointF(0, chartY + chartH),
                    Color.FromArgb(45, chart.LineColor.R, chart.LineColor.G, chart.LineColor.B),
                    Color.FromArgb(3, chart.LineColor.R, chart.LineColor.G, chart.LineColor.B)))
                {
                    g.FillPath(gradientBrush, fillPath);
                }

                using (var linePath = CreateSmoothLinePath(pts))
                {
                    g.DrawPath(chart.LinePen, linePath);
                    g.DrawPath(chart.GlowPen, linePath);
                }

                var lastPt = pts[pts.Count - 1];
                g.FillEllipse(chart.LineBrush, lastPt.X - 2.5f, lastPt.Y - 2.5f, 5, 5);
            }
        }

        private GraphicsPath CreateSmoothFillPath(List<PointF> points, float baseY)
        {
            var path = new GraphicsPath();
            if (points.Count < 2) return path;

            path.AddLine(points[0].X, baseY, points[0].X, points[0].Y);

            for (int i = 0; i < points.Count - 1; i++)
            {
                var p1 = points[i];
                var p2 = points[i + 1];
                float cpx = (p1.X + p2.X) / 2;
                path.AddBezier(p1.X, p1.Y, cpx, p1.Y, cpx, p2.Y, p2.X, p2.Y);
            }

            path.AddLine(points[points.Count - 1].X, points[points.Count - 1].Y, points[points.Count - 1].X, baseY);
            path.CloseFigure();
            return path;
        }

        private GraphicsPath CreateSmoothLinePath(List<PointF> points)
        {
            var path = new GraphicsPath();
            if (points.Count < 2) return path;

            path.StartFigure();
            for (int i = 0; i < points.Count - 1; i++)
            {
                var p1 = points[i];
                var p2 = points[i + 1];
                float cpx = (p1.X + p2.X) / 2;
                path.AddBezier(p1.X, p1.Y, cpx, p1.Y, cpx, p2.Y, p2.X, p2.Y);
            }
            return path;
        }

        private GraphicsPath CreateRoundRect(int x, int y, int w, int h, int r)
        {
            var path = new GraphicsPath();
            path.AddArc(x, y, r * 2, r * 2, 180, 90);
            path.AddArc(x + w - r * 2, y, r * 2, r * 2, 270, 90);
            path.AddArc(x + w - r * 2, y + h - r * 2, r * 2, r * 2, 0, 90);
            path.AddArc(x, y + h - r * 2, r * 2, r * 2, 90, 90);
            path.CloseFigure();
            return path;
        }

        private string FormatSpeed(float kbps)
        {
            if (kbps >= 1024) return string.Format("{0:F1}MB/s", kbps / 1024f);
            if (kbps >= 1) return string.Format("{0:F0}KB/s", kbps);
            return string.Format("{0:F0}B/s", kbps * 1024);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _updateTimer.Stop();
            _updateTimer.Dispose();

            foreach (var c in _charts)
            {
                c.LineBrush.Dispose();
                c.LinePen.Dispose();
                c.GlowPen.Dispose();
            }

            base.OnFormClosing(e);
        }
    }
}
