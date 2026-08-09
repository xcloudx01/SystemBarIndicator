using System;
using System.Globalization;

namespace SystemBarIndicator.Util
{
    /// <summary>
    /// Platform-independent formatting of values for display.
    /// </summary>
    public static class Formatting
    {
        /// <summary>
        /// Load percentage for the bar: NaN and negatives → "0%", above 100 → "100%".
        /// Clamping guarantees the string is never wider than the "100%" slot template.
        /// </summary>
        public static string FormatPercent(float percent)
        {
            float p = float.IsNaN(percent) ? 0f : Math.Clamp(percent, 0f, 100f);
            return string.Format(CultureInfo.InvariantCulture, "{0:F0}%", p);
        }

        /// <summary>
        /// Temperature for the bar: NaN and negatives → "0C", above 999 → "999C".
        /// Clamping guarantees the string is never wider than the "999C" slot template.
        /// </summary>
        public static string FormatTemperature(float celsius) => FormatTemperature(celsius, fahrenheit: false);

        /// <summary>
        /// Temperature for the bar in the chosen unit. The value is clamped to 0..999
        /// AFTER conversion, so the string is never wider than the slot template
        /// (see <see cref="TemperatureSlotTemplate"/>).
        /// </summary>
        public static string FormatTemperature(float celsius, bool fahrenheit)
        {
            float t = float.IsNaN(celsius) ? 0f : Math.Clamp(celsius, 0f, 999f);
            if (fahrenheit)
                t = Math.Clamp(t * 9f / 5f + 32f, 0f, 999f);
            return string.Format(CultureInfo.InvariantCulture, "{0:F0}{1}", t, fahrenheit ? 'F' : 'C');
        }

        /// <summary>The widest possible temperature string — the fixed-width slot template.</summary>
        public static string TemperatureSlotTemplate(bool fahrenheit) => fahrenheit ? "999F" : "999C";

        /// <summary>
        /// Formats a transfer rate given in KB/s, picking the unit:
        /// B/s (&lt; 1 KB/s), KB/s (&lt; 1 MB/s) or MB/s.
        /// </summary>
        public static string FormatSpeed(float kbps)
        {
            if (kbps >= 1024f)
                return string.Format(CultureInfo.InvariantCulture, "{0:F1}MB/s", kbps / 1024f);
            if (kbps >= 1f)
                return string.Format(CultureInfo.InvariantCulture, "{0:F0}KB/s", kbps);
            return string.Format(CultureInfo.InvariantCulture, "{0:F0}B/s", kbps * 1024f);
        }
    }
}
