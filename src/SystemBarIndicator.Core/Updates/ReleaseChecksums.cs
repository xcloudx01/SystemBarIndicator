using System;

namespace SystemBarIndicator.Updates
{
    /// <summary>
    /// Reads the sha256sum file format: one "&lt;64 hex digits&gt; &lt;filename&gt;" pair per
    /// line, with an optional '*' before the name (sha256sum's binary-mode marker).
    /// </summary>
    public static class ReleaseChecksums
    {
        /// <summary>
        /// The lower-case hex hash recorded for <paramref name="fileName"/>, or null when
        /// the file is not listed (or the line is malformed).
        /// </summary>
        public static string? FindHash(string? checksums, string? fileName)
        {
            if (string.IsNullOrWhiteSpace(checksums) || string.IsNullOrWhiteSpace(fileName))
                return null;

            foreach (var rawLine in checksums.Split('\n'))
            {
                string line = rawLine.Trim();
                int space = line.IndexOf(' ');
                if (space <= 0)
                    continue;

                string hash = line.Substring(0, space);
                string name = line.Substring(space + 1).TrimStart(' ', '*');
                if (string.Equals(name, fileName, StringComparison.OrdinalIgnoreCase) && IsSha256Hex(hash))
                    return hash.ToLowerInvariant();
            }
            return null;
        }

        private static bool IsSha256Hex(string text)
        {
            if (text.Length != 64)
                return false;

            foreach (char c in text)
            {
                bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!hex)
                    return false;
            }
            return true;
        }
    }
}
