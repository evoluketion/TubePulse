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
        private static readonly string BaseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TubePulse");
        private static readonly bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        private static bool useYtDlpNightlies = false;

        public static async Task<string> EnsureYtDlpAsync()
        {
            string filename = useYtDlpNightlies
                ? (IsWindows ? "yt-dlp-nightly.exe" : "yt-dlp-nightly")
                : (IsWindows ? "yt-dlp.exe" : "yt-dlp");
            string ytDlpPath = Path.Combine(BaseDir, filename);

            if (!File.Exists(ytDlpPath))
            {
                Console.WriteLine($"\nyt-dlp not found. Downloading {(useYtDlpNightlies ? "nightly" : "stable")} build...");
                Directory.CreateDirectory(BaseDir);

                string baseUrl = useYtDlpNightlies
                    ? "https://github.com/yt-dlp/yt-dlp-nightly-builds/releases/latest/download"
                    : "https://github.com/yt-dlp/yt-dlp/releases/latest/download";
                
                string url = IsWindows
                    ? $"{baseUrl}/yt-dlp.exe"
                    : $"{baseUrl}/yt-dlp";

                var bytes = await HttpClient.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(ytDlpPath, bytes);

                if (!IsWindows)
                    Process.Start("chmod", $"+x \"{ytDlpPath}\"")?.WaitForExit();

                Console.WriteLine($"yt-dlp downloaded to: {ytDlpPath}");
            }

            return ytDlpPath;
        }

        public static async Task<string> UpdateYtDlpAsync()
        {
            var ytDlpPath = await EnsureYtDlpAsync();
            Console.WriteLine($"\nChecking for updates for yt-dlp ({(useYtDlpNightlies ? "nightly" : "stable")})...");

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

            return ytDlpPath;
        }

        public static bool IsFFmpegAvailable()
        {
            try
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = "-version",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                if (process != null)
                {
                    process.WaitForExit();

                    Console.WriteLine("FFmpeg is available.");

                    return process.ExitCode == 0;
                }
            }
            catch
            {
                Console.WriteLine("FFmpeg is not available.");
                Console.WriteLine("Please ensure FFmpeg is installed and added to your system's PATH.");
                Console.WriteLine("For more information, visit: https://ffmpeg.org/download.html");
            }

            return false;
        }

        public static async Task<string> VerifyDependencies(bool useNightlies = false)
        {
            useYtDlpNightlies = useNightlies;
            var ytDlpPath = await UpdateYtDlpAsync();

            if (!IsFFmpegAvailable())
            {
                throw new InvalidOperationException("FFmpeg is required but not found.");
            }

            return ytDlpPath;
        }
    }
}
