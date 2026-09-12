using System;
using System.IO;
using SystemBarIndicator.Configuration;
using Xunit;

namespace SystemBarIndicator.Tests
{
    public class SettingsStoreTests : IDisposable
    {
        private readonly string _dir;
        private readonly string _path;

        public SettingsStoreTests()
        {
            // A separate directory per test: the atomic write leaves a .tmp alongside, and
            // GetTempFileName also creates an empty file — we want neither effect here.
            _dir = Path.Combine(Path.GetTempPath(), "sbi-tests-" + Path.GetRandomFileName());
            Directory.CreateDirectory(_dir);
            _path = Path.Combine(_dir, SettingsStore.FileName);
        }

        public void Dispose()
        {
            try { Directory.Delete(_dir, recursive: true); }
            catch (IOException) { }
        }

        [Fact]
        public void Load_MissingFile_ReturnsDefaultsWithoutError()
        {
            var s = SettingsStore.Load(_path, out var error);

            Assert.Null(error);
            Assert.Equal(AppSettings.DefaultOpacity, s.Opacity, 6);
            Assert.Equal(ThemeNames.Dark, s.Theme);
            Assert.Null(s.BarRight);
            Assert.Null(s.BarTop);
        }

        [Fact]
        public void SaveThenLoad_RoundTripsEveryField()
        {
            var source = new AppSettings
            {
                BarRight = 1885,
                BarTop = 1035,
                Opacity = 0.71,
                Theme = ThemeNames.Custom,
                BackgroundColor = "#FF203040",
                FontScale = 1.25,
                UpdateIntervalMs = 2000,
                UseFahrenheit = true,
                AutoUpdate = false,
                ShowCpu = false,
                ShowCpuTemp = false,
                ShowRam = false,
                ShowGpu = false,
                ShowGpuTemp = false,
                ShowGpuVramUsage = false,
                ShowGpuVramTemp = false,
                ShowDisk = false,
                ShowNetwork = true
            };

            SettingsStore.Save(_path, source);
            var loaded = SettingsStore.Load(_path, out var error);

            Assert.Null(error);
            Assert.Equal(1885, loaded.BarRight);
            Assert.Equal(1035, loaded.BarTop);
            Assert.Equal(0.71, loaded.Opacity, 6);
            Assert.Equal(ThemeNames.Custom, loaded.Theme);
            Assert.Equal("#FF203040", loaded.BackgroundColor);
            Assert.Equal(1.25, loaded.FontScale, 6);
            Assert.Equal(2000, loaded.UpdateIntervalMs);
            Assert.True(loaded.UseFahrenheit);
            Assert.False(loaded.AutoUpdate);
            Assert.False(loaded.ShowCpu);
            Assert.False(loaded.ShowCpuTemp);
            Assert.False(loaded.ShowRam);
            Assert.False(loaded.ShowGpu);
            Assert.False(loaded.ShowGpuTemp);
            Assert.False(loaded.ShowGpuVramUsage);
            Assert.False(loaded.ShowGpuVramTemp);
            Assert.False(loaded.ShowDisk);
            Assert.True(loaded.ShowNetwork);
        }

        [Theory]
        [InlineData(nameof(AppSettings.ShowCpu))]
        [InlineData(nameof(AppSettings.ShowCpuTemp))]
        [InlineData(nameof(AppSettings.ShowRam))]
        [InlineData(nameof(AppSettings.ShowGpu))]
        [InlineData(nameof(AppSettings.ShowGpuTemp))]
        [InlineData(nameof(AppSettings.ShowGpuVramUsage))]
        [InlineData(nameof(AppSettings.ShowGpuVramTemp))]
        [InlineData(nameof(AppSettings.ShowDisk))]
        [InlineData(nameof(AppSettings.ShowNetwork))]
        public void Save_WritesEverySectionFlag(string propertyName)
        {
            // The flags must be properties: System.Text.Json would silently skip plain
            // fields, and the section selection would never be persisted at all.
            SettingsStore.Save(_path, new AppSettings());
            Assert.Contains(propertyName, File.ReadAllText(_path), StringComparison.Ordinal);
        }

        [Fact]
        public void Load_PartialJson_KeepsDefaultsForMissingFields()
        {
            File.WriteAllText(_path, "{\"Opacity\": 0.5}");

            var s = SettingsStore.Load(_path, out var error);

            Assert.Null(error);
            Assert.Equal(0.5, s.Opacity, 6);
            Assert.Equal(ThemeNames.Dark, s.Theme);
            Assert.Equal(AppSettings.DefaultIntervalMs, s.UpdateIntervalMs);
            Assert.True(s.ShowNetwork);
        }

