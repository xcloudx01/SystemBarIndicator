using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using SystemBarIndicator.Models;

namespace SystemBarIndicator
{
    public class DashboardWindow : Form
    {
        private readonly DataHistory _history;
        private Timer _updateTimer;
        private Func<SensorData> _getCurrentData;
        private bool _isDark = true;

        private Color _bgColor;
        private Color _cardBg;
        private Color _textColor;
        private Color _dimColor;
        private Color _borderColor;

        public DashboardWindow(DataHistory history, Func<SensorData> getCurrentData)
        {
            _history = history;
            _getCurrentData = getCurrentData;

            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.Size = new Size(900, 680);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MinimumSize = new Size(700, 500);
            this.BackColor = Color.FromArgb(18, 18, 24);
            this.Text = "SystemBar Indicator — Dashboard";
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.ResizeRedraw, true);

            SetThemeColors();

            _updateTimer = new Timer { Interval = 1000 };
            _updateTimer.Tick += (s, e) => this.Invalidate();
            _updateTimer.Start();
        }

        private void SetThemeColors()
        {
            _bgColor = Color.FromArgb(18, 18, 24);
            _cardBg = Color.FromArgb(28, 28, 38);
            _textColor = Color.FromArgb(230, 230, 240);
            _dimColor = Color.FromArgb(120, 120, 140);
            _borderColor = Color.FromArgb(40, 40, 55);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int padding = 20;
            int cardGap = 16;
            int headerHeight = 60;
            int cardWidth = (this.ClientSize.Width - padding * 2 - cardGap * 2) / 3;
            int cardHeight = 180;

            // Header
            using (var titleFont = new Font("Segoe UI", 20f, FontStyle.Bold))
            using (var titleBrush = new SolidBrush(_textColor))
            {
                e.Graphics.DrawString("System Monitor", titleFont, titleBrush, padding, 15);
            }
            using (var subFont = new Font("Segoe UI", 10f, FontStyle.Regular))
            using (var subBrush = new SolidBrush(_dimColor))
            {
                e.Graphics.DrawString("Real-time hardware monitoring", subFont, subBrush, padding, 45);
            }

            // Current values row
            var currentData = _getCurrentData();
            int rowY = headerHeight + 10;
            int miniCardW = (this.ClientSize.Width - padding * 2 - cardGap * 3) / 4;
            int miniCardH = 70;

            DrawMiniCard(e.Graphics, "CPU", string.Format("{0:F0}%", currentData.CpuUsage), currentData.CpuUsage / 100f, padding, rowY, miniCardW, miniCardH);
            DrawMiniCard(e.Graphics, "RAM", string.Format("{0:F0}%", currentData.RamUsage), currentData.RamUsage / 100f, padding + miniCardW + cardGap, rowY, miniCardW, miniCardH);
            DrawMiniCard(e.Graphics, "GPU", string.Format("{0:F0}%", currentData.GpuUsage), currentData.GpuUsage / 100f, padding + (miniCardW + cardGap) * 2, rowY, miniCardW, miniCardH);

            float totalNet = currentData.NetDown + currentData.NetUp;
            float netPercent = Math.Min(totalNet / 10240f, 1f);
            DrawMiniCard(e.Graphics, "NET", string.Format("{0} / {1}", FormatSpeed(currentData.NetDown), FormatSpeed(currentData.NetUp)), netPercent, padding + (miniCardW + cardGap) * 3, rowY, miniCardW, miniCardH);

            // Chart cards
            int chartY = rowY + miniCardH + cardGap + 10;
            int chartH = (this.ClientSize.Height - chartY - padding - 20) / 2;

            DrawChartCard(e.Graphics, "CPU Usage", _history.GetPoints(), p => p.CpuUsage, Color.FromArgb(99, 179, 237), Color.FromArgb(52, 120, 180), padding, chartY, this.ClientSize.Width - padding * 2, chartH);
            DrawChartCard(e.Graphics, "RAM Usage", _history.GetPoints(), p => p.RamUsage, Color.FromArgb(129, 140, 248), Color.FromArgb(99, 110, 200), padding, chartY + chartH + cardGap, this.ClientSize.Width - padding * 2, chartH);

            // Second row of charts
            int chartY2 = chartY + chartH * 2 + cardGap * 2;
            int halfW = (this.ClientSize.Width - padding * 2 - cardGap) / 2;

            DrawChartCard(e.Graphics, "GPU Usage", _history.GetPoints(), p => p.GpuUsage, Color.FromArgb(72, 187, 120), Color.FromArgb(56, 150, 96), padding, chartY2, halfW, chartH);
            DrawChartCard(e.Graphics, "Network", _history.GetPoints(), p => p.NetDown, Color.FromArgb(237, 137, 54), Color.FromArgb(200, 110, 40), padding + halfW + cardGap, chartY2, halfW, chartH);
        }

        private void DrawMiniCard(Graphics g, string label, string value, float loadPercent, int x, int y, int w, int h)
        {
            using (var path = CreateRoundRect(x, y, w, h, 12))
            using (var brush = new SolidBrush(_cardBg))
            {
                g.FillPath(brush, path);
            }

            using (var labelFont = new Font("Segoe UI", 9f, FontStyle.Regular))
            using (var valueFont = new Font("Segoe UI", 18f, FontStyle.Bold))
            using (var labelBrush = new SolidBrush(_dimColor))
            using (var valueBrush = new SolidBrush(GetColorForLoad(loadPercent)))
            {
                g.DrawString(label, labelFont, labelBrush, x + 14, y + 10);
                g.DrawString(value, valueFont, valueBrush, x + 14, y + 28);
            }

            // Mini progress bar
            int barX = x + 14;
            int barY = y + h - 16;
            int barW = w - 28;
            int barH = 5;

            using (var bgBrush = new SolidBrush(Color.FromArgb(45, 45, 60)))
            using (var barPath = CreateRoundRect(barX, barY, barW, barH, 3))
            {
                g.FillPath(bgBrush, barPath);
            }

            int fillW = (int)(barW * Math.Min(loadPercent, 1f));
            if (fillW > 0)
            {
                using (var fillBrush = new SolidBrush(GetColorForLoad(loadPercent)))
                using (var fillPath = CreateRoundRect(barX, barY, fillW, barH, 3))
                {
                    g.FillPath(fillBrush, fillPath);
                }
            }
        }

