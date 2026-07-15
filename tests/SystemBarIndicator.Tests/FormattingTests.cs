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
            // Не зависит от текущей культуры — всегда точка, а не запятая.
            Assert.Equal("1.5MB/s", Formatting.FormatSpeed(1536f));
        }

        [Theory]
        [InlineData(0f, "0%")]
        [InlineData(7.4f, "7%")]
        [InlineData(99.9f, "100%")]
        [InlineData(100f, "100%")]
        [InlineData(150f, "100%")]          // clamp сверху: не шире шаблона слота "100%"
        [InlineData(-5f, "0%")]             // clamp снизу
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
        [InlineData(1500f, "999C")]         // clamp сверху: не шире шаблона слота "999C"
        [InlineData(-10f, "0C")]            // clamp снизу
        [InlineData(float.NaN, "0C")]
        public void FormatTemperature_ClampsAndFormats(float value, string expected)
        {
            Assert.Equal(expected, Formatting.FormatTemperature(value));
        }
    }
}
