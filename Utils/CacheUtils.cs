using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace TubePulse.Utils
{
    public static class CacheUtils
    {
        private const string CacheFileExtension = ".json";
        
        private static string GetCacheFilePath(string channelName, string cachePath)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            return $"{cachePath}/videoCache_{channelName}{CacheFileExtension}";
        }

        public static HashSet<string> LoadCache(string channelName, string cachePath)
        {
            var cacheFile = GetCacheFilePath(channelName, cachePath);
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

        public static void SaveCache(string channelName, string cachePath, HashSet<string> processedVideoIds)
        {
            var cacheFile = GetCacheFilePath(channelName, cachePath);
            try
            {
                Directory.CreateDirectory(cachePath);
                string json = JsonSerializer.Serialize(processedVideoIds.ToList());
                File.WriteAllText(cacheFile, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving cache for channel: {ex.Message}");
            }
        }
    }
}