using System;
using System.Globalization;

namespace SystemBarIndicator.Configuration
{
    /// <summary>
    /// Colours are stored in settings as "#AARRGGBB" strings — the file is meant to be
    /// hand-edited, and a signed int representation (-15132391) is useless for that.
    /// </summary>
    public static class ColorCodec
    {
        public const string Default = "#FF191919";

        /// <summary>Parses "#AARRGGBB" or "#RRGGBB" (alpha = FF). Returns false on failure.</summary>
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

        /// <summary>Formats ARGB as "#AARRGGBB".</summary>
        public static string ToHex(int argb) =>
            "#" + unchecked((uint)argb).ToString("X8", CultureInfo.InvariantCulture);

        /// <summary>Canonicalises a string to "#AARRGGBB"; garbage becomes <see cref="Default"/>.</summary>
        public static string Normalize(string? text) =>
            TryParse(text, out int argb) ? ToHex(argb) : Default;

        /// <summary>Parses a colour, returning <paramref name="fallback"/> on failure.</summary>
        public static int ParseOrDefault(string? text, int fallback) =>
            TryParse(text, out int argb) ? argb : fallback;

        /// <summary>
        /// Whether the background is dark, by relative luminance (ITU-R BT.601). This picks
        /// the light or dark set of accent colours for the Custom theme.
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
