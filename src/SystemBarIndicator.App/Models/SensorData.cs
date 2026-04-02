namespace SystemBarIndicator.Models
{
    public class SensorData
    {
        public float CpuUsage { get; set; }
        public float CpuTemperature { get; set; }
        public float GpuUsage { get; set; }
        public float GpuTemperature { get; set; }
        public float GpuMemoryUsed { get; set; }
        public float GpuMemoryTotal { get; set; }
        public float RamUsagePercent { get; set; }
        public long RamUsed { get; set; }
        public long RamTotal { get; set; }
        public float DiskReadSpeed { get; set; }
        public float DiskWriteSpeed { get; set; }
        public float DiskUsagePercent { get; set; }
        public float NetworkDownloadSpeed { get; set; }
        public float NetworkUploadSpeed { get; set; }
    }
}
