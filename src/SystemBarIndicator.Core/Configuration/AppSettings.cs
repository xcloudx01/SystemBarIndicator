using System;

namespace SystemBarIndicator.Configuration
{
    /// <summary>Visual theme of the bar.</summary>
    public static class ThemeNames
    {
        public const string Dark = "Dark";
        public const string Light = "Light";
        public const string Custom = "Custom";

        public static bool IsKnown(string? name) =>
            name == Dark || name == Light || name == Custom;
    }

    /// <summary>
    /// User settings: bar position and appearance.
    /// Stored as JSON (see <see cref="SettingsStore"/>), which is why every member is an
    /// auto-property: System.Text.Json ignores plain fields by default.
    /// </summary>
    public sealed class AppSettings
    {
        /// <summary>File schema version — room for future migrations.</summary>
        public int SchemaVersion { get; set; } = 1;

        // --- Bar position ---
        // We store the RIGHT edge, not Left: the window width is fitted to the content only
        // after the first frame, and that fit holds the right edge in place.
        // null means the user never set a position, so the bar docks to the taskbar.
        public int? BarRight { get; set; }
        public int? BarTop { get; set; }

        // --- Appearance ---
        public double Opacity { get; set; } = DefaultOpacity;
        public string Theme { get; set; } = ThemeNames.Dark;

        /// <summary>Background colour for the Custom theme, as "#AARRGGBB".</summary>
        public string BackgroundColor { get; set; } = "#FF191919";

        /// <summary>Font scale relative to the base size (9 pt / 7.5 pt).</summary>
        public double FontScale { get; set; } = DefaultFontScale;

        public int UpdateIntervalMs { get; set; } = DefaultIntervalMs;

        /// <summary>Show temperatures in degrees Fahrenheit.</summary>
        public bool UseFahrenheit { get; set; }

        // --- Which sections to show ---
        public bool ShowCpu { get; set; } = true;
        public bool ShowCpuTemp { get; set; } = true;
        public bool ShowRam { get; set; } = true;
        public bool ShowGpu { get; set; } = true;
        public bool ShowGpuTemp { get; set; } = true;
        public bool ShowDisk { get; set; } = true;
        public bool ShowNetwork { get; set; } = true;

        public const double MinOpacity = 0.30;
        public const double MaxOpacity = 1.00;
        public const double DefaultOpacity = 0.92;

        public const double MinFontScale = 0.75;
        public const double MaxFontScale = 1.75;
        public const double DefaultFontScale = 1.00;

        public const int DefaultIntervalMs = 1000;

        /// <summary>Allowed poll intervals, ms. Anything else is rounded to the nearest one.</summary>
        public static readonly int[] AllowedIntervalsMs = { 500, 1000, 2000, 5000 };

        /// <summary>
        /// Returns a copy with every value clamped into its allowed range. The original is
        /// left untouched. Robust against any file contents: JSON can slip a null into a
        /// non-nullable property and NaN/infinity into a number.
        /// </summary>
        public AppSettings Normalized()
        {
            var s = Clone();

            s.SchemaVersion = s.SchemaVersion <= 0 ? 1 : s.SchemaVersion;

            s.Opacity = double.IsNaN(s.Opacity) || double.IsInfinity(s.Opacity)
                ? DefaultOpacity
                : Math.Clamp(s.Opacity, MinOpacity, MaxOpacity);

            s.FontScale = double.IsNaN(s.FontScale) || double.IsInfinity(s.FontScale)
                ? DefaultFontScale
                : Math.Clamp(s.FontScale, MinFontScale, MaxFontScale);

            s.UpdateIntervalMs = NearestInterval(s.UpdateIntervalMs);

            // Nullable annotations do not affect deserialization (RespectNullableAnnotations
            // is off by default), so strings are checked for null explicitly.
            if (!ThemeNames.IsKnown(s.Theme))
                s.Theme = ThemeNames.Dark;

            s.BackgroundColor = ColorCodec.Normalize(s.BackgroundColor);

            // A completely empty bar cannot be grabbed with the mouse — keep at least CPU.
            if (!s.ShowCpu && !s.ShowRam && !s.ShowGpu && !s.ShowDisk && !s.ShowNetwork &&
                !s.ShowCpuTemp && !s.ShowGpuTemp)
            {
                s.ShowCpu = true;
            }

            return s;
        }

        /// <summary>The nearest allowed poll interval.</summary>
        public static int NearestInterval(int ms)
        {
            int best = DefaultIntervalMs;
            int bestDistance = int.MaxValue;
            foreach (int allowed in AllowedIntervalsMs)
            {
                int distance = Math.Abs(allowed - ms);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = allowed;
                }
            }
            return best;
        }

        public AppSettings Clone() => new AppSettings
        {
            SchemaVersion = SchemaVersion,
            BarRight = BarRight,
            BarTop = BarTop,
            Opacity = Opacity,
            Theme = Theme,
            BackgroundColor = BackgroundColor,
            FontScale = FontScale,
            UpdateIntervalMs = UpdateIntervalMs,
            UseFahrenheit = UseFahrenheit,
            ShowCpu = ShowCpu,
            ShowCpuTemp = ShowCpuTemp,
            ShowRam = ShowRam,
            ShowGpu = ShowGpu,
            ShowGpuTemp = ShowGpuTemp,
            ShowDisk = ShowDisk,
            ShowNetwork = ShowNetwork
        };

        /// <summary>
        /// Whether the settings differ in ways that require recreating the drawing resources
        /// (fonts and brushes). Opacity and interval are excluded — they are applied
        /// directly, without rebuilding any GDI+ objects.
        /// </summary>
        public bool DiffersInAppearance(AppSettings other) =>
            other is null ||
            !string.Equals(Theme, other.Theme, StringComparison.Ordinal) ||
            !string.Equals(BackgroundColor, other.BackgroundColor, StringComparison.OrdinalIgnoreCase) ||
            FontScale != other.FontScale;
    }
}
