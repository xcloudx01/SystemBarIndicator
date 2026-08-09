using System;
using System.Collections.Generic;
using System.Drawing;
using SystemBarIndicator.Configuration;

namespace SystemBarIndicator
{
    /// <summary>
    /// Drawing resources for the bar and the overlay that depend on settings (theme + font
    /// scale). These used to be <c>static readonly</c> fields on the form; once the look
    /// became configurable they became swappable, so they are gathered into a single object
    /// with an explicit lifetime.
    /// </summary>
    internal sealed class BarTheme : IDisposable
    {
        // Base point sizes (at FontScale = 1.0) — the same ones that used to be hard-coded.
        private const float BaseMainFontSize = 9f;
        private const float BaseSmallFontSize = 7.5f;
        private const float BaseTitleFontSize = 8.5f;
        private const float BaseValueFontSize = 13f;

        private readonly List<IDisposable> _owned = new();

        // --- Bar ---
        public Font MainFont { get; private set; } = null!;
        public Font SmallFont { get; private set; } = null!;
        public Brush Dim { get; private set; } = null!;
        public Pen Separator { get; private set; } = null!;
        public Brush Track { get; private set; } = null!;

        // --- Graph overlay ---
        public Font TitleFont { get; private set; } = null!;
        public Font ValueFont { get; private set; } = null!;
        public Brush TitleBrush { get; private set; } = null!;
        public Brush CardBrush { get; private set; } = null!;
        public Pen GridPen { get; private set; } = null!;

        public Color Background { get; private set; }
        public Color OverlayBackground { get; private set; }

        // Load-level and throughput colours are readable on any background and do not depend
        // on the theme, so they are created once for the lifetime of the application.
        public static readonly Brush Red = new SolidBrush(Color.FromArgb(244, 67, 54));
        public static readonly Brush Orange = new SolidBrush(Color.FromArgb(255, 152, 0));
        public static readonly Brush Yellow = new SolidBrush(Color.FromArgb(255, 193, 7));
        public static readonly Brush Green = new SolidBrush(Color.FromArgb(76, 175, 80));
        // The network colours match the overlay charts (Net DL / Net UL).
        public static readonly Brush NetDown = new SolidBrush(Color.FromArgb(237, 137, 54));
        public static readonly Brush NetUp = new SolidBrush(Color.FromArgb(38, 198, 218));
        // Disk gets its own colour pair (purple/pink) so it is not confused with network.
        public static readonly Brush DiskRead = new SolidBrush(Color.FromArgb(171, 71, 188));
        public static readonly Brush DiskWrite = new SolidBrush(Color.FromArgb(236, 64, 122));

        private BarTheme()
        {
        }

        /// <summary>
        /// Builds the theme from the settings. If construction fails partway through, the
        /// GDI+ objects allocated so far are disposed — otherwise they would leak.
        /// </summary>
        public static BarTheme Create(AppSettings settings)
        {
            var theme = new BarTheme();
            try
            {
                theme.Build(settings);
                return theme;
            }
            catch
            {
                theme.Dispose();
                throw;
            }
        }

        private void Build(AppSettings settings)
        {
            float scale = (float)settings.FontScale;

            MainFont = Own(new Font("Segoe UI", BaseMainFontSize * scale, FontStyle.Regular));
            SmallFont = Own(new Font("Segoe UI", BaseSmallFontSize * scale, FontStyle.Regular));
            TitleFont = Own(new Font("Segoe UI", BaseTitleFontSize * scale, FontStyle.Bold));
            ValueFont = Own(new Font("Segoe UI", BaseValueFontSize * scale, FontStyle.Bold));

            switch (settings.Theme)
            {
                case ThemeNames.Light:
                    Background = Color.FromArgb(240, 240, 240);
                    ApplyLightPalette();
                    break;

                case ThemeNames.Custom:
                    int argb = ColorCodec.ParseOrDefault(settings.BackgroundColor, unchecked((int)0xFF191919));
                    Background = Color.FromArgb(argb);
                    if (ColorCodec.IsDark(argb))
                        ApplyDarkPalette();
                    else
                        ApplyLightPalette();
                    // The overlay echoes the user's background, slightly darker/lighter — see Shift.
                    OverlayBackground = Shift(Background, ColorCodec.IsDark(argb) ? -6 : 10);
                    break;

                default:
                    Background = Color.FromArgb(25, 25, 25);
                    ApplyDarkPalette();
                    break;
            }
        }

        private void ApplyDarkPalette()
        {
            Dim = Own(new SolidBrush(Color.FromArgb(150, 150, 150)));
            Separator = Own(new Pen(Color.FromArgb(60, 60, 60), 1));
            Track = Own(new SolidBrush(Color.FromArgb(55, 55, 62)));
            TitleBrush = Own(new SolidBrush(Color.FromArgb(150, 150, 170)));
            CardBrush = Own(new SolidBrush(Color.FromArgb(28, 28, 38)));
            GridPen = Own(new Pen(Color.FromArgb(38, 38, 52), 1));
            OverlayBackground = Color.FromArgb(20, 20, 28);
        }

        private void ApplyLightPalette()
        {
            Dim = Own(new SolidBrush(Color.FromArgb(90, 90, 90)));
            Separator = Own(new Pen(Color.FromArgb(190, 190, 190), 1));
            Track = Own(new SolidBrush(Color.FromArgb(205, 205, 212)));
            TitleBrush = Own(new SolidBrush(Color.FromArgb(90, 90, 110)));
            CardBrush = Own(new SolidBrush(Color.FromArgb(228, 228, 234)));
            GridPen = Own(new Pen(Color.FromArgb(200, 200, 210), 1));
            OverlayBackground = Color.FromArgb(245, 245, 248);
        }

        private static Color Shift(Color color, int delta) => Color.FromArgb(
            color.A,
            Math.Clamp(color.R + delta, 0, 255),
            Math.Clamp(color.G + delta, 0, 255),
            Math.Clamp(color.B + delta, 0, 255));

        // Registers a created resource so Dispose releases it even if building the theme fails.
        private T Own<T>(T resource) where T : IDisposable
        {
            _owned.Add(resource);
            return resource;
        }

        public void Dispose()
        {
            for (int i = _owned.Count - 1; i >= 0; i--)
                _owned[i].Dispose();
            _owned.Clear();
        }
    }
}
