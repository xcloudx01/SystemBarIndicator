using System;

namespace SystemBarIndicator.Util
{
    /// <summary>
    /// Экспоненциальное скользящее среднее. Сглаживает рваные показания
    /// (например, мгновенную скорость сети), чтобы значение менялось плавно.
    /// </summary>
    public sealed class Ema
    {
        private readonly float _alpha;
        private bool _initialized;

        /// <param name="alpha">
        /// Коэффициент сглаживания в диапазоне (0..1]. Ближе к 1 — быстрее реагирует
        /// и меньше сглаживает; ближе к 0 — плавнее, но с большей задержкой.
        /// </param>
        public Ema(float alpha)
        {
            if (alpha <= 0f || alpha > 1f)
                throw new ArgumentOutOfRangeException(nameof(alpha), alpha, "alpha должен быть в (0..1].");
            _alpha = alpha;
        }

        public float Value { get; private set; }

        /// <summary>Добавляет новое измерение и возвращает сглаженное значение.</summary>
        public float Update(float sample)
        {
            Value = _initialized ? Value + _alpha * (sample - Value) : sample;
            _initialized = true;
            return Value;
        }

        public void Reset()
        {
            _initialized = false;
            Value = 0f;
        }
    }
}
