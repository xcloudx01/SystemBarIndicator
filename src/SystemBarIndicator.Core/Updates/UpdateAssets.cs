using System;
using System.Collections.Generic;

namespace SystemBarIndicator.Updates
{
    /// <summary>Picks the updater's files out of a release's asset list.</summary>
    public static class UpdateAssets
    {
        /// <summary>
        /// The build archive: a .zip with "win-x64" in the name. The release procedure keeps
        /// that name constant across versions (see deploy/README.md).
        /// </summary>
        public static string? PickZip(IEnumerable<string?> names)
        {
            foreach (var name in names)
            {
                if (name != null &&
                    name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) &&
                    name.Contains("win-x64", StringComparison.OrdinalIgnoreCase))
                {
                    return name;
                }
            }
            return null;
        }

        /// <summary>The checksum list published next to the archive, if the release has one.</summary>
        public static string? PickChecksums(IEnumerable<string?> names)
        {
            foreach (var name in names)
            {
                if (string.Equals(name, "SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase))
                    return name;
            }
            return null;
        }
    }
}
