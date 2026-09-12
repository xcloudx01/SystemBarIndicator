using System;

namespace SystemBarIndicator.Monitoring
{
    /// <summary>
    /// Turns a used/total pair of video memory readings into a load percentage, for cards
    /// that do not report one ready-made. Kept out of the sensor so the arithmetic — which
    /// is all edge cases — can be tested.
    /// </summary>
    public static class VramLoad
    {
        /// <summary>
        /// How full the video memory is, in percent, or null when the pair cannot produce a
        /// usable number. Both readings must be in the same unit (LibreHardwareMonitor
        /// reports megabytes); only their ratio is used, so which unit it is does not matter.
        /// </summary>
        public static float? Percent(float? used, float? total)
        {
            if (!used.HasValue || !total.HasValue)
                return null;

            float u = used.Value;
            float t = total.Value;

            // A driver reporting nonsense must not paint a bogus load: NaN and infinity
            // survive the arithmetic silently, and a zero total would divide by zero.
            if (float.IsNaN(u) || float.IsInfinity(u) || float.IsNaN(t) || float.IsInfinity(t))
                return null;
            if (t <= 0f || u < 0f)
                return null;

            // Clamped rather than trusted: some drivers count memory reserved by the OS on
            // top of what is allocated, which can read as slightly over the total.
            return Math.Clamp(u / t * 100f, 0f, 100f);
        }
    }
}
