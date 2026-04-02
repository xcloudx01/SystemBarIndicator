using System;
using System.Runtime.InteropServices;

namespace SystemBarIndicator.Sensors
{
    public class RamSensor
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        public (float usagePercent, long usedBytes, long totalBytes) GetInfo()
        {
            var memStatus = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX)) };
            if (GlobalMemoryStatusEx(ref memStatus))
            {
                long total = (long)memStatus.ullTotalPhys;
                long used = total - (long)memStatus.ullAvailPhys;
                float percent = total > 0 ? (float)used / total * 100f : 0f;
                return (percent, used, total);
            }
            return (0, 0, 0);
        }
    }
}
