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
        private const string CacheFile = "videoCache.json";
        private HashSet<string> processedVideoIds;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Load or initialize cache
            processedVideoIds = LoadCache();
            bool isFirstRun = processedVideoIds.Count == 0;

            if (isFirstRun)
            {
                Console.WriteLine("First run: Fetching all existing videos and caching them...");
                var urls = File.ReadAllLines("channelUrls.txt");
                await FetchAndCacheAllVideos(urls.ToList());
            }
            else
            {
                Console.WriteLine($"Loaded {processedVideoIds.Count} cached video IDs.");
            }

            // Start monitoring for new videos
            while (!stoppingToken.IsCancellationRequested)
            {
                var urls = File.ReadAllLines("channelUrls.txt");
                await CheckAndDownloadNewVideos(urls.ToList());
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }

        private HashSet<string> LoadCache()
        {
            if (File.Exists(CacheFile))
            {
                try
                {
                    string json = File.ReadAllText(CacheFile);
                    var cachedIds = JsonSerializer.Deserialize<List<string>>(json);
                    return new HashSet<string>(cachedIds ?? new List<string>());
                }
                catch
                {
                    Console.WriteLine("Error loading cache, starting fresh.");
                }
            }
            return new HashSet<string>();
        }

        private void SaveCache()
        {
            try
            {
                string json = JsonSerializer.Serialize(processedVideoIds.ToList());
                File.WriteAllText(CacheFile, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving cache: {ex.Message}");
            }
        }

        private async Task FetchAndCacheAllVideos(List<string> urls)
        {
            foreach (var url in urls)
            {
                Console.WriteLine($"Fetching all videos for: {url}");
                var videos = await GetVideos(url, null); // null means fetch all
                foreach (var video in videos)
                {
                    processedVideoIds.Add(video.Id);
                }
            }
            SaveCache();
            Console.WriteLine($"Cached {processedVideoIds.Count} video IDs.");
        }

        private async Task CheckAndDownloadNewVideos(List<string> urls)
        {
            foreach (var url in urls)
            {
                Console.WriteLine($"Checking for new videos in last 24 hours for: {url}");
                var recentVideos = await GetVideos(url, "today-1day");
                var newVideos = recentVideos.Where(v => !processedVideoIds.Contains(v.Id)).ToList();

                if (newVideos.Any())
                {
                    Console.WriteLine($"Found {newVideos.Count} new videos to download.");
                    foreach (var video in newVideos)
                    {
                        await DownloadVideo(video.Url);
                        processedVideoIds.Add(video.Id);
                    }
                    SaveCache();
                }
                else
                {
                    Console.WriteLine("No new videos found.");
                }
            }
        }

        private async Task<List<YoutubeVideo>> GetVideos(string url, string dateAfter)
        {
            var argumentList = new List<string>
            {
                "-j",
                "--flat-playlist",
                "--match-filter",
                "\"original_url!*=/shorts/ & url!*=/shorts/\"",
                $"\"{url}\""
            };

            if (!string.IsNullOrEmpty(dateAfter))
            {
                argumentList.Insert(1, $"--dateafter \"{dateAfter}\"");
            }

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

                var serializer = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var videos = new List<YoutubeVideo>();
                foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    try
                    {
                        var video = JsonSerializer.Deserialize<YoutubeVideo>(line, serializer);
                        if (video != null) videos.Add(video);
                    }
                    catch
                    {
                        // Skip invalid JSON lines
                    }
                }

                Console.WriteLine($"Fetched {videos.Count} videos.");
                if (!string.IsNullOrEmpty(error))
                {
                    Console.WriteLine($"yt-dlp stderr: {error}");
                }

                return videos;
            }
        }

        private async Task DownloadVideo(string url)
        {
            Console.WriteLine($"Downloading video: {url}");

            var argumentList = new List<string>
            {
                "--match-filter",
                "\"original_url!*=/shorts/ & url!*=/shorts/\"",
                $"\"{url}\""
            };

            var startInfo = new ProcessStartInfo
            {
                FileName = "yt-dlp",
                Arguments = string.Join(" ", argumentList),
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = new Process())
            {
                process.StartInfo = startInfo;
                process.Start();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    Console.WriteLine("Download completed successfully.");
                }
                else
                {
                    Console.WriteLine($"Download failed with exit code: {process.ExitCode}");
                }
            }
        }
    }
}
