using System.Collections.Generic;

namespace TubePulse
{
    public class TubePulseSettings
    {
        public List<Channel> Channels { get; set; } = new List<Channel>();
        public string DownloadPath { get; set; } = string.Empty;
        public string CachePath { get; set; } = string.Empty;
        public int PollingTimeoutHours { get; set; } = 1;
        public string DownloadResolution {get; set; } = string.Empty;
    }

    public class Channel
    {
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string DownloadResolution {get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
    }
}
