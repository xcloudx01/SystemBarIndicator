using System;
using System.Diagnostics;

namespace SystemBarIndicator.Sensors
{
    /// <summary>
    /// CPU load, preferring the performance counter and falling back to GetSystemTimes when
    /// the counter registry is unusable (it can be disabled machine-wide, or simply broken).
    /// The fallback is one-way: once the counter has let us down there is no point asking it
    /// again every second.
    /// </summary>
    public class CpuSensor : IDisposable
    {
        private PerformanceCounter? _counter;
        private SystemTimesCpuSource? _fallback;
        private bool _fallbackBroken;

        /// <summary>True once the performance counter has been given up on.</summary>
        public bool UsingFallback => _counter == null;

        public CpuSensor()
        {
            try
            {
                var counter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                try
                {
                    counter.NextValue(); // first reading primes the counter and is always 0
                }
                catch
                {
                    counter.Dispose(); // priming can throw after construction succeeded
                    throw;
                }
                _counter = counter;
            }
            catch (Exception ex) when (PerfCounters.IsExpectedFailure(ex))
            {
                Logger.Warn($"CPU performance counter unavailable ({PerfCounters.Describe(ex)}); " +
                            "measuring CPU load with GetSystemTimes instead");
                _fallback = new SystemTimesCpuSource();
            }
        }

        public float GetUsage()
        {
            if (_counter != null)
            {
                try
                {
                    return _counter.NextValue();
                }
                catch (Exception ex) when (PerfCounters.IsExpectedFailure(ex))
                {
                    // Counters can also break *after* a successful start. Letting this
                    // propagate would abort the whole polling tick — RAM, disk, network,
                    // history and the repaint with it — once per second, forever.
                    Logger.Warn($"CPU performance counter failed while running ({PerfCounters.Describe(ex)}); " +
                                "switching to GetSystemTimes");
                    _counter.Dispose();
                    _counter = null;
                    _fallback = new SystemTimesCpuSource();
                }
            }

            if (_fallback == null || _fallbackBroken)
                return 0f;

            try
            {
                return _fallback.GetUsage();
            }
            catch (Exception ex)
            {
                // Nothing left to fall back to; report once and stay quiet.
                Logger.Error("SystemTimesCpuSource", ex);
                _fallbackBroken = true;
                return 0f;
            }
        }

        public void Dispose()
        {
            _counter?.Dispose();
            _counter = null;
        }
    }
}
