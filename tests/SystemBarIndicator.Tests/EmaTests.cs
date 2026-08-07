using System;
using SystemBarIndicator.Util;
using Xunit;

namespace SystemBarIndicator.Tests
{
    public class EmaTests
    {
        [Fact]
        public void FirstSample_IsReturnedAsIs()
        {
            var ema = new Ema(0.4f);
            Assert.Equal(100f, ema.Update(100f));
        }

        [Fact]
        public void SecondSample_BlendsWithAlpha()
        {
            var ema = new Ema(0.5f);
            ema.Update(100f);
            // 100 + 0.5 * (0 - 100) = 50
            Assert.Equal(50f, ema.Update(0f));
        }

        [Fact]
        public void RepeatedSameSample_ConvergesToIt()
        {
            var ema = new Ema(0.3f);
            ema.Update(0f);
            for (int i = 0; i < 200; i++)
                ema.Update(500f);
            Assert.True(Math.Abs(ema.Value - 500f) < 0.5f, $"ожидалось ~500, получено {ema.Value}");
        }

        [Fact]
        public void Smooths_BurstyInput_LessThanRawSwing()
        {
            var ema = new Ema(0.3f);
            ema.Update(0f);
            // Чередование 0 и 1000 — сглаженное значение держится в середине, не прыгает на всю амплитуду.
            ema.Update(1000f);
            ema.Update(0f);
            ema.Update(1000f);
            Assert.InRange(ema.Value, 1f, 999f);
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(-0.1f)]
        [InlineData(1.1f)]
        public void Constructor_RejectsAlphaOutOfRange(float alpha)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Ema(alpha));
        }

        [Fact]
        public void Alpha_CanBeChangedOnTheFly_KeepingAccumulatedValue()
        {
            // Смена интервала опроса меняет alpha, но не должна сбрасывать сглаженное значение.
            var ema = new Ema(0.5f);
            ema.Update(100f);

            ema.Alpha = 0.25f;

            Assert.Equal(0.25f, ema.Alpha);
            Assert.Equal(100f, ema.Value);
            // 100 + 0.25 * (0 - 100) = 75
            Assert.Equal(75f, ema.Update(0f));
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(-0.1f)]
        [InlineData(1.1f)]
        public void Alpha_RejectsValueOutOfRange(float alpha)
        {
            var ema = new Ema(0.4f);
            Assert.Throws<ArgumentOutOfRangeException>(() => ema.Alpha = alpha);
        }
    }
}
