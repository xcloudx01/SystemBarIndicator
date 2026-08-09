using System;
using SystemBarIndicator.Util;
using Xunit;

namespace SystemBarIndicator.Tests
{
    public class AutoScaleTests
    {
        [Fact]
        public void StartsAtFloor()
        {
            var scale = new AutoScale(floor: 64f, decay: 0.97f);
            Assert.Equal(64f, scale.Value);
        }

        [Fact]
        public void RisesImmediatelyToFitPeak()
        {
            var scale = new AutoScale(64f, 0.97f);
            scale.Update(5000f);
            Assert.Equal(5000f, scale.Value);
        }

        [Fact]
        public void NeverExceedsScale_FractionStaysWithinUnit()
        {
            var scale = new AutoScale(64f, 0.97f);
            scale.Update(5000f);
            Assert.Equal(1f, scale.Fraction(5000f)); // the peak is the full scale
            Assert.InRange(scale.Fraction(2500f), 0.49f, 0.51f);
        }

        [Fact]
        public void DecaysSlowlyWhenActivityDrops_ButNotBelowFloor()
        {
            var scale = new AutoScale(64f, 0.9f);
            scale.Update(1000f);          // scale = 1000
            for (int i = 0; i < 5; i++)
                scale.Update(0f);          // decay
            Assert.True(scale.Value < 1000f, "the scale should shrink");
            for (int i = 0; i < 200; i++)
                scale.Update(0f);
            Assert.Equal(64f, scale.Value); // but never below the floor
        }

        [Fact]
        public void Fraction_ClampsAndHandlesZero()
        {
            var scale = new AutoScale(100f, 0.97f);
            Assert.Equal(0f, scale.Fraction(-10f));
            Assert.Equal(1f, scale.Fraction(99999f));
        }

        [Theory]
        [InlineData(0f, 0.97f)]
        [InlineData(64f, 1f)]
        [InlineData(64f, 0f)]
        public void Constructor_RejectsInvalidArgs(float floor, float decay)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new AutoScale(floor, decay));
        }

        [Fact]
        public void Decay_CanBeChangedOnTheFly_KeepingCurrentScale()
        {
            // Changing the poll interval changes the decay, but not the current scale.
            var scale = new AutoScale(64f, 0.9f);
            scale.Update(1000f);

            scale.Decay = 0.5f;

            Assert.Equal(0.5f, scale.Decay);
            Assert.Equal(1000f, scale.Value);
            Assert.Equal(500f, scale.Update(0f));
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(1f)]
        [InlineData(-0.5f)]
        public void Decay_RejectsValueOutOfRange(float decay)
        {
            var scale = new AutoScale(64f, 0.97f);
            Assert.Throws<ArgumentOutOfRangeException>(() => scale.Decay = decay);
        }
    }
}
