using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using SystemBarIndicator.Monitoring;

namespace SystemBarIndicator.Sensors
{
    /// <summary>
    /// CPU load straight from the kernel, used when the performance-counter registry is
    /// unusable. Needs no counter service, no registry entries and no elevation.
    ///
    /// Caveat: on machines with more than 64 logical processors GetSystemTimes reports only
    /// the calling thread's processor group, so the reading covers that group rather than
    /// the whole machine.
    /// </summary>
    internal sealed class SystemTimesCpuSource
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetSystemTimes(out ulong idleTime, out ulong kernelTime, out ulong userTime);

        private ulong _previousIdle;
        private ulong _previousTotal;
        private bool _primed;

        /// <summary>Load since the previous call; the first call primes the baseline and returns 0.</summary>
        public float GetUsage()
        {
            if (!GetSystemTimes(out ulong idle, out ulong kernel, out ulong user))
                throw new Win32Exception(Marshal.GetLastWin32Error());

            // Kernel time already includes idle time, so the two of them plus user time are
            // the whole budget and idle is the part of it that was spent doing nothing.
            ulong total = kernel + user;

            if (!_primed)
            {
                _previousIdle = idle;
                _previousTotal = total;
                _primed = true;
                return 0f;
            }

            float usage = CpuLoad.FromSamples(_previousIdle, _previousTotal, idle, total);
            _previousIdle = idle;
            _previousTotal = total;
            return usage;
        }
    }
}
