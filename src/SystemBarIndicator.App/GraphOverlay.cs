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
        private readonly DataHistory _history;
        private readonly Func<SensorData> _getCurrentData;
        private readonly Form _parentForm;
        private Timer _updateTimer;
        private int _selectedIndex = 0;

        private string[] _chartNames = { "CPU", "RAM", "GPU", "NET" };
        private Color[] _lineColors = {
            Color.FromArgb(99, 179, 237),
            Color.FromArgb(129, 140, 248),
            Color.FromArgb(72, 187, 120),
            Color.FromArgb(237, 137, 54)
        };
        private Color[] _fillColors = {
            Color.FromArgb(52, 120, 180),
            Color.FromArgb(99, 110, 200),
            Color.FromArgb(56, 150, 96),
            Color.FromArgb(200, 110, 40)
        };
        private Func<DataPoint, float>[] _getValueFuncs;

        public GraphOverlay(DataHistory history, Func<SensorData> getCurrentData, Form parentForm)
        {
            _history = history;
            _getCurrentData = getCurrentData;
            _parentForm = parentForm;

            _getValueFuncs = new Func<DataPoint, float>[]
            {
                p => p.CpuUsage,
                p => p.RamUsage,
                p => p.GpuUsage,
                p => p.NetDown
            };

            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.BackColor = Color.FromArgb(20, 20, 28);
            this.Opacity = 0.95;
            this.Size = new Size(parentForm.Width, 140);
            this.StartPosition = FormStartPosition.Manual;
            this.DoubleBuffered = true;

            _updateTimer = new Timer { Interval = 500 };
            _updateTimer.Tick += (s, e) => this.Invalidate();
            _updateTimer.Start();

            this.MouseDown += (s, e) =>
            {
                int chartW = (this.Width - 20) / 4;
                int idx = e.X / chartW;
                if (idx >= 0 && idx < 4)
                {
                    _selectedIndex = idx;
                    this.Invalidate();
                }
            };
        }

        public void PositionAboveParent()
        {
            this.Location = new Point(_parentForm.Left, _parentForm.Top - this.Height - 4);
            this.Size = new Size(_parentForm.Width, 140);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int padding = 8;
            int chartGap = 6;
            int chartW = (this.Width - padding * 2 - chartGap * 3) / 4;
            int chartH = this.Height - padding * 2;

            for (int i = 0; i < 4; i++)
            {
                int x = padding + i * (chartW + chartGap);
                DrawChart(e.Graphics, _chartNames[i], _history.GetPoints(), _getValueFuncs[i],
                    _lineColors[i], _fillColors[i], x, padding, chartW, chartH, i == _selectedIndex);
            }
        }

        private void DrawChart(Graphics g, string title, List<DataPoint> data, Func<DataPoint, float> getValue,
            Color lineColor, Color fillColor, int x, int y, int w, int h, bool isSelected)
        {
            // Card background
            int radius = isSelected ? 10 : 8;
            using (var path = CreateRoundRect(x, y, w, h, radius))
            using (var brush = new SolidBrush(isSelected ? Color.FromArgb(35, 35, 50) : Color.FromArgb(28, 28, 38)))
            {
                g.FillPath(brush, path);
            }

            // Border for selected
            if (isSelected)
            {
                using (var path = CreateRoundRect(x, y, w, h, radius))
                using (var pen = new Pen(Color.FromArgb(60, 60, 80), 1))
                {
                    g.DrawPath(pen, path);
                }
            }

            // Title
            using (var titleFont = new Font("Segoe UI", 8f, FontStyle.Bold))
            using (var titleBrush = new SolidBrush(Color.FromArgb(160, 160, 180)))
            {
                g.DrawString(title, titleFont, titleBrush, x + 8, y + 6);
            }

            // Current value
            float currentVal = 0;
            if (data.Count > 0)
                currentVal = getValue(data[data.Count - 1]);

            string displayVal = title == "NET" ? FormatSpeed(currentVal) : string.Format("{0:F0}%", currentVal);
            using (var valFont = new Font("Segoe UI", 14f, FontStyle.Bold))
            using (var valBrush = new SolidBrush(lineColor))
            {
                var size = g.MeasureString(displayVal, valFont);
                g.DrawString(displayVal, valFont, valBrush, x + w - size.Width - 8, y + 4);
            }

            // Chart area
            int chartX = x + 6;
            int chartY = y + 30;
            int chartW2 = w - 12;
            int chartH2 = h - 38;

            if (data.Count < 2) return;

            float maxVal = title == "NET" ? 1000 : 100;
            if (title == "NET")
            {
                foreach (var p in data)
                {
                    float v = getValue(p);
                    if (v > maxVal) maxVal = v;
                }
                maxVal = Math.Max(maxVal, 10);
            }

            // Grid lines
            using (var gridPen = new Pen(Color.FromArgb(40, 40, 55), 1))
            {
                for (int i = 1; i <= 3; i++)
                {
                    int gy = chartY + (int)(chartH2 * i / 4f);
                    g.DrawLine(gridPen, chartX, gy, chartX + chartW2, gy);
                }
            }

            // Build points
            var pts = new List<PointF>();
            float stepX = (float)chartW2 / Math.Max(data.Count - 1, 1);
            for (int i = 0; i < data.Count; i++)
            {
                float val = getValue(data[i]);
                float normalized = Math.Max(0, Math.Min(val / maxVal, 1f));
                pts.Add(new PointF(chartX + i * stepX, chartY + chartH2 - (normalized * chartH2)));
            }

            if (pts.Count >= 2)
            {
                // Fill gradient
                using (var fillPath = CreateSmoothFillPath(pts, chartX, chartY + chartH2))
                using (var gradientBrush = new LinearGradientBrush(
                    new PointF(0, chartY), new PointF(0, chartY + chartH2),
                    Color.FromArgb(50, lineColor.R, lineColor.G, lineColor.B),
                    Color.FromArgb(3, lineColor.R, lineColor.G, lineColor.B)))
                {
                    g.FillPath(gradientBrush, fillPath);
                }

                // Smooth line
                using (var linePath = CreateSmoothLinePath(pts))
                using (var linePen = new Pen(lineColor, 2f))
                {
                    linePen.LineJoin = LineJoin.Round;
                    g.DrawPath(linePen, linePath);
                }

                // Glow
                using (var linePath = CreateSmoothLinePath(pts))
                using (var glowPen = new Pen(Color.FromArgb(35, lineColor.R, lineColor.G, lineColor.B), 5f))
                {
                    glowPen.LineJoin = LineJoin.Round;
                    g.DrawPath(glowPen, linePath);
                }

                // End dot
                var lastPt = pts[pts.Count - 1];
                using (var dotBrush = new SolidBrush(lineColor))
                {
                    g.FillEllipse(dotBrush, lastPt.X - 3, lastPt.Y - 3, 6, 6);
                }
            }
        }

        private GraphicsPath CreateSmoothFillPath(List<PointF> points, float baseX, float baseY)
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
            if (kbps >= 1024) return string.Format("{0:F1}GB", kbps / 1024f);
            if (kbps >= 1) return string.Format("{0:F0}KB", kbps);
            return string.Format("{0:F0}B", kbps * 1024);
        }

        public int SelectedIndex => _selectedIndex;

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _updateTimer.Stop();
            _updateTimer.Dispose();
            base.OnFormClosing(e);
        }
    }
}
