using SystemBarIndicator.Configuration;
using Xunit;

namespace SystemBarIndicator.Tests
{
    public class ColorCodecTests
    {
        [Theory]
        [InlineData("#FF191919", unchecked((int)0xFF191919))]
        [InlineData("FF191919", unchecked((int)0xFF191919))]
        [InlineData("#191919", unchecked((int)0xFF191919))]      // no alpha means opaque
        [InlineData("#ff191919", unchecked((int)0xFF191919))]    // case does not matter
        [InlineData("  #FF191919  ", unchecked((int)0xFF191919))]
        [InlineData("#80FFFFFF", unchecked((int)0x80FFFFFF))]
        public void TryParse_AcceptsSupportedForms(string text, int expected)
        {
            Assert.True(ColorCodec.TryParse(text, out int argb));
            Assert.Equal(expected, argb);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("#FFF")]
        [InlineData("#FF19191")]
        [InlineData("#FF1919199")]
        [InlineData("#GGGGGG")]
        [InlineData("rgb(1,2,3)")]
        public void TryParse_RejectsGarbage(string? text)
        {
            Assert.False(ColorCodec.TryParse(text, out int argb));
            Assert.Equal(0, argb);
        }

        [Fact]
        public void ToHex_RoundTrips()
        {
            int argb = unchecked((int)0xCC123456);
            Assert.Equal("#CC123456", ColorCodec.ToHex(argb));
            Assert.True(ColorCodec.TryParse(ColorCodec.ToHex(argb), out int parsed));
            Assert.Equal(argb, parsed);
        }

        [Fact]
        public void Normalize_CanonicalisesOrFallsBack()
        {
            Assert.Equal("#FF191919", ColorCodec.Normalize("191919"));
            Assert.Equal("#FF191919", ColorCodec.Normalize("#ff191919"));
            Assert.Equal(ColorCodec.Default, ColorCodec.Normalize("garbage"));
            Assert.Equal(ColorCodec.Default, ColorCodec.Normalize(null));
        }

        [Fact]
        public void ParseOrDefault_UsesFallbackOnGarbage()
        {
            Assert.Equal(7, ColorCodec.ParseOrDefault("not a colour", 7));
            Assert.Equal(unchecked((int)0xFF010203), ColorCodec.ParseOrDefault("#FF010203", 7));
        }

        [Theory]
        [InlineData(unchecked((int)0xFF000000), true)]
        [InlineData(unchecked((int)0xFF191919), true)]
        [InlineData(unchecked((int)0xFF202030), true)]
        [InlineData(unchecked((int)0xFFFFFFFF), false)]
        [InlineData(unchecked((int)0xFFF0F0F0), false)]
        [InlineData(unchecked((int)0xFF00FF00), false)]   // green is brighter than the threshold
        [InlineData(unchecked((int)0xFF0000FF), true)]    // blue counts as dark by luminance
        public void IsDark_FollowsRelativeLuminance(int argb, bool expected)
        {
            Assert.Equal(expected, ColorCodec.IsDark(argb));
        }

        [Fact]
        public void IsDark_IgnoresAlpha()
        {
            Assert.Equal(ColorCodec.IsDark(unchecked((int)0xFF191919)),
                         ColorCodec.IsDark(unchecked((int)0x00191919)));
        }
    }
}
