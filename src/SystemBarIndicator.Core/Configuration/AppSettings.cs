using System;

namespace SystemBarIndicator.Configuration
{
    /// <summary>Тема оформления бара.</summary>
    public static class ThemeNames
    {
        public const string Dark = "Dark";
        public const string Light = "Light";
        public const string Custom = "Custom";

        public static bool IsKnown(string? name) =>
            name == Dark || name == Light || name == Custom;
    }

    /// <summary>
    /// Пользовательские настройки: положение бара и внешний вид.
    /// Хранится в JSON (см. <see cref="SettingsStore"/>), поэтому все поля —
    /// автосвойства: System.Text.Json по умолчанию игнорирует обычные поля.
    /// </summary>
    public sealed class AppSettings
    {
        /// <summary>Версия схемы файла — задел на будущие миграции.</summary>
        public int SchemaVersion { get; set; } = 1;

        // --- Положение бара ---
        // Сохраняем ПРАВЫЙ край, а не Left: ширина окна подгоняется под контент уже
        // после первого кадра, и подгонка удерживает именно правый край.
        // null — позиция пользователем не задана, бар встаёт по панели задач.
        public int? BarRight { get; set; }
        public int? BarTop { get; set; }

        // --- Внешний вид ---
        public double Opacity { get; set; } = DefaultOpacity;
        public string Theme { get; set; } = ThemeNames.Dark;

        /// <summary>Цвет фона для темы Custom в виде "#AARRGGBB".</summary>
        public string BackgroundColor { get; set; } = "#FF191919";

        /// <summary>Масштаб шрифта относительно базового (9 pt / 7.5 pt).</summary>
        public double FontScale { get; set; } = DefaultFontScale;

        public int UpdateIntervalMs { get; set; } = DefaultIntervalMs;

        /// <summary>Показывать температуры в градусах Фаренгейта.</summary>
        public bool UseFahrenheit { get; set; }

        // --- Состав секций ---
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

        /// <summary>Допустимые интервалы опроса, мс. Прочие округляются до ближайшего.</summary>
        public static readonly int[] AllowedIntervalsMs = { 500, 1000, 2000, 5000 };

        /// <summary>
        /// Возвращает копию с приведёнными в допустимый диапазон значениями.
        /// Исходный объект не изменяется. Устойчива к любому содержимому файла:
        /// JSON может подсунуть null в ненулевое свойство и NaN/бесконечность в число.
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

            // Аннотации nullable не влияют на десериализацию (RespectNullableAnnotations
            // по умолчанию выключен), поэтому строки проверяем на null явно.
            if (!ThemeNames.IsKnown(s.Theme))
                s.Theme = ThemeNames.Dark;

            s.BackgroundColor = ColorCodec.Normalize(s.BackgroundColor);

            // Полностью пустой бар не поймать мышью — оставляем хотя бы CPU.
            if (!s.ShowCpu && !s.ShowRam && !s.ShowGpu && !s.ShowDisk && !s.ShowNetwork &&
                !s.ShowCpuTemp && !s.ShowGpuTemp)
            {
                s.ShowCpu = true;
            }

            return s;
        }

        /// <summary>Ближайший допустимый интервал опроса.</summary>
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
        /// Отличаются ли настройки в части, требующей пересоздания ресурсов рисования
        /// (шрифты и кисти). Прозрачность и интервал сюда не входят — они применяются
        /// напрямую, без пересоздания GDI+-объектов.
        /// </summary>
        public bool DiffersInAppearance(AppSettings other) =>
            other is null ||
            !string.Equals(Theme, other.Theme, StringComparison.Ordinal) ||
            !string.Equals(BackgroundColor, other.BackgroundColor, StringComparison.OrdinalIgnoreCase) ||
            FontScale != other.FontScale;
    }
}