        [Fact]
        public void Load_BrokenJson_ReturnsDefaultsAndReportsError()
        {
            File.WriteAllText(_path, "{{{ this is not json");

            var s = SettingsStore.Load(_path, out var error);

            Assert.NotNull(error);
            Assert.Equal(AppSettings.DefaultOpacity, s.Opacity, 6);
        }

        [Fact]
        public void Load_NullDocument_ReturnsDefaults()
        {
            // "null" is valid JSON that yields null: without an explicit check this would
            // be a NullReferenceException.
            File.WriteAllText(_path, "null");

            var s = SettingsStore.Load(_path, out var error);

            Assert.Null(error);
            Assert.Equal(ThemeNames.Dark, s.Theme);
        }

        [Fact]
        public void Load_EmptyFile_ReturnsDefaults()
        {
            File.WriteAllText(_path, "   ");

            var s = SettingsStore.Load(_path, out var error);

            Assert.Null(error);
            Assert.Equal(AppSettings.DefaultOpacity, s.Opacity, 6);
        }

        [Fact]
        public void Load_NullStringProperty_DoesNotProduceNullTheme()
        {
            // Nullable annotations do not affect deserialization — null reaches the property.
            File.WriteAllText(_path, "{\"Theme\": null, \"BackgroundColor\": null}");

            var s = SettingsStore.Load(_path, out var error);

            Assert.Null(error);
            Assert.Equal(ThemeNames.Dark, s.Theme);
            Assert.Equal(ColorCodec.Default, s.BackgroundColor);
        }

        [Fact]
        public void Load_OutOfRangeValues_AreNormalized()
        {
            File.WriteAllText(_path, "{\"Opacity\": 42, \"FontScale\": 0.01, \"UpdateIntervalMs\": 137, \"Theme\": \"neon\"}");

            var s = SettingsStore.Load(_path, out _);

            Assert.Equal(AppSettings.MaxOpacity, s.Opacity, 6);
            Assert.Equal(AppSettings.MinFontScale, s.FontScale, 6);
            Assert.Equal(500, s.UpdateIntervalMs);
            Assert.Equal(ThemeNames.Dark, s.Theme);
        }

        [Fact]
        public void Load_UnknownFields_AreIgnored()
        {
            File.WriteAllText(_path, "{\"Opacity\": 0.8, \"BrandNewField\": [1,2,3]}");

            var s = SettingsStore.Load(_path, out var error);

            Assert.Null(error);
            Assert.Equal(0.8, s.Opacity, 6);
        }

        [Fact]
        public void Load_JsonWithCommentsAndTrailingComma_IsAccepted()
        {
            // The file is documented as hand-editable — small liberties must not break it.
            File.WriteAllText(_path, "{\n  // opacity\n  \"Opacity\": 0.6,\n}");

            var s = SettingsStore.Load(_path, out var error);

            Assert.Null(error);
            Assert.Equal(0.6, s.Opacity, 6);
        }

        [Fact]
        public void Save_NonFiniteValues_DoNotThrow()
        {
            // NaN cannot be written to JSON — Save must persist a normalized copy.
            var broken = new AppSettings { Opacity = double.NaN, FontScale = double.PositiveInfinity };

            SettingsStore.Save(_path, broken);
            var loaded = SettingsStore.Load(_path, out var error);

            Assert.Null(error);
            Assert.Equal(AppSettings.DefaultOpacity, loaded.Opacity, 6);
            Assert.Equal(AppSettings.DefaultFontScale, loaded.FontScale, 6);
        }

        [Fact]
        public void Save_OverwritesPreviousFileAndLeavesNoTempBehind()
        {
            SettingsStore.Save(_path, new AppSettings { Opacity = 0.5 });
            SettingsStore.Save(_path, new AppSettings { Opacity = 0.9 });

            var s = SettingsStore.Load(_path, out _);

            Assert.Equal(0.9, s.Opacity, 6);
            Assert.False(File.Exists(_path + ".tmp"));
        }

        [Fact]
        public void Save_CreatesMissingDirectory()
        {
            string nested = Path.Combine(_dir, "sub", "dir", SettingsStore.FileName);

            SettingsStore.Save(nested, new AppSettings());

            Assert.True(File.Exists(nested));
        }

        [Fact]
        public void DefaultPath_PointsNextToApplicationLog()
        {
            string expectedDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SystemBarIndicator");

            Assert.Equal(Path.Combine(expectedDir, SettingsStore.FileName), SettingsStore.DefaultPath);
        }
    }
}
