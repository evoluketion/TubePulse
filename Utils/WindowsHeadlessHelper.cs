using System.Runtime.Versioning;

namespace TubePulse
{
    [SupportedOSPlatform("windows")]
    public static class WindowsHeadlessHelper
    {
        public static Utils.TrayIconManager Start()
        {
            return new Utils.TrayIconManager();
        }
    }
}
