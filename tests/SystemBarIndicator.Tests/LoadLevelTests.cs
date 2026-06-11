using SystemBarIndicator.Monitoring;
using Xunit;

namespace SystemBarIndicator.Tests
{
    public class LoadLevelTests
    {
        [Theory]
        [InlineData(0f, LoadLevel.Normal)]
        [InlineData(0.59f, LoadLevel.Normal)]
        [InlineData(0.60f, LoadLevel.Elevated)]
        [InlineData(0.79f, LoadLevel.Elevated)]
        [InlineData(0.80f, LoadLevel.High)]
        [InlineData(0.89f, LoadLevel.High)]
        [InlineData(0.90f, LoadLevel.Critical)]
        [InlineData(1.0f, LoadLevel.Critical)]
        public void FromFraction_MapsThresholds(float fraction, LoadLevel expected)
        {
            Assert.Equal(expected, LoadLevels.FromFraction(fraction));
        }

        [Theory]
        [InlineData(0f, LoadLevel.Normal)]
        [InlineData(59f, LoadLevel.Normal)]
        [InlineData(60f, LoadLevel.Elevated)]
        [InlineData(74f, LoadLevel.Elevated)]
        [InlineData(75f, LoadLevel.High)]
        [InlineData(89f, LoadLevel.High)]
        [InlineData(90f, LoadLevel.Critical)]
        [InlineData(100f, LoadLevel.Critical)]
        public void FromTemperature_MapsThresholds(float celsius, LoadLevel expected)
        {
            Assert.Equal(expected, LoadLevels.FromTemperature(celsius));
        }
    }
}
