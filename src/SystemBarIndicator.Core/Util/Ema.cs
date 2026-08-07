using System;

namespace SystemBarIndicator.Util
{
    /// <summary>
    /// Экспоненциальное скользящее среднее. Сглаживает рваные показания
    /// (например, мгновенную скорость сети), чтобы значение менялось плавно.
    /// </summary>
    public sealed class Ema
    {
        private float _alpha;
        private bool _initialized;

        /// <param name="alpha">
        /// Коэффициент сглаживания в диапазоне (0..1]. Ближе к 1 — быстрее реагирует
        /// и меньше сглаживает; ближе к 0 — плавнее, но с большей задержкой.
        /// </param>
        public Ema(float alpha)
        {
            Alpha = alpha;
        }

        /// <summary>
        /// Коэффициент сглаживания (0..1]. Меняется на лету при смене интервала опроса:
        /// одно и то же «время сглаживания» требует разного alpha при разном шаге
        /// (см. <see cref="SmoothingRates.AlphaForInterval"/>). Накопленное значение сохраняется.
        /// </summary>
        public float Alpha
        {
            get => _alpha;
            set
            {
                if (value <= 0f || value > 1f)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "alpha должен быть в (0..1].");
                _alpha = value;
            }
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
