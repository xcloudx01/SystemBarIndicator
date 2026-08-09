using System;

namespace SystemBarIndicator.Util
{
    /// <summary>
    /// Exponential moving average. Smooths jagged readings (an instantaneous network
    /// speed, say) so the value changes gradually.
    /// </summary>
    public sealed class Ema
    {
        private float _alpha;
        private bool _initialized;

        /// <param name="alpha">
        /// Smoothing factor in (0..1]. Closer to 1 reacts faster and smooths less;
        /// closer to 0 is smoother but lags more.
        /// </param>
        public Ema(float alpha)
        {
            Alpha = alpha;
        }

        /// <summary>
        /// Smoothing factor (0..1]. Changed on the fly when the poll interval changes:
        /// the same "smoothing time" needs a different alpha at a different step
        /// (see <see cref="SmoothingRates.AlphaForInterval"/>). The accumulated value is kept.
        /// </summary>
        public float Alpha
        {
            get => _alpha;
            set
            {
                if (value <= 0f || value > 1f)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "alpha must be in (0..1].");
                _alpha = value;
            }
        }

        public float Value { get; private set; }

        /// <summary>Adds a new sample and returns the smoothed value.</summary>
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
