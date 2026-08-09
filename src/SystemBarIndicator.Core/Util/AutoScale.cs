using System;

namespace SystemBarIndicator.Util
{
    /// <summary>
    /// Adaptive full-scale value for quantities with no fixed maximum (network speed,
    /// for instance). Jumps up immediately to accommodate a new peak, so a value never
    /// exceeds the scale, and decays gently once activity subsides.
    /// </summary>
    public sealed class AutoScale
    {
        private readonly float _floor;
        private float _decay;

        /// <param name="floor">Minimum full scale (the value never drops below it).</param>
        /// <param name="decay">Per-step decay factor in (0..1), e.g. 0.97.</param>
        public AutoScale(float floor, float decay)
        {
            if (floor <= 0f)
                throw new ArgumentOutOfRangeException(nameof(floor), floor, "floor must be positive.");
            _floor = floor;
            Decay = decay;
            Value = floor;
        }

        /// <summary>
        /// Per-step decay factor (0..1). Changed on the fly when the poll interval changes —
        /// see <see cref="SmoothingRates.DecayForInterval"/>. The current scale is kept.
        /// </summary>
        public float Decay
        {
            get => _decay;
            set
            {
                if (value <= 0f || value >= 1f)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "decay must be in (0..1).");
                _decay = value;
            }
        }

        /// <summary>The current full scale.</summary>
        public float Value { get; private set; }

        /// <summary>Updates the scale from the current value and returns it.</summary>
        public float Update(float current)
        {
            Value = current > Value ? current : Math.Max(_floor, Value * _decay);
            return Value;
        }

        /// <summary>Fill fraction (0..1) of a value relative to the current scale.</summary>
        public float Fraction(float value)
        {
            if (Value <= 0f) return 0f;
            float f = value / Value;
            if (f < 0f) return 0f;
            return f > 1f ? 1f : f;
        }
    }
}
