using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
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
        private const string CacheFileExtension = ".json";
        private HashSet<string> processedVideoIds;
        private readonly TubePulseSettings settings;

        public Worker(IOptions<TubePulseSettings> options)
        {
            settings = options.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(5000); // Starting delay to offset application starting debug logs
            var channels = settings.Channels;

            Console.WriteLine("\nBeginning Processing.");
            Console.WriteLine("---------------------------------------------------------\n");
            while (!stoppingToken.IsCancellationRequested)
            {
                foreach (Channel channel in channels)
                {
                    var channelName = channel.Name;
                    var channelUrl = channel.Url;

                    processedVideoIds = LoadCache(channelName);
                    bool isFirstRun = processedVideoIds.Count == 0;

                    if (isFirstRun)
                    {
                        Console.WriteLine($"First run for channel {channelName}: Fetching all existing videos and caching them...");
                        await FetchAndCacheAllVideos(channelUrl, channelName);
                    }
                    else
                    {
                        Console.WriteLine($"Loaded {processedVideoIds.Count} cached video IDs for channel {channelName}.");
                    }

                    await CheckAndDownloadNewVideos(channel.Url, channel.Name);
                }

                await Task.Delay(TimeSpan.FromHours(settings.pollingTimeout), stoppingToken);
            }
        }

        private string GetCacheFilePath(string channelName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            return $"{settings.CachePath}/videoCache_{channelName}{CacheFileExtension}";
        }

        private HashSet<string> LoadCache(string channelName)
        {
            var cacheFile = GetCacheFilePath(channelName);
            if (File.Exists(cacheFile))
            {
                try
                {
                    string json = File.ReadAllText(cacheFile);
                    var cachedIds = JsonSerializer.Deserialize<List<string>>(json);
                    return new HashSet<string>(cachedIds ?? new List<string>());
                }
                catch
                {
                    Console.WriteLine("Error loading cache for channel, starting fresh.");
                }
            }
            return new HashSet<string>();
        }

        private void SaveCache(string channelName)
        {
            var cacheFile = GetCacheFilePath(channelName);
            try
            {
                string json = JsonSerializer.Serialize(processedVideoIds.ToList());
                File.WriteAllText(cacheFile, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving cache for channel: {ex.Message}");
            }
        }

        private async Task FetchAndCacheAllVideos(string channelUrl, string channelName)
        {
            Console.WriteLine($"Fetching all videos for: {channelUrl}");
            var videos = await GetVideos(channelUrl, null);
            foreach (var video in videos)
            {
                processedVideoIds.Add(video.Id);
            }
            SaveCache(channelName);
            Console.WriteLine($"Cached {processedVideoIds.Count} video IDs for channel.");
        }

        private async Task CheckAndDownloadNewVideos(string channelUrl, string channelName)
        {
            Console.WriteLine($"Checking for new videos in last 24 hours for: {channelUrl}");
            var recentVideos = await GetVideos(channelUrl, "today-1day");
            var newVideos = recentVideos.Where(v => !processedVideoIds.Contains(v.Id)).ToList();

            if (newVideos.Any())
            {
                Console.WriteLine($"Found {newVideos.Count} new videos to download.");
                foreach (var video in newVideos)
                {
                    await DownloadVideo(video.Url, channelName);
                    processedVideoIds.Add(video.Id);
                }
                SaveCache(channelName);
            }
            else
            {
                Console.WriteLine("No new videos found.");
                Console.WriteLine("---------------------------------------------------------\n");
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

        private async Task DownloadVideo(string url, string channelName)
        {
            Console.WriteLine($"Downloading video: {url}");

            var argumentList = new List<string>
            {
                $"\"{url}\""
            };

            Console.WriteLine($"Executing: yt-dlp {string.Join(" ", argumentList)}");

            var downloadPath = string.IsNullOrWhiteSpace(settings.DownloadPath)
                    ? Environment.CurrentDirectory
                    : settings.DownloadPath;
            downloadPath += $"/{channelName}";
            Directory.CreateDirectory(downloadPath);

            var startInfo = new ProcessStartInfo
            {
                FileName = "yt-dlp",
                Arguments = string.Join(" ", argumentList),
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = downloadPath
            };
            
            Console.WriteLine($"Downloading to: {downloadPath}");
            using (var process = new Process())
            {
                process.StartInfo = startInfo;
                process.Start();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    Console.WriteLine("Download completed successfully.");
                    Console.WriteLine("---------------------------------------------------------");
                }
                else
                {
                    Console.WriteLine($"Download failed with exit code: {process.ExitCode}");
                }
            }
        }
    }
}
