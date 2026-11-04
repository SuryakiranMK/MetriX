using System;

namespace MetriX.Model
{
    public class UpdateInfo
    {
        public bool IsUpdateAvailable { get; set; }
        public string VersionTag { get; set; }
        public Version NewVersion { get; set; }
        public Version CurrentVersion { get; set; }
        public string ReleaseTitle { get; set; }
        public string Changelog { get; set; }
        public string ExeDownloadUrl { get; set; }
        public string ZipDownloadUrl { get; set; }
        public long AssetSizeBytes { get; set; }
        public DateTime PublishedAt { get; set; }
        public string ErrorMessage { get; set; }

        public UpdateInfo()
        {
            VersionTag = "";
            ReleaseTitle = "";
            Changelog = "";
            ExeDownloadUrl = "";
            ZipDownloadUrl = "";
            ErrorMessage = "";
        }
    }
}
