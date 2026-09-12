using SystemBarIndicator.Monitoring;
using Xunit;

namespace SystemBarIndicator.Tests
{
    public class VramLoadTests
    {
        [Theory]
        [InlineData(0f, 10240f, 0f)]
        [InlineData(3167f, 10240f, 30.928f)]
        [InlineData(10240f, 10240f, 100f)]
        public void Percent_IsTheUsedShareOfTheTotal(float used, float total, float expected)
        {
            Assert.Equal(expected, VramLoad.Percent(used, total)!.Value, 3);
        }

        [Fact]
        public void Percent_IsUnitAgnostic()
        {
            // Only the ratio is used, so megabytes and bytes must agree.
            Assert.Equal(VramLoad.Percent(3167f, 10240f), VramLoad.Percent(3167f * 1048576f, 10240f * 1048576f));
        }

        [Theory]
        [InlineData(null, 10240f)]
        [InlineData(3167f, null)]
        [InlineData(null, null)]
        public void Percent_NeedsBothHalvesOfThePair(float? used, float? total)
        {
            Assert.Null(VramLoad.Percent(used, total));
        }

        [Theory]
        [InlineData(3167f, 0f)]     // a card that reports no total at all
        [InlineData(3167f, -1f)]
        [InlineData(-1f, 10240f)]
        public void Percent_RefusesAnUnusablePair(float used, float total)
        {
            Assert.Null(VramLoad.Percent(used, total));
        }

        [Theory]
        [InlineData(float.NaN, 10240f)]
        [InlineData(3167f, float.NaN)]
        [InlineData(float.PositiveInfinity, 10240f)]
        [InlineData(3167f, float.PositiveInfinity)]
        public void Percent_RefusesNonFiniteReadings(float used, float total)
        {
            // NaN and infinity survive the arithmetic silently and would be drawn as a bar.
            Assert.Null(VramLoad.Percent(used, total));
        }

        [Fact]
        public void Percent_ClampsAboveTotal()
        {
            // Some drivers count memory reserved by the OS on top of what is allocated.
            Assert.Equal(100f, VramLoad.Percent(10500f, 10240f)!.Value, 3);
        }
    }
}
