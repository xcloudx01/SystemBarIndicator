using System;

namespace SystemBarIndicator.Util
{
    /// <summary>
    /// Пересчёт коэффициентов <see cref="Ema"/> и <see cref="AutoScale"/> под фактический
    /// шаг опроса. Базовые значения (alpha 0.4, decay 0.97) откалиброваны под тик в 1 секунду;
    /// при другом интервале их нужно пересчитать, иначе сглаживание станет заметно
    /// быстрее или медленнее по времени.
    /// </summary>
    public static class SmoothingRates
    {
        private const float MinAlpha = 0.001f;
        private const float MinDecay = 0.001f;
        private const float MaxDecay = 0.999f;

        /// <summary>
        /// Alpha для шага <paramref name="intervalMs"/> при заданном alpha на секунду.
        /// Сохраняется доля «памяти» за одну секунду: (1-alpha) возводится в степень dt.
        /// </summary>
        public static float AlphaForInterval(float alphaPerSecond, int intervalMs)
        {
            if (alphaPerSecond <= 0f || alphaPerSecond > 1f)
                throw new ArgumentOutOfRangeException(nameof(alphaPerSecond), alphaPerSecond, "alpha должен быть в (0..1].");
            if (intervalMs <= 0)
                throw new ArgumentOutOfRangeException(nameof(intervalMs), intervalMs, "Интервал должен быть положительным.");

            if (alphaPerSecond >= 1f)
                return 1f;

            double seconds = intervalMs / 1000.0;
            double retained = Math.Pow(1.0 - alphaPerSecond, seconds);
            return (float)Math.Clamp(1.0 - retained, MinAlpha, 1.0);
        }

        /// <summary>
        /// Множитель затухания для шага <paramref name="intervalMs"/> при заданном затухании
        /// за секунду: за одну секунду шкала спадает одинаково при любом шаге.
        /// </summary>
        public static float DecayForInterval(float decayPerSecond, int intervalMs)
        {
            if (decayPerSecond <= 0f || decayPerSecond >= 1f)
                throw new ArgumentOutOfRangeException(nameof(decayPerSecond), decayPerSecond, "decay должен быть в (0..1).");
            if (intervalMs <= 0)
                throw new ArgumentOutOfRangeException(nameof(intervalMs), intervalMs, "Интервал должен быть положительным.");

            double seconds = intervalMs / 1000.0;
            double decayed = Math.Pow(decayPerSecond, seconds);
            return (float)Math.Clamp(decayed, MinDecay, MaxDecay);
        }
    }
}
