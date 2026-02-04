using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace TubePulse.Utils
{
    public static class YtDlpManager
    {
        private static readonly HttpClient HttpClient = new();

        public static async Task<string> EnsureYtDlpAsync(bool useNightlies = false)
        {
            bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            string baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TubePulse");
            string filename = useNightlies
                ? (isWindows ? "yt-dlp-nightly.exe" : "yt-dlp-nightly")
                : (isWindows ? "yt-dlp.exe" : "yt-dlp");
            string ytDlpPath = Path.Combine(baseDir, filename);

            if (!File.Exists(ytDlpPath))
            {
                Console.WriteLine($"\nyt-dlp not found. Downloading {(useNightlies ? "nightly" : "stable")} build...");
                Directory.CreateDirectory(baseDir);
                
                string baseUrl = useNightlies
                    ? "https://github.com/yt-dlp/yt-dlp-nightly-builds/releases/latest/download"
                    : "https://github.com/yt-dlp/yt-dlp/releases/latest/download";
                
                string url = isWindows
                    ? $"{baseUrl}/yt-dlp.exe"
                    : $"{baseUrl}/yt-dlp";

                var bytes = await HttpClient.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(ytDlpPath, bytes);

                if (!isWindows)
                    Process.Start("chmod", $"+x \"{ytDlpPath}\"")?.WaitForExit();

                Console.WriteLine($"yt-dlp downloaded to: {ytDlpPath}");
            }

            return ytDlpPath;
        }

        public static async Task UpdateYtDlpAsync(bool useNightlies = false)
        {
            var ytDlpPath = await EnsureYtDlpAsync(useNightlies);
            Console.WriteLine($"\nChecking for updates for yt-dlp ({(useNightlies ? "nightly" : "stable")})...");

            var process = Process.Start(new ProcessStartInfo
            {
                FileName = ytDlpPath,
                Arguments = "-U",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process != null)
            {
                Console.WriteLine(await process.StandardOutput.ReadToEndAsync());
                await process.WaitForExitAsync();
            }
        }
    }
}
