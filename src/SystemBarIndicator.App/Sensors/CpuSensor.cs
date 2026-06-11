using System;
using System.Diagnostics;

namespace SystemBarIndicator.Sensors
{
    public class CpuSensor : IDisposable
    {
        private readonly PerformanceCounter _cpuCounter;

        public CpuSensor()
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _cpuCounter.NextValue();
        }

        public float GetUsage()
        {
            return _cpuCounter.NextValue();
        }

        public void Dispose()
        {
            _cpuCounter.Dispose();
        }
    }
}
