using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using TubePulse.models;

namespace TubePulse
{
    class Program
    {
        static async Task Main()
        {
            const string url = "https://www.youtube.com/@NoCopyrightSounds"; // Example URL

            await getVideos(url);
        }
        
        private static async Task getVideos(string url)
        {
            var serializer = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var argumentList = new List<string>
            {
                "-j",
                "--flat-playlist",
                "--match-filter \"original_url!*=/shorts/ & url!*=/shorts/\"",
                $"\"{url}\""
            };
            var startInfo = new ProcessStartInfo
            {
                FileName = "yt-dlp",
                Arguments = string.Join(" ", argumentList),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = new Process())
            {
                process.StartInfo = startInfo;
                process.Start();

                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                var videos = new List<YoutubeVideo>();
                foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    var video = JsonSerializer.Deserialize<YoutubeVideo>(line, serializer);
                    if (video != null) videos.Add(video);
                }

                Console.WriteLine($"Exit code: {process.ExitCode}");
                Console.WriteLine($"Parsed {videos.Count} videos:");

                // foreach (var video in videos)
                // {
                //     Console.WriteLine($"- {video.Title} ({video.Url})");
                // }
                
                Console.WriteLine("STDERR:");
                Console.WriteLine(error);
            }
        }
    }
}