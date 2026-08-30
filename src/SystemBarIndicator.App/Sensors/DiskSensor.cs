using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SystemBarIndicator.Sensors
{
    public class DiskSensor : IDisposable
    {
        private readonly List<PerformanceCounter> _readCounters = new();
        private readonly List<PerformanceCounter> _writeCounters = new();
        private readonly List<PerformanceCounter> _usageCounters = new();

        /// <summary>
        /// False once the counters are known to be unusable — at construction or later.
        /// One-way: the bar hides the disk section on it, and a section that came and went
        /// as samples failed would make the layout jump.
        /// </summary>
        public bool Available { get; private set; }

        public DiskSensor()
        {
            try
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

                foreach (var c in AllCounters())
                    c.NextValue();
            }
            catch
            {
                // Whatever was built before the failure must still be released.
                DisposeCounters();
                throw;
            }

            Available = _readCounters.Count > 0;
        }

        public (float readMBps, float writeMBps, float usagePercent) GetInfo()
        {
            if (!Available)
                return (0, 0, 0);

            int count = _readCounters.Count;

            try
            {
                float totalRead = 0, totalWrite = 0, totalUsage = 0;
                for (int i = 0; i < count; i++)
                {
                    totalRead += _readCounters[i].NextValue();
                    totalWrite += _writeCounters[i].NextValue();
                    totalUsage += _usageCounters[i].NextValue();
                }

                return (totalRead / 1048576f, totalWrite / 1048576f, totalUsage / count);
            }
            catch (Exception ex) when (PerfCounters.IsExpectedFailure(ex))
            {
                // As with the CPU counter: letting this escape would abort the entire
                // polling tick every second instead of losing just the disk section.
                Logger.Warn($"Disk performance counters failed while running ({PerfCounters.Describe(ex)}); " +
                            "disk activity will not be shown");
                Available = false;
                DisposeCounters();
                return (0, 0, 0);
            }
        }

        private IEnumerable<PerformanceCounter> AllCounters() =>
            _readCounters.Concat(_writeCounters).Concat(_usageCounters);

        private void DisposeCounters()
        {
            foreach (var c in AllCounters())
                c.Dispose();

            _readCounters.Clear();
            _writeCounters.Clear();
            _usageCounters.Clear();
        }

        public void Dispose()
        {
            Available = false;
            DisposeCounters();
        }
    }
}
