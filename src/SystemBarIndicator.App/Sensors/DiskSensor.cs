using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SystemBarIndicator.Sensors
{
    public class DiskSensor
    {
        private readonly List<PerformanceCounter> _readCounters = new();
        private readonly List<PerformanceCounter> _writeCounters = new();
        private readonly List<PerformanceCounter> _usageCounters = new();

        public DiskSensor()
        {
            var category = new PerformanceCounterCategory("PhysicalDisk");
            var instances = category.GetInstanceNames()
                .Where(name => !name.Equals("_total", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var instance in instances)
            {
                _readCounters.Add(new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", instance));
                _writeCounters.Add(new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", instance));
                _usageCounters.Add(new PerformanceCounter("PhysicalDisk", "% Disk Time", instance));
            }

            foreach (var c in _readCounters.Concat(_writeCounters).Concat(_usageCounters))
                c.NextValue();
        }

        public (float readMBps, float writeMBps, float usagePercent) GetInfo()
        {
            float totalRead = 0, totalWrite = 0, totalUsage = 0;
            int count = _readCounters.Count;
            if (count == 0) return (0, 0, 0);

            for (int i = 0; i < count; i++)
            {
                totalRead += _readCounters[i].NextValue();
                totalWrite += _writeCounters[i].NextValue();
                totalUsage += _usageCounters[i].NextValue();
            }

            return (totalRead / 1048576f, totalWrite / 1048576f, totalUsage / count);
        }
    }
}
