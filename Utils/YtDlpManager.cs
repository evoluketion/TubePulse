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

        public static async Task<string> EnsureYtDlpAsync()
        {
            bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            string baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TubePulse");
            string ytDlpPath = Path.Combine(baseDir, isWindows ? "yt-dlp.exe" : "yt-dlp");

            if (!File.Exists(ytDlpPath))
            {
                Console.WriteLine("yt-dlp not found. Downloading...");
                Directory.CreateDirectory(baseDir);
                
                string url = isWindows
                    ? "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe"
                    : "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp";

                var bytes = await HttpClient.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(ytDlpPath, bytes);

                if (!isWindows)
                    Process.Start("chmod", $"+x \"{ytDlpPath}\"")?.WaitForExit();

                Console.WriteLine($"yt-dlp downloaded to: {ytDlpPath}");
            }

            return ytDlpPath;
        }

        public static async Task UpdateYtDlpAsync()
        {
            var ytDlpPath = await EnsureYtDlpAsync();
            Console.WriteLine("Updating yt-dlp...");

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
