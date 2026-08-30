using System;
using System.Collections.Generic;

namespace SystemBarIndicator.Models
{
    public class DataPoint
    {
        public DateTime Timestamp { get; set; }
        public float CpuUsage { get; set; }
        public float CpuTemperature { get; set; }
        public float GpuUsage { get; set; }
        public float GpuTemperature { get; set; }
        public float GpuVramTemperature { get; set; }
        public float RamUsage { get; set; }
        public float DiskRead { get; set; }
        public float DiskWrite { get; set; }
        public float NetDown { get; set; }
        public float NetUp { get; set; }
    }

    public class DataHistory
    {
        private readonly Queue<DataPoint> _history;
        private readonly int _maxPoints;

        public DataHistory(int maxPoints = 120)
        {
            _maxPoints = maxPoints;
            _history = new Queue<DataPoint>();
        }

        public void Add(DataPoint point)
        {
            _history.Enqueue(point);
            while (_history.Count > _maxPoints)
                _history.Dequeue();
        }

        public List<DataPoint> GetPoints()
        {
            return new List<DataPoint>(_history);
        }

        public int Count => _history.Count;

        public void Clear()
        {
            _history.Clear();
        }
    }
}
