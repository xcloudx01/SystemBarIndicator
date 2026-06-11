namespace SystemBarIndicator.Monitoring
{
    /// <summary>
    /// Уровень нагрузки/нагрева для цветовой индикации.
    /// </summary>
    public enum LoadLevel
    {
        Normal,
        Elevated,
        High,
        Critical
    }

    /// <summary>
    /// Пороги перехода между уровнями нагрузки. Вынесены из UI, чтобы их можно
    /// было покрыть тестами независимо от рисования.
    /// </summary>
    public static class LoadLevels
    {
        /// <summary>Уровень по доле нагрузки в диапазоне 0..1 (например, CPU/RAM/GPU).</summary>
        public static LoadLevel FromFraction(float fraction)
        {
            if (fraction >= 0.9f) return LoadLevel.Critical;
            if (fraction >= 0.8f) return LoadLevel.High;
            if (fraction >= 0.6f) return LoadLevel.Elevated;
            return LoadLevel.Normal;
        }

        /// <summary>Уровень по температуре в градусах Цельсия.</summary>
        public static LoadLevel FromTemperature(float celsius)
        {
            if (celsius >= 90f) return LoadLevel.Critical;
            if (celsius >= 75f) return LoadLevel.High;
            if (celsius >= 60f) return LoadLevel.Elevated;
            return LoadLevel.Normal;
        }
    }
}
