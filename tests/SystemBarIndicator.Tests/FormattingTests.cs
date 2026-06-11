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
    }
}
