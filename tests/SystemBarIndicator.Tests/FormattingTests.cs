using SystemBarIndicator.Util;
using Xunit;

namespace SystemBarIndicator.Tests
{
    public class FormattingTests
    {
        [Theory]
        [InlineData(0f, "0B/s")]
        [InlineData(0.5f, "512B/s")]    // 0.5 * 1024
        [InlineData(1f, "1KB/s")]
        [InlineData(512f, "512KB/s")]
        [InlineData(1023f, "1023KB/s")]
        [InlineData(1024f, "1.0MB/s")]
        [InlineData(1536f, "1.5MB/s")]  // 1.5 * 1024
        [InlineData(2048f, "2.0MB/s")]
        public void FormatSpeed_SelectsUnitAndPrecision(float kbps, string expected)
        {
            Assert.Equal(expected, Formatting.FormatSpeed(kbps));
        }

        [Fact]
        public void FormatSpeed_UsesInvariantDecimalPoint()
        {
            // Independent of the current culture — always a dot, never a comma.
            Assert.Equal("1.5MB/s", Formatting.FormatSpeed(1536f));
        }

        [Theory]
        [InlineData(0f, "0%")]
        [InlineData(7.4f, "7%")]
        [InlineData(99.9f, "100%")]
        [InlineData(100f, "100%")]
        [InlineData(150f, "100%")]          // clamped above: never wider than the "100%" slot
        [InlineData(-5f, "0%")]             // clamped below
        [InlineData(float.NaN, "0%")]
        [InlineData(float.PositiveInfinity, "100%")]
        public void FormatPercent_ClampsAndFormats(float value, string expected)
        {
            Assert.Equal(expected, Formatting.FormatPercent(value));
        }

        [Theory]
        [InlineData(0f, "0C")]
        [InlineData(45.6f, "46C")]
        [InlineData(105f, "105C")]
        [InlineData(1500f, "999C")]         // clamped above: never wider than the "999C" slot
        [InlineData(-10f, "0C")]            // clamped below
        [InlineData(float.NaN, "0C")]
        public void FormatTemperature_ClampsAndFormats(float value, string expected)
        {
            Assert.Equal(expected, Formatting.FormatTemperature(value));
        }

        [Theory]
        [InlineData(0f, "32F")]
        [InlineData(100f, "212F")]
        [InlineData(45.6f, "114F")]         // 45.6C = 114.08F
        [InlineData(600f, "999F")]          // clamped above AFTER the conversion
        [InlineData(-10f, "32F")]           // clamped below to 0C, then converted
        [InlineData(float.NaN, "32F")]
        public void FormatTemperature_Fahrenheit_ConvertsAndClamps(float celsius, string expected)
        {
            Assert.Equal(expected, Formatting.FormatTemperature(celsius, fahrenheit: true));
        }

        [Fact]
        public void FormatTemperature_CelsiusOverloadMatchesExplicitCelsius()
        {
            Assert.Equal(Formatting.FormatTemperature(63f), Formatting.FormatTemperature(63f, fahrenheit: false));
        }

        [Fact]
        public void TemperatureSlotTemplate_FitsAnyFormattedValue()
        {
            // The slot template must be at least as wide as the longest possible string.
            foreach (bool fahrenheit in new[] { false, true })
            {
                string template = Formatting.TemperatureSlotTemplate(fahrenheit);
                foreach (float c in new[] { 0f, 9f, 42f, 99f, 150f, 999f, float.NaN })
                    Assert.True(Formatting.FormatTemperature(c, fahrenheit).Length <= template.Length);
            }
        }
    }
}
