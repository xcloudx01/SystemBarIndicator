using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;

namespace SystemBarIndicator.Sensors
{
    /// <summary>
    /// Измеряет скорость сети по дельте счётчиков байт интерфейсов.
    /// Используется <see cref="IPInterfaceStatistics"/> вместо PerformanceCounter:
    /// имена экземпляров perf-счётчиков "Network Interface" формируются из описания
    /// адаптера и не совпадают с <c>NetworkInterface.Name</c>, из-за чего сопоставление
    /// по имени часто не находит счётчик.
    /// </summary>
    public class NetworkSensor
    {
        private readonly Dictionary<string, long> _prevReceived = new();
        private readonly Dictionary<string, long> _prevSent = new();
        private DateTime _lastCheck;

        public NetworkSensor()
        {
            _lastCheck = DateTime.Now;
            foreach (var ni in GetActiveInterfaces())
            {
                try
                {
                    var stats = ni.GetIPv4Statistics();
                    _prevReceived[ni.Id] = stats.BytesReceived;
                    _prevSent[ni.Id] = stats.BytesSent;
                }
                catch
                {
                    // интерфейс без доступной IPv4-статистики — пропускаем
                }
            }
        }

        public (float downloadKBps, float uploadKBps) GetInfo()
        {
            var now = DateTime.Now;
            double elapsed = (now - _lastCheck).TotalSeconds;
            long totalDown = 0, totalUp = 0;

            foreach (var ni in GetActiveInterfaces())
            {
                try
                {
                    var stats = ni.GetIPv4Statistics();
                    string id = ni.Id;

                    if (_prevReceived.TryGetValue(id, out long prevR))
                    {
                        long diff = stats.BytesReceived - prevR;
                        if (diff > 0) totalDown += diff;
                    }
                    if (_prevSent.TryGetValue(id, out long prevS))
                    {
                        long diff = stats.BytesSent - prevS;
                        if (diff > 0) totalUp += diff;
                    }

                    _prevReceived[id] = stats.BytesReceived;
                    _prevSent[id] = stats.BytesSent;
                }
                catch
                {
                    // пропускаем сбойный интерфейс
                }
            }

            _lastCheck = now;
            if (elapsed <= 0) return (0, 0);

            return ((float)(totalDown / 1024.0 / elapsed), (float)(totalUp / 1024.0 / elapsed));
        }

        private static IEnumerable<NetworkInterface> GetActiveInterfaces()
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                             ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                             ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel);
        }
    }
}
