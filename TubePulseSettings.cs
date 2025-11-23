using System.Collections.Generic;

namespace TubePulse
{
    public class TubePulseSettings
    {
        public List<string> ChannelUrls { get; set; } = new List<string>();
        public string DownloadPath { get; set; } = string.Empty;
    }
}