        private void DrawChartCard(Graphics g, string title, List<DataPoint> data, Func<DataPoint, float> getValue, Color lineColor, Color fillColor, int x, int y, int w, int h)
        {
            // Card background
            using (var path = CreateRoundRect(x, y, w, h, 14))
            using (var brush = new SolidBrush(_cardBg))
            {
                g.FillPath(brush, path);
            }

            // Title
            using (var titleFont = new Font("Segoe UI", 11f, FontStyle.Bold))
            using (var titleBrush = new SolidBrush(_textColor))
            {
                g.DrawString(title, titleFont, titleBrush, x + 16, y + 12);
            }

            // Current value
            float currentVal = 0;
            if (data.Count > 0)
                currentVal = getValue(data[data.Count - 1]);

            using (var valFont = new Font("Segoe UI", 22f, FontStyle.Bold))
            using (var valBrush = new SolidBrush(lineColor))
            {
                string displayVal = title.Contains("Network") ? FormatSpeed(currentVal) : string.Format("{0:F1}%", currentVal);
                var size = g.MeasureString(displayVal, valFont);
                g.DrawString(displayVal, valFont, valBrush, x + w - size.Width - 16, y + 8);
            }

            // Chart area
            int chartX = x + 16;
            int chartY = y + 48;
            int chartW = w - 32;
            int chartH = h - 60;

            if (data.Count < 2) return;

            float maxVal = 100;
            if (title.Contains("Network"))
            {
                maxVal = 1;
                foreach (var p in data)
                {
                    float v = getValue(p);
                    if (v > maxVal) maxVal = v;
                }
                maxVal = Math.Max(maxVal, 100);
            }

            // Draw grid lines
            using (var gridPen = new Pen(Color.FromArgb(45, 45, 60), 1))
            {
                for (int i = 1; i <= 4; i++)
                {
                    int gy = chartY + (int)(chartH * i / 5f);
                    g.DrawLine(gridPen, chartX, gy, chartX + chartW, gy);
                }
            }

            // Build smooth path
            var points = new List<PointF>();
            float stepX = (float)chartW / Math.Max(data.Count - 1, 1);

            for (int i = 0; i < data.Count; i++)
            {
                float val = getValue(data[i]);
                float normalized = Math.Max(0, Math.Min(val / maxVal, 1f));
                float px = chartX + i * stepX;
                float py = chartY + chartH - (normalized * chartH);
                points.Add(new PointF(px, py));
            }

            // Fill gradient area
            if (points.Count >= 2)
            {
                using (var fillPath = CreateSmoothFillPath(points, chartX, chartY + chartH))
                using (var gradientBrush = new LinearGradientBrush(
                    new PointF(0, chartY), new PointF(0, chartY + chartH),
                    Color.FromArgb(60, lineColor.R, lineColor.G, lineColor.B),
                    Color.FromArgb(2, lineColor.R, lineColor.G, lineColor.B)))
                {
                    g.FillPath(gradientBrush, fillPath);
                }

                // Draw smooth line
                using (var linePath = CreateSmoothLinePath(points))
                using (var linePen = new Pen(lineColor, 2.5f))
                {
                    linePen.LineJoin = LineJoin.Round;
                    g.DrawPath(linePen, linePath);
                }

                // Glow effect
                using (var linePath = CreateSmoothLinePath(points))
                using (var glowPen = new Pen(Color.FromArgb(40, lineColor.R, lineColor.G, lineColor.B), 6f))
                {
                    glowPen.LineJoin = LineJoin.Round;
                    g.DrawPath(glowPen, linePath);
                }

                // End dot
                var lastPt = points[points.Count - 1];
                using (var dotBrush = new SolidBrush(lineColor))
                {
                    g.FillEllipse(dotBrush, lastPt.X - 4, lastPt.Y - 4, 8, 8);
                }
                using (var dotOutline = new Pen(_cardBg, 2))
                {
                    g.DrawEllipse(dotOutline, lastPt.X - 4, lastPt.Y - 4, 8, 8);
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
            path.AddLine(points[0].X, points[0].Y, points[0].X, points[0].Y);

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

        private Color GetColorForLoad(float p)
        {
            if (p >= 0.9f) return Color.FromArgb(244, 67, 54);
            if (p >= 0.8f) return Color.FromArgb(255, 152, 0);
            if (p >= 0.6f) return Color.FromArgb(255, 193, 7);
            return Color.FromArgb(76, 175, 80);
        }

        private string FormatSpeed(float kbps)
        {
            if (kbps >= 1024) return string.Format("{0:F1} GB/s", kbps / 1024f);
            if (kbps >= 1) return string.Format("{0:F0} KB/s", kbps);
            return string.Format("{0:F0} B/s", kbps * 1024);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _updateTimer.Stop();
            _updateTimer.Dispose();
            base.OnFormClosing(e);
        }
    }
}
