using System;

namespace SystemBarIndicator.Monitoring
{
    /// <summary>
    /// Turns raw kernel CPU time counters into a load percentage. Kept separate from the
    /// sensor so the arithmetic — which is all edge cases — can be tested.
    /// </summary>
    public static class CpuLoad
    {
        /// <summary>
        /// Load between two samples of the cumulative counters. A counter that went backwards
        /// means the source was reset (or the machine resumed oddly): the sample is dropped
        /// rather than turned into a spike.
        /// </summary>
        public static float FromSamples(ulong previousIdle, ulong previousTotal, ulong currentIdle, ulong currentTotal)
        {
            if (currentIdle < previousIdle || currentTotal < previousTotal)
                return 0f;

            return FromDeltas(currentIdle - previousIdle, currentTotal - previousTotal);
        }

        /// <summary>
        /// Busy share of <paramref name="totalDelta"/>, as a percentage.
        /// </summary>
        public static float FromDeltas(ulong idleDelta, ulong totalDelta)
        {
            // Both guards run BEFORE the subtraction: these are unsigned, so an idle delta
            // larger than the total would wrap around to an enormous number and clamp to
            // 100 % — the exact opposite of the truth.
            if (totalDelta == 0)
                return 0f;
            if (idleDelta >= totalDelta)
                return 0f;

            double busy = (double)(totalDelta - idleDelta) / totalDelta * 100.0;
            return (float)Math.Clamp(busy, 0.0, 100.0);
        }
    }
}
