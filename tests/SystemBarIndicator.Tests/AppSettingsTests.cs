using SystemBarIndicator.Configuration;
using Xunit;

namespace SystemBarIndicator.Tests
{
    public class AppSettingsTests
    {
        [Theory]
        [InlineData(0.0, AppSettings.MinOpacity)]
        [InlineData(-1.0, AppSettings.MinOpacity)]
        [InlineData(0.5, 0.5)]
        [InlineData(1.0, 1.0)]
        [InlineData(3.0, AppSettings.MaxOpacity)]
        public void Normalized_ClampsOpacity(double value, double expected)
        {
            var s = new AppSettings { Opacity = value }.Normalized();
            Assert.Equal(expected, s.Opacity, 6);
        }

        [Theory]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        [InlineData(double.NegativeInfinity)]
        public void Normalized_ReplacesNonFiniteOpacityWithDefault(double value)
        {
            var s = new AppSettings { Opacity = value }.Normalized();
            Assert.Equal(AppSettings.DefaultOpacity, s.Opacity, 6);
        }

        [Theory]
        [InlineData(0.1, AppSettings.MinFontScale)]
        [InlineData(1.25, 1.25)]
        [InlineData(9.0, AppSettings.MaxFontScale)]
        [InlineData(double.NaN, AppSettings.DefaultFontScale)]
        public void Normalized_ClampsFontScale(double value, double expected)
        {
            var s = new AppSettings { FontScale = value }.Normalized();
            Assert.Equal(expected, s.FontScale, 6);
        }

        [Theory]
        [InlineData(500, 500)]
        [InlineData(1000, 1000)]
        [InlineData(5000, 5000)]
        [InlineData(137, 500)]      // closer to 500
        [InlineData(0, 500)]
        [InlineData(-100, 500)]
        [InlineData(1400, 1000)]    // 400 vs 600 — closer to 1000
        [InlineData(1700, 2000)]
        [InlineData(999999, 5000)]  // closer to the maximum
        public void Normalized_SnapsIntervalToAllowed(int value, int expected)
        {
            var s = new AppSettings { UpdateIntervalMs = value }.Normalized();
            Assert.Equal(expected, s.UpdateIntervalMs);
        }

        [Theory]
        [InlineData("Dark", "Dark")]
        [InlineData("Light", "Light")]
        [InlineData("Custom", "Custom")]
        [InlineData("neon", "Dark")]
        [InlineData("", "Dark")]
        [InlineData(null, "Dark")]      // JSON can slip a null into a non-nullable property
        public void Normalized_FallsBackToDarkForUnknownTheme(string? theme, string expected)
        {
            var s = new AppSettings { Theme = theme! }.Normalized();
            Assert.Equal(expected, s.Theme);
        }

        [Fact]
        public void Normalized_RepairsBackgroundColor()
        {
            Assert.Equal(ColorCodec.Default, new AppSettings { BackgroundColor = "not a colour" }.Normalized().BackgroundColor);
            Assert.Equal(ColorCodec.Default, new AppSettings { BackgroundColor = null! }.Normalized().BackgroundColor);
            Assert.Equal("#FF102030", new AppSettings { BackgroundColor = "102030" }.Normalized().BackgroundColor);
        }

        [Fact]
        public void Normalized_KeepsAtLeastOneSectionVisible()
        {
            var s = new AppSettings
            {
                ShowCpu = false,
                ShowCpuTemp = false,
                ShowRam = false,
                ShowGpu = false,
                ShowGpuTemp = false,
                ShowGpuVramTemp = false,
                ShowDisk = false,
                ShowNetwork = false
            }.Normalized();

            Assert.True(s.ShowCpu);
        }

        [Fact]
        public void Normalized_DoesNotForceCpuWhenSomethingElseVisible()
        {
            var s = new AppSettings { ShowCpu = false, ShowRam = true }.Normalized();
            Assert.False(s.ShowCpu);
        }

