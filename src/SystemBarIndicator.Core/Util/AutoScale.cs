using System;

namespace SystemBarIndicator.Util
{
    /// <summary>
    /// Адаптивный масштаб «полной шкалы» для величин без фиксированного максимума
    /// (например, скорость сети). Мгновенно поднимается под новый пик, чтобы значение
    /// не выходило за шкалу, и плавно опускается, когда активность спадает.
    /// </summary>
    public sealed class AutoScale
    {
        private readonly float _floor;
        private readonly float _decay;

        /// <param name="floor">Минимальная полная шкала (значение никогда не опускается ниже).</param>
        /// <param name="decay">Множитель затухания за шаг в (0..1), например 0.97.</param>
        public AutoScale(float floor, float decay)
        {
            if (floor <= 0f)
                throw new ArgumentOutOfRangeException(nameof(floor), floor, "floor должен быть положительным.");
            if (decay <= 0f || decay >= 1f)
                throw new ArgumentOutOfRangeException(nameof(decay), decay, "decay должен быть в (0..1).");
            _floor = floor;
            _decay = decay;
            Value = floor;
        }

        /// <summary>Текущая полная шкала.</summary>
        public float Value { get; private set; }

        /// <summary>Обновляет шкалу по текущему значению и возвращает её.</summary>
        public float Update(float current)
        {
            Value = current > Value ? current : Math.Max(_floor, Value * _decay);
            return Value;
        }

        /// <summary>Доля заполнения (0..1) для значения относительно текущей шкалы.</summary>
        public float Fraction(float value)
        {
            if (Value <= 0f) return 0f;
            float f = value / Value;
            if (f < 0f) return 0f;
            return f > 1f ? 1f : f;
        }
    }
}
