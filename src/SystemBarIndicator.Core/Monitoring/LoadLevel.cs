namespace SystemBarIndicator.Monitoring
{
    /// <summary>
    /// Load/heat level used for colour coding.
    /// </summary>
    public enum LoadLevel
    {
        Normal,
        Elevated,
        High,
        Critical
    }

    /// <summary>
    /// Thresholds between load levels. Kept out of the UI so they can be tested
    /// independently of drawing.
    /// </summary>
    public static class LoadLevels
    {
        /// <summary>Level from a load fraction in 0..1 (CPU/RAM/GPU, for example).</summary>
        public static LoadLevel FromFraction(float fraction)
        {
            if (fraction >= 0.9f) return LoadLevel.Critical;
            if (fraction >= 0.8f) return LoadLevel.High;
            if (fraction >= 0.6f) return LoadLevel.Elevated;
            return LoadLevel.Normal;
        }

        /// <summary>Level from a temperature in degrees Celsius.</summary>
        public static LoadLevel FromTemperature(float celsius)
        {
            if (celsius >= 90f) return LoadLevel.Critical;
            if (celsius >= 75f) return LoadLevel.High;
            if (celsius >= 60f) return LoadLevel.Elevated;
            return LoadLevel.Normal;
        }
    }
}
