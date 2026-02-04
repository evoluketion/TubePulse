using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TubePulse.models;
using TubePulse.Utils;

namespace TubePulse
{
    public class Worker : BackgroundService
    {
        private HashSet<string> processedVideoIds;
        private readonly TubePulseSettings settings;
        private string ytDlpPath;

        public Worker(IOptions<TubePulseSettings> options)
        {
            settings = options.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
            Console.Title = $"TubePulse v{version}";
            try
            {
                // Brief delay to let host startup logs appear first
                await Task.Delay(100, stoppingToken);

                // Ensure yt-dlp is available (downloads if missing, then updates)
                ytDlpPath = await YtDlpManager.EnsureYtDlpAsync(settings.YtDlpNightlies);
                await YtDlpManager.UpdateYtDlpAsync(settings.YtDlpNightlies);

                await Task.Delay(5000, stoppingToken);
                var channels = settings.Channels;

                if (!checkPathsSpecified(settings.DownloadPath, settings.CachePath))
                {
                    Console.WriteLine("Error: DownloadPath and CachePath must be specified in appsettings.json.");
                    return;
                }

                Console.WriteLine("\nBeginning Processing.\n");
                while (!stoppingToken.IsCancellationRequested)
                {
                    foreach (Channel channel in channels)
                    {
                        if (stoppingToken.IsCancellationRequested)
                        {
                            Console.WriteLine("Shutdown requested. Stopping channel processing...");
                            break;
                        }

                        var channelName = channel.Name;
                        var channelUrl = channel.Url;

                        Console.WriteLine("---------------------------------------------------------");
                        Console.WriteLine($"Processing channel: {channelName}");
                        
                        if (!channel.Enabled)
                        {
                            Console.WriteLine($"Channel {channelName} is disabled. Skipping.");
                            continue;
                        }

                        var resolution = string.IsNullOrEmpty(channel.DownloadResolution) ? settings.DownloadResolution : channel.DownloadResolution;

                        processedVideoIds = CacheUtils.LoadCache(channelName, settings.CachePath);
                        bool isFirstRun = processedVideoIds.Count == 0;

                        if (isFirstRun)
                        {
                            Console.WriteLine($"First run for channel {channelName}: Fetching all existing videos and caching them...");
                            await FetchAndCacheAllVideos(channelUrl, channelName, stoppingToken);
                        }
                        else
                        {
                            Console.WriteLine($"Loaded {processedVideoIds.Count} cached video IDs for channel {channelName}.");
                        }

                        await CheckAndDownloadNewVideos(channel.Url, channel.Name, resolution, stoppingToken);
                    }

                    if (!stoppingToken.IsCancellationRequested)
                    {
                        Console.WriteLine("---------------------------------------------------------\n");
                        Console.WriteLine($"Completed at: {DateTime.Now.ToString("hh:mm:ss")} - Waiting {settings.PollingTimeoutHours} hours before next check...\n");
                        await Task.Delay(TimeSpan.FromHours(settings.PollingTimeoutHours), stoppingToken);

                        // Check for yt-dlp updates before next polling cycle
                        await YtDlpManager.UpdateYtDlpAsync(settings.YtDlpNightlies);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("\nShutdown initiated. Cleaning up...");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nFatal error: {ex.Message}");
                throw;
            }
            finally
            {
                Console.WriteLine("TubePulse service stopped.");
            }
        }

        private static bool checkPathsSpecified(string downloadPath, string cachePath)
        {
            return !string.IsNullOrWhiteSpace(downloadPath) && !string.IsNullOrWhiteSpace(cachePath);
        }

        private async Task FetchAndCacheAllVideos(string channelUrl, string channelName, CancellationToken cancellationToken)
        {
            Console.WriteLine($"Fetching all videos for: {channelUrl}");
            var videos = await GetVideos(channelUrl, null, cancellationToken);
            foreach (var video in videos)
            {
                processedVideoIds.Add(video.Id);
            }
            CacheUtils.SaveCache(channelName, settings.CachePath, processedVideoIds);
            Console.WriteLine($"Cached {processedVideoIds.Count} video IDs for channel.");
        }

        private async Task CheckAndDownloadNewVideos(string channelUrl, string channelName, string downloadResolution, CancellationToken cancellationToken)
        {
            Console.WriteLine($"Checking for new videos for: {channelUrl}");
            var recentVideos = await GetVideos(channelUrl, "today-1day", cancellationToken);
            var newVideos = recentVideos.Where(v => !processedVideoIds.Contains(v.Id)).ToList();

            if (newVideos.Any())
            {
                Console.WriteLine($"Found {newVideos.Count} new videos to download.\n");
                foreach (var video in newVideos)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        Console.WriteLine("Shutdown requested during download. Saving progress...");
                        CacheUtils.SaveCache(channelName, settings.CachePath, processedVideoIds);
                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    await DownloadVideo(video.Url, channelName, downloadResolution, cancellationToken);
                    processedVideoIds.Add(video.Id);
                    CacheUtils.SaveCache(channelName, settings.CachePath, processedVideoIds);
                }
            }
            else
            {
                Console.WriteLine("No new videos found.");
            }
        }

        private async Task<List<YoutubeVideo>> GetVideos(string url, string dateAfter, CancellationToken cancellationToken)
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
                FileName = ytDlpPath,
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

                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                using (cancellationToken.Register(() =>
                {
                    if (!process.HasExited)
                    {
                        try
                        {
                            process.Kill(true);
                        }
                        catch { }
                    }
                }))
                {
                    await process.WaitForExitAsync(cancellationToken);
                }

                string output = await outputTask;
                string error = await errorTask;

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

        private async Task DownloadVideo(string url, string channelName, string downloadResolution, CancellationToken cancellationToken)
        {
            Console.WriteLine($"Downloading video: {url} at resolution {downloadResolution}p.");

            var (sleepParams, sleepLog) = GetSleepParameters();
            if (!string.IsNullOrEmpty(sleepLog))
                Console.WriteLine(sleepLog);
            
            var argumentList = new List<string>
            {
                "-f",
                $"\"bv*[height<={downloadResolution}]+ba/b[height<={downloadResolution}] / wv*+ba/w\"",
                $"\"{url}\"",
                "--write-thumbnail",
                "--convert-thumbnails jpg"
            };
            
            if (!string.IsNullOrEmpty(sleepParams))
                argumentList.Add(sleepParams);

            Console.WriteLine($"Executing: yt-dlp {string.Join(" ", argumentList)}");

            var downloadPath = $"{settings.DownloadPath}/{channelName}";
            Directory.CreateDirectory(downloadPath);

            var startInfo = new ProcessStartInfo
            {
                FileName = ytDlpPath,
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

                try
                {
                    using (cancellationToken.Register(() =>
                    {
                        if (!process.HasExited)
                        {
                            try
                            {
                                process.Kill(true);
                                Console.WriteLine("yt-dlp process terminated due to shutdown.");
                            }
                            catch { }
                        }
                    }))
                    {
                        await process.WaitForExitAsync(cancellationToken);
                    }

                    if (process.ExitCode == 0)
                    {
                        Console.WriteLine("Download completed successfully.\n");
                    }
                    else
                    {
                        Console.WriteLine($"Download failed with exit code: {process.ExitCode}");
                    }
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("Download interrupted by shutdown request.");
                    throw;
                }
            }
        }

        private (string Params, string LogMessage) GetSleepParameters()
        {
            var min = settings.SleepInterval;
            var max = settings.MaxSleepInterval;
            
            if (min == 0 && max == 0)
                return (string.Empty, string.Empty);
            
            if (max > min)
                return ($"--sleep-interval {min} --max-sleep-interval {max}", $"Randomly sleeping between {min}-{max} seconds to avoid rate limiting.");
            
            return ($"--sleep-interval {min}", $"Sleeping for {min} seconds between downloads to avoid rate limiting.");
        }
    }
}
