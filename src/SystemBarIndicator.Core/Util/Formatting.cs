using System;
using System.Globalization;

namespace SystemBarIndicator.Util
{
    /// <summary>
    /// Платформонезависимое форматирование значений для отображения.
    /// </summary>
    public static class Formatting
    {
        /// <summary>
        /// Процент нагрузки для полоски: NaN и отрицательные → "0%", выше 100 → "100%".
        /// Диапазон гарантирует, что строка не шире шаблона слота "100%".
        /// </summary>
        public static string FormatPercent(float percent)
        {
            float p = float.IsNaN(percent) ? 0f : Math.Clamp(percent, 0f, 100f);
            return string.Format(CultureInfo.InvariantCulture, "{0:F0}%", p);
        }

        /// <summary>
        /// Температура для полоски: NaN и отрицательные → "0C", выше 999 → "999C".
        /// Диапазон гарантирует, что строка не шире шаблона слота "999C".
        /// </summary>
        public static string FormatTemperature(float celsius)
        {
            float t = float.IsNaN(celsius) ? 0f : Math.Clamp(celsius, 0f, 999f);
            return string.Format(CultureInfo.InvariantCulture, "{0:F0}C", t);
        }

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
