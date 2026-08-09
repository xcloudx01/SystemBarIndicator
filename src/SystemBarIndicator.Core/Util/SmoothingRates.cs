using System;

namespace SystemBarIndicator.Util
{
    /// <summary>
    /// Recalculates the <see cref="Ema"/> and <see cref="AutoScale"/> coefficients for the
    /// actual poll step. The base values (alpha 0.4, decay 0.97) are calibrated for a 1 s
    /// tick; at any other interval they must be recomputed, or the smoothing becomes
    /// noticeably faster or slower in wall-clock terms.
    /// </summary>
    public static class SmoothingRates
    {
        private const float MinAlpha = 0.001f;
        private const float MinDecay = 0.001f;
        private const float MaxDecay = 0.999f;

        /// <summary>
        /// Alpha for a step of <paramref name="intervalMs"/> given an alpha per second.
        /// The "memory" retained over one second is preserved: (1-alpha) is raised to dt.
        /// </summary>
        public static float AlphaForInterval(float alphaPerSecond, int intervalMs)
        {
            if (alphaPerSecond <= 0f || alphaPerSecond > 1f)
                throw new ArgumentOutOfRangeException(nameof(alphaPerSecond), alphaPerSecond, "alpha must be in (0..1].");
            if (intervalMs <= 0)
                throw new ArgumentOutOfRangeException(nameof(intervalMs), intervalMs, "The interval must be positive.");

            if (alphaPerSecond >= 1f)
                return 1f;

            double seconds = intervalMs / 1000.0;
            double retained = Math.Pow(1.0 - alphaPerSecond, seconds);
            return (float)Math.Clamp(1.0 - retained, MinAlpha, 1.0);
        }

        /// <summary>
        /// Decay factor for a step of <paramref name="intervalMs"/> given a decay per
        /// second: over one second the scale falls by the same amount at any step size.
        /// </summary>
        public static float DecayForInterval(float decayPerSecond, int intervalMs)
        {
            if (decayPerSecond <= 0f || decayPerSecond >= 1f)
                throw new ArgumentOutOfRangeException(nameof(decayPerSecond), decayPerSecond, "decay must be in (0..1).");
            if (intervalMs <= 0)
                throw new ArgumentOutOfRangeException(nameof(intervalMs), intervalMs, "The interval must be positive.");

            double seconds = intervalMs / 1000.0;
            double decayed = Math.Pow(decayPerSecond, seconds);
            return (float)Math.Clamp(decayed, MinDecay, MaxDecay);
        }
    }
}