        [Fact]
        public void Normalized_DoesNotMutateSource()
        {
            var source = new AppSettings { Opacity = 5.0, UpdateIntervalMs = 137, Theme = "neon" };
            source.Normalized();

            Assert.Equal(5.0, source.Opacity, 6);
            Assert.Equal(137, source.UpdateIntervalMs);
            Assert.Equal("neon", source.Theme);
        }

        [Fact]
        public void Normalized_PreservesPositionAndUnits()
        {
            var s = new AppSettings { BarRight = 1900, BarTop = 1040, UseFahrenheit = true }.Normalized();

            Assert.Equal(1900, s.BarRight);
            Assert.Equal(1040, s.BarTop);
            Assert.True(s.UseFahrenheit);
        }

        [Fact]
        public void Normalized_FixesNonPositiveSchemaVersion()
        {
            Assert.Equal(1, new AppSettings { SchemaVersion = 0 }.Normalized().SchemaVersion);
            Assert.Equal(2, new AppSettings { SchemaVersion = 2 }.Normalized().SchemaVersion);
        }

        [Fact]
        public void Clone_CopiesEveryField()
        {
            var source = new AppSettings
            {
                SchemaVersion = 3,
                BarRight = 100,
                BarTop = 200,
                Opacity = 0.6,
                Theme = ThemeNames.Light,
                BackgroundColor = "#FF010203",
                FontScale = 1.5,
                UpdateIntervalMs = 2000,
                UseFahrenheit = true,
                AutoUpdate = false,
                ShowCpu = false,
                ShowCpuTemp = false,
                ShowRam = false,
                ShowGpu = false,
                ShowGpuTemp = false,
                ShowGpuVramTemp = false,
                ShowDisk = false,
                ShowNetwork = true
            };

            var copy = source.Clone();

            Assert.Equal(source.SchemaVersion, copy.SchemaVersion);
            Assert.Equal(source.BarRight, copy.BarRight);
            Assert.Equal(source.BarTop, copy.BarTop);
            Assert.Equal(source.Opacity, copy.Opacity, 6);
            Assert.Equal(source.Theme, copy.Theme);
            Assert.Equal(source.BackgroundColor, copy.BackgroundColor);
            Assert.Equal(source.FontScale, copy.FontScale, 6);
            Assert.Equal(source.UpdateIntervalMs, copy.UpdateIntervalMs);
            Assert.Equal(source.UseFahrenheit, copy.UseFahrenheit);
            Assert.Equal(source.AutoUpdate, copy.AutoUpdate);
            Assert.Equal(source.ShowCpu, copy.ShowCpu);
            Assert.Equal(source.ShowCpuTemp, copy.ShowCpuTemp);
            Assert.Equal(source.ShowRam, copy.ShowRam);
            Assert.Equal(source.ShowGpu, copy.ShowGpu);
            Assert.Equal(source.ShowGpuTemp, copy.ShowGpuTemp);
            Assert.Equal(source.ShowGpuVramTemp, copy.ShowGpuVramTemp);
            Assert.Equal(source.ShowDisk, copy.ShowDisk);
            Assert.Equal(source.ShowNetwork, copy.ShowNetwork);
        }

        [Fact]
        public void DiffersInAppearance_ReactsOnlyToDrawingResources()
        {
            var a = new AppSettings();

            Assert.False(a.DiffersInAppearance(a.Clone()));

            // Opacity and interval are applied without recreating any GDI+ resources.
            var sameLook = a.Clone();
            sameLook.Opacity = 0.4;
            sameLook.UpdateIntervalMs = 5000;
            sameLook.UseFahrenheit = true;
            sameLook.ShowGpu = false;
            Assert.False(a.DiffersInAppearance(sameLook));

            var otherTheme = a.Clone();
            otherTheme.Theme = ThemeNames.Light;
            Assert.True(a.DiffersInAppearance(otherTheme));

            var otherScale = a.Clone();
            otherScale.FontScale = 1.4;
            Assert.True(a.DiffersInAppearance(otherScale));

            var otherColor = a.Clone();
            otherColor.BackgroundColor = "#FF445566";
            Assert.True(a.DiffersInAppearance(otherColor));
        }
    }
}
