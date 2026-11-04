using System;

namespace MetriX.Model
{
    public class NetworkStats
    {
        public double DownloadSpeedBytesPerSec { get; set; }
        public double UploadSpeedBytesPerSec { get; set; }

        public string DownloadFormatted { get; set; }
        public string UploadFormatted { get; set; }

        public string DownloadUnit { get; set; }
        public string UploadUnit { get; set; }

        public ulong TotalInBytes { get; set; }
        public ulong TotalOutBytes { get; set; }

        public NetworkStats()
        {
            DownloadFormatted = "0.0";
            UploadFormatted = "0.0";
            DownloadUnit = "KB/s";
            UploadUnit = "KB/s";
        }
    }

    public class AdapterInfo
    {
        public string Alias { get; set; }
        public string Description { get; set; }
        public uint Type { get; set; }
        public bool IsUp { get; set; }

        public override string ToString()
        {
            return string.IsNullOrEmpty(Alias) ? Description : Alias;
        }
    }
}
