using System;
using System.ComponentModel;

namespace SystemBarIndicator.Sensors
{
    /// <summary>Shared handling of the ways Windows performance counters go missing.</summary>
    internal static class PerfCounters
    {
        /// <summary>
        /// True for the failures that mean "these counters are not usable on this machine"
        /// rather than "the app is broken": the counter service disabled globally
        /// (Win32 1058), a damaged or missing category, or no permission to read it.
        /// Anything else is left to propagate.
        /// </summary>
        public static bool IsExpectedFailure(Exception ex) =>
            ex is Win32Exception ||
            ex is InvalidOperationException ||
            ex is UnauthorizedAccessException ||
            ex is PlatformNotSupportedException;

        public static string Describe(Exception ex) => $"{ex.GetType().Name}: {ex.Message}";
    }
}
