using System;
using SystemBarIndicator.Updates;
using Xunit;

namespace SystemBarIndicator.Tests
{
    public class UpdateVersionTests
    {
        [Theory]
        [InlineData("v1.2.3", "1.2.3.0")]
        [InlineData("V1.2.3", "1.2.3.0")]
        [InlineData("1.2.3", "1.2.3.0")]
        [InlineData(" v1.2.3 ", "1.2.3.0")]
        [InlineData("1.2", "1.2.0.0")]
        [InlineData("1.2.3.4", "1.2.3.4")]
        public void ParseTag_AcceptsPlainVersions(string tag, string expected)
        {
            Assert.Equal(Version.Parse(expected), UpdateVersion.ParseTag(tag));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("latest")]
        [InlineData("v1")]
        [InlineData("v1.2.3-beta")] // pre-releases must never trigger an update
        [InlineData("release-2026-08-30")]
        public void ParseTag_RejectsEverythingElse(string? tag)
        {
            Assert.Null(UpdateVersion.ParseTag(tag));
        }

        [Theory]
        [InlineData("1.0.0.0", "1.0.1", true)]
        [InlineData("1.0.0.0", "1.1", true)]
        [InlineData("1.0.0.0", "2.0", true)]
        [InlineData("1.0.0.0", "1.0.0", false)] // equal after zero-padding
        [InlineData("1.0.0.0", "1.0", false)]
        [InlineData("1.1.0.0", "1.0.9", false)] // never "update" backwards
        public void IsNewer_ComparesZeroPaddedVersions(string current, string latest, bool expected)
        {
            Assert.Equal(expected, UpdateVersion.IsNewer(Version.Parse(current), Version.Parse(latest)));
        }

        [Fact]
        public void IsNewer_UnknownSideIsNeverNewer()
        {
            Assert.False(UpdateVersion.IsNewer(null, new Version(9, 9)));
            Assert.False(UpdateVersion.IsNewer(new Version(1, 0), null));
            Assert.False(UpdateVersion.IsNewer(null, null));
        }
    }

    public class UpdateAssetsTests
    {
        [Fact]
        public void PickZip_FindsTheWinX64Archive()
        {
            var names = new[] { "SHA256SUMS.txt", "SystemBarIndicator-win-x64.zip", "notes.md" };
            Assert.Equal("SystemBarIndicator-win-x64.zip", UpdateAssets.PickZip(names));
        }

        [Fact]
        public void PickZip_IsCaseInsensitive()
        {
            Assert.Equal("APP-WIN-X64.ZIP", UpdateAssets.PickZip(new[] { "APP-WIN-X64.ZIP" }));
        }

        [Theory]
        [InlineData("SystemBarIndicator-arm64.zip")] // wrong architecture
        [InlineData("SystemBarIndicator-win-x64.7z")] // wrong format
        [InlineData("win-x64.zip.txt")]
        public void PickZip_IgnoresOtherAssets(string name)
        {
            Assert.Null(UpdateAssets.PickZip(new string?[] { name, null }));
        }

        [Theory]
        [InlineData("SHA256SUMS.txt", "SHA256SUMS.txt")]
        [InlineData("sha256sums.txt", "sha256sums.txt")]
        public void PickChecksums_FindsTheChecksumList(string name, string expected)
        {
            Assert.Equal(expected, UpdateAssets.PickChecksums(new string?[] { "a.zip", name }));
        }

        [Fact]
        public void PickChecksums_NullWhenAbsent()
        {
            Assert.Null(UpdateAssets.PickChecksums(new string?[] { "a.zip", "checksums.md5", null }));
        }
    }

    public class ReleaseChecksumsTests
    {
        private const string Hash = "a3f5698a3778cf136dbde1a2e94a794b433c0f0a8ab4f52c60ff65e5f0d78956";

        [Theory]
        [InlineData(Hash + " *SystemBarIndicator-win-x64.zip")] // sha256sum binary mode
        [InlineData(Hash + "  SystemBarIndicator-win-x64.zip")] // sha256sum text mode
        [InlineData(Hash + " SystemBarIndicator-win-x64.zip")]
        public void FindHash_ReadsTheShaSumFormats(string line)
        {
            Assert.Equal(Hash, ReleaseChecksums.FindHash(line, "SystemBarIndicator-win-x64.zip"));
        }

        [Fact]
        public void FindHash_PicksTheRightLineAndSurvivesCrLf()
        {
            string text = "0000000000000000000000000000000000000000000000000000000000000000 *other.zip\r\n" +
                          Hash.ToUpperInvariant() + " *SystemBarIndicator-win-x64.zip\r\n";
            Assert.Equal(Hash, ReleaseChecksums.FindHash(text, "SystemBarIndicator-win-x64.zip"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not a checksum file")]
        [InlineData("deadbeef *SystemBarIndicator-win-x64.zip")] // hash too short
        [InlineData(Hash + " *another-file.zip")]
        public void FindHash_NullForMissingOrMalformedEntries(string? text)
        {
            Assert.Null(ReleaseChecksums.FindHash(text, "SystemBarIndicator-win-x64.zip"));
        }
    }
}
