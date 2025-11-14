using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TubePulse.models;

namespace TubePulse
{
    public class Worker : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var urls = File.ReadAllLines("channelUrls.txt");
                await getVideos(urls.ToList());
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }

        private static async Task getVideos(List<string> urls)
        {
            var serializer = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            foreach (var url in urls)
            {
                Console.WriteLine($"Processing URL: {url}");
                var argumentList = new List<string>
                {
                    "-j",
                    "--flat-playlist",
                    "--match-filter",
                    "\"original_url!*=/shorts/ & url!*=/shorts/\"",
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

                    var videos = new List<TubePulse.models.YoutubeVideo>();
                    foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var video = JsonSerializer.Deserialize<TubePulse.models.YoutubeVideo>(line, serializer);
                        if (video != null) videos.Add(video);
                    }

                    Console.WriteLine($"Exit code: {process.ExitCode}");
                    Console.WriteLine($"Parsed {videos.Count} videos:");
                    Console.WriteLine("STDERR:");
                    Console.WriteLine("----------------");
                    Console.WriteLine(error);
                }
            }
        }
    }
}
