using System;
using System.Globalization;

namespace SystemBarIndicator.Configuration
{
    /// <summary>
    /// Цвет в настройках хранится строкой "#AARRGGBB" — файл редактируется руками,
    /// а знаковое int-представление (-15132391) для этого непригодно.
    /// </summary>
    public static class ColorCodec
    {
        public const string Default = "#FF191919";

        /// <summary>Разбирает "#AARRGGBB" или "#RRGGBB" (альфа = FF). При ошибке — false.</summary>
        public static bool TryParse(string? text, out int argb)
        {
            argb = 0;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            string s = text.Trim();
            if (s.Length > 0 && s[0] == '#')
                s = s.Substring(1);

            if (s.Length != 6 && s.Length != 8)
                return false;

            if (!uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint value))
                return false;

            if (s.Length == 6)
                value |= 0xFF000000u;

            argb = unchecked((int)value);
            return true;
        }

        /// <summary>Форматирует ARGB в "#AARRGGBB".</summary>
        public static string ToHex(int argb) =>
            "#" + unchecked((uint)argb).ToString("X8", CultureInfo.InvariantCulture);

        /// <summary>Приводит строку к каноническому "#AARRGGBB"; мусор заменяет на <see cref="Default"/>.</summary>
        public static string Normalize(string? text) =>
            TryParse(text, out int argb) ? ToHex(argb) : Default;

        /// <summary>Разбирает цвет, а при неудаче возвращает <paramref name="fallback"/>.</summary>
        public static int ParseOrDefault(string? text, int fallback) =>
            TryParse(text, out int argb) ? argb : fallback;

        /// <summary>
        /// Тёмный ли фон — по относительной яркости (ITU-R BT.601). По ней выбирается
        /// светлый или тёмный набор вспомогательных цветов для темы Custom.
        /// </summary>
        public static bool IsDark(int argb)
        {
            int r = (argb >> 16) & 0xFF;
            int g = (argb >> 8) & 0xFF;
            int b = argb & 0xFF;
            double luma = 0.299 * r + 0.587 * g + 0.114 * b;
            return luma < 128.0;
        }
    }
}
