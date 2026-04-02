using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;

namespace SystemBarIndicator.Sensors
{
    public class NetworkSensor
    {
        private readonly Dictionary<string, PerformanceCounter> _downloadCounters = new();
        private readonly Dictionary<string, PerformanceCounter> _uploadCounters = new();
        private readonly Dictionary<string, long> _prevDownloadBytes = new();
        private readonly Dictionary<string, long> _prevUploadBytes = new();
        private readonly Dictionary<string, DateTime> _lastReadTime = new();

        public NetworkSensor()
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                             ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                             ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                .ToList();

            foreach (var ni in interfaces)
            {
                try
                {
                    var ifName = ni.Name;
                    _downloadCounters[ifName] = new PerformanceCounter("Network Interface", "Bytes Received/sec", ifName);
                    _uploadCounters[ifName] = new PerformanceCounter("Network Interface", "Bytes Sent/sec", ifName);
                    _downloadCounters[ifName].NextValue();
                    _uploadCounters[ifName].NextValue();
                }
                catch
                {
                    // Skip interfaces that don't have performance counters
                }
            }
        }

        public (float downloadKBps, float uploadKBps) GetInfo()
        {
            float totalDownload = 0, totalUpload = 0;
            foreach (var key in _downloadCounters.Keys)
            {
                try
                {
                    totalDownload += _downloadCounters[key].NextValue();
                    totalUpload += _uploadCounters[key].NextValue();
                }
                catch
                {
                    // Skip failed counters
                }
            }
            return (totalDownload / 1024f, totalUpload / 1024f);
        }
    }
}
