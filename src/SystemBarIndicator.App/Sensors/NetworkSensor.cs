using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;

namespace SystemBarIndicator.Sensors
{
    /// <summary>
    /// Measures network speed from the delta of each interface's byte counters.
    /// <see cref="IPInterfaceStatistics"/> is used instead of PerformanceCounter: the
    /// instance names of the "Network Interface" performance counters are derived from the
    /// adapter description and do not match <c>NetworkInterface.Name</c>, so matching by
    /// name frequently fails to find the counter.
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
                    // interface with no available IPv4 statistics — skip it
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
                    // skip the failing interface
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
