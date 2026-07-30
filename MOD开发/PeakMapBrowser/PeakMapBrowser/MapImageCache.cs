using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace PeakMapBrowser
{
    internal static class MapImageCache
    {
        private const long MaxCacheBytes = 256L * 1024L * 1024L;
        private const int MaxCacheFiles = 300;
        private static readonly object Sync = new object();

        private static string CacheDirectory
        {
            get { return Path.Combine(Application.persistentDataPath, "PeakMapBrowser", "ImageCache"); }
        }

        public static string GetExistingPath(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return null;
            }

            lock (Sync)
            {
                try
                {
                    string path = GetCachePath(url);
                    if (!File.Exists(path) || new FileInfo(path).Length == 0)
                    {
                        return null;
                    }

                    Touch(path);
                    return path;
                }
                catch
                {
                    return null;
                }
            }
        }

        public static void Remove(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return;
            }

            lock (Sync)
            {
                try
                {
                    string path = GetCachePath(url);
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
                catch
                {
                    // A stale cache file must not break a later network request.
                }
            }
        }

        public static void Save(string url, byte[] bytes)
        {
            if (string.IsNullOrEmpty(url) || bytes == null || bytes.Length == 0)
            {
                return;
            }

            lock (Sync)
            {
                try
                {
                    Directory.CreateDirectory(CacheDirectory);
                    string target = GetCachePath(url);
                    if (File.Exists(target))
                    {
                        Touch(target);
                        return;
                    }

                    string temp = target + ".tmp-" + Guid.NewGuid().ToString("N");
                    try
                    {
                        File.WriteAllBytes(temp, bytes);
                        File.Move(temp, target);
                    }
                    finally
                    {
                        if (File.Exists(temp))
                        {
                            File.Delete(temp);
                        }
                    }

                    PruneCache();
                }
                catch
                {
                    // A cache failure must never make the image request fail.
                }
            }
        }

        private static string GetCachePath(string url)
        {
            Directory.CreateDirectory(CacheDirectory);
            return Path.Combine(CacheDirectory, "image-" + Hash(url) + ".cache");
        }

        private static string Hash(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
                StringBuilder result = new StringBuilder(bytes.Length * 2);
                for (int i = 0; i < bytes.Length; i++)
                {
                    result.Append(bytes[i].ToString("x2"));
                }

                return result.ToString();
            }
        }

        private static void Touch(string path)
        {
            try
            {
                File.SetLastAccessTimeUtc(path, DateTime.UtcNow);
            }
            catch
            {
                // Access time is only used for optional cleanup.
            }
        }

        private static void PruneCache()
        {
            string[] files = Directory.GetFiles(CacheDirectory, "*.cache", SearchOption.TopDirectoryOnly);
            Array.Sort(files, (a, b) => File.GetLastAccessTimeUtc(a).CompareTo(File.GetLastAccessTimeUtc(b)));

            long totalBytes = 0L;
            for (int i = 0; i < files.Length; i++)
            {
                totalBytes += new FileInfo(files[i]).Length;
            }

            int removeCount = Mathf.Max(0, files.Length - MaxCacheFiles);
            for (int i = 0; i < files.Length && (totalBytes > MaxCacheBytes || i < removeCount); i++)
            {
                try
                {
                    long length = new FileInfo(files[i]).Length;
                    File.Delete(files[i]);
                    totalBytes -= length;
                }
                catch
                {
                    // Ignore files locked by another process.
                }
            }
        }
    }
}
