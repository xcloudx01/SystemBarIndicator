using System;

namespace SystemBarIndicator.Updates
{
    /// <summary>Release tag parsing and version comparison rules for the auto-updater.</summary>
    public static class UpdateVersion
    {
        /// <summary>
        /// Parses a release tag ("v1.2.3" or "1.2.3") into a version. Returns null for
        /// anything else — including pre-release tags like "v1.2.3-beta" — so a mislabeled
        /// release can never trigger an update.
        /// </summary>
        public static Version? ParseTag(string? tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return null;

            string text = tag.Trim();
            if (text.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                text = text.Substring(1);

            return Version.TryParse(text, out var version) ? Normalize(version) : null;
        }

        /// <summary>
        /// True when <paramref name="latest"/> is strictly newer than
        /// <paramref name="current"/>. Null on either side means "unknown" and never counts
        /// as newer: a failed parse must not start an update loop.
        /// </summary>
        public static bool IsNewer(Version? current, Version? latest)
        {
            if (current == null || latest == null)
                return false;
            return Normalize(latest) > Normalize(current);
        }

        /// <summary>
        /// Pads missing components with zeros: the assembly reports "1.0.0.0" while tags
        /// carry "1.0.0", and <see cref="Version"/> treats a missing component as -1.
        /// </summary>
        public static Version Normalize(Version version) =>
            new Version(version.Major, version.Minor, Math.Max(version.Build, 0), Math.Max(version.Revision, 0));
    }
}
