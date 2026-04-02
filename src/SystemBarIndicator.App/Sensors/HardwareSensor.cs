using LibreHardwareMonitor.Hardware;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SystemBarIndicator.Sensors
{
    public class HardwareSensor : IDisposable
    {
        private readonly Computer _computer;

        public HardwareSensor()
        {
            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = false,
                IsMotherboardEnabled = false,
                IsControllerEnabled = false,
                IsNetworkEnabled = false,
                IsStorageEnabled = false
            };
            _computer.Open();
        }

        public float? GetCpuTemperature()
        {
            return GetFirstValue(_computer.Hardware
                .Where(h => h.HardwareType == HardwareType.Cpu)
                .SelectMany(h => h.Sensors)
                .Where(s => s.SensorType == SensorType.Temperature &&
                            (s.Name.Contains("Core") || s.Name.Contains("Package") || s.Name.Contains("CPU"))));
        }

        public float? GetGpuUsage()
        {
            return GetFirstValue(_computer.Hardware
                .Where(h => h.HardwareType == HardwareType.GpuNvidia || h.HardwareType == HardwareType.GpuAmd || h.HardwareType == HardwareType.GpuIntel)
                .SelectMany(h => h.Sensors)
                .Where(s => s.SensorType == SensorType.Load && s.Name.Contains("GPU")));
        }

        public float? GetGpuTemperature()
        {
            return GetFirstValue(_computer.Hardware
                .Where(h => h.HardwareType == HardwareType.GpuNvidia || h.HardwareType == HardwareType.GpuAmd || h.HardwareType == HardwareType.GpuIntel)
                .SelectMany(h => h.Sensors)
                .Where(s => s.SensorType == SensorType.Temperature));
        }

        public (float? used, float? total)? GetGpuMemory()
        {
            var gpuHardware = _computer.Hardware
                .FirstOrDefault(h => h.HardwareType == HardwareType.GpuNvidia || h.HardwareType == HardwareType.GpuAmd || h.HardwareType == HardwareType.GpuIntel);
            
            if (gpuHardware == null) return null;

            float? used = null;
            float? total = null;

            foreach (var sensor in gpuHardware.Sensors)
            {
                if (sensor.SensorType == SensorType.SmallData && sensor.Value.HasValue)
                {
                    if (sensor.Name.Contains("GPU Memory Used") || sensor.Name.Contains("D3D"))
                        used = sensor.Value;
                    else if (sensor.Name.Contains("GPU Memory Total"))
                        total = sensor.Value;
                }
            }

            if (used.HasValue || total.HasValue)
                return (used, total);
            return null;
        }

        private static float? GetFirstValue(IEnumerable<ISensor> sensors)
        {
            foreach (var sensor in sensors)
            {
                if (sensor.Value.HasValue)
                    return sensor.Value;
            }
            return null;
        }

        public void Dispose()
        {
            _computer.Close();
        }
    }
}
