using System;
using SystemBarIndicator.Util;
using Xunit;

namespace SystemBarIndicator.Tests
{
    public class SmoothingRatesTests
    {
        [Fact]
        public void AlphaForInterval_KeepsBaseValueAtOneSecond()
        {
            Assert.Equal(0.4f, SmoothingRates.AlphaForInterval(0.4f, 1000), 5);
        }

        [Fact]
        public void AlphaForInterval_ShorterStepReactsLess()
        {
            float half = SmoothingRates.AlphaForInterval(0.4f, 500);
            float full = SmoothingRates.AlphaForInterval(0.4f, 1000);

            Assert.True(half < full);
            // 1 - sqrt(0.6)
            Assert.Equal(0.2254f, half, 4);
        }

        [Fact]
        public void AlphaForInterval_LongerStepReactsMore()
        {
            float five = SmoothingRates.AlphaForInterval(0.4f, 5000);

            Assert.True(five > 0.4f);
            Assert.True(five < 1f);
            Assert.Equal(1f - MathF.Pow(0.6f, 5f), five, 4);
        }

        [Fact]
        public void AlphaForInterval_TwoShortStepsEqualOneLongStep()
        {
            // Смысл пересчёта: за одну секунду сглаживание «съедает» одинаковую долю
            // независимо от того, за сколько шагов эта секунда набрана.
            float half = SmoothingRates.AlphaForInterval(0.4f, 500);
            float retainedAfterTwoSteps = (1f - half) * (1f - half);

            Assert.Equal(0.6f, retainedAfterTwoSteps, 4);
        }

        [Fact]
        public void AlphaForInterval_StaysInValidRange()
        {
            foreach (int ms in new[] { 500, 1000, 2000, 5000, 60000 })
            {
                float alpha = SmoothingRates.AlphaForInterval(0.4f, ms);
                Assert.InRange(alpha, 0.0001f, 1f);
                _ = new Ema(alpha); // конструктор Ema не должен ругаться на результат
            }
        }

        [Fact]
        public void AlphaForInterval_OneStaysOne()
        {
            Assert.Equal(1f, SmoothingRates.AlphaForInterval(1f, 5000), 5);
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(-0.1f)]
        [InlineData(1.5f)]
        public void AlphaForInterval_RejectsInvalidAlpha(float alpha)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SmoothingRates.AlphaForInterval(alpha, 1000));
        }

        [Fact]
        public void AlphaForInterval_RejectsNonPositiveInterval()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SmoothingRates.AlphaForInterval(0.4f, 0));
        }

        [Fact]
        public void DecayForInterval_KeepsBaseValueAtOneSecond()
        {
            Assert.Equal(0.97f, SmoothingRates.DecayForInterval(0.97f, 1000), 5);
        }

        [Fact]
        public void DecayForInterval_ScalesWithStepLength()
        {
            float half = SmoothingRates.DecayForInterval(0.97f, 500);
            float five = SmoothingRates.DecayForInterval(0.97f, 5000);

            Assert.True(half > 0.97f);            // за полшага спадает меньше
            Assert.True(five < 0.97f);            // за пять секунд — сильнее
            Assert.Equal(MathF.Pow(0.97f, 5f), five, 4);
        }

        [Fact]
        public void DecayForInterval_TwoShortStepsEqualOneLongStep()
        {
            float half = SmoothingRates.DecayForInterval(0.97f, 500);
            Assert.Equal(0.97f, half * half, 4);
        }

        [Fact]
        public void DecayForInterval_StaysInValidRange()
        {
            foreach (int ms in new[] { 500, 1000, 2000, 5000, 60000 })
            {
                float decay = SmoothingRates.DecayForInterval(0.97f, ms);
                Assert.InRange(decay, 0.0001f, 0.9999f);
                _ = new AutoScale(1f, decay); // конструктор AutoScale не должен ругаться
            }
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(1f)]
        [InlineData(1.2f)]
        public void DecayForInterval_RejectsInvalidDecay(float decay)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SmoothingRates.DecayForInterval(decay, 1000));
        }
    }
}
