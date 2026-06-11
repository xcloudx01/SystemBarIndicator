using System.Globalization;

namespace SystemBarIndicator.Util
{
    /// <summary>
    /// Платформонезависимое форматирование значений для отображения.
    /// </summary>
    public static class Formatting
    {
        /// <summary>
        /// Форматирует скорость передачи данных, заданную в КБ/с, выбирая единицу:
        /// B/s (&lt; 1 КБ/с), KB/s (&lt; 1 МБ/с) или MB/s.
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
