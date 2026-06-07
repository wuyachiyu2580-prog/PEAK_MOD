using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace PeakMapBrowser
{
    internal static class MapSaveService
    {
        public static string SavePath
        {
            get { return Path.Combine(Application.persistentDataPath, "TerrainCustomiser", "Map Saves"); }
        }

        public static string CoverPath
        {
            get { return Path.Combine(SavePath, "Covers"); }
        }

        public static string PicturesPath
        {
            get { return Environment.GetFolderPath(Environment.SpecialFolder.MyPictures); }
        }

        public static string[] GetLocalJsonFiles()
        {
            EnsureSaveDirectory();

            string[] files = Directory.GetFiles(SavePath, "*.json");
            Array.Sort(files, (a, b) => File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)));
            return files;
        }

        public static string[] GetLocalImageFiles()
        {
            EnsureSaveDirectory();

            string[] roots = GetImageRootPaths();
            System.Collections.Generic.List<string> files = new System.Collections.Generic.List<string>();
            for (int i = 0; i < roots.Length; i++)
            {
                if (!Directory.Exists(roots[i]))
                {
                    continue;
                }

                string[] found = Directory.GetFiles(roots[i], "*.*", SearchOption.TopDirectoryOnly);
                for (int j = 0; j < found.Length; j++)
                {
                    if (IsSupportedImage(found[j]))
                    {
                        files.Add(found[j]);
                    }
                }
            }

            files.Sort((a, b) => File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)));
            return files.ToArray();
        }

        public static void EnsureSaveDirectory()
        {
            Directory.CreateDirectory(SavePath);
            Directory.CreateDirectory(CoverPath);
        }

        public static string[] GetImageRootPaths()
        {
            EnsureSaveDirectory();
            if (string.IsNullOrEmpty(PicturesPath))
            {
                return new[] { SavePath, CoverPath };
            }

            return new[] { SavePath, CoverPath, PicturesPath };
        }

        public static string GetImageRootLabel(string root)
        {
            string full = NormalizePath(root);
            if (string.Equals(full, NormalizePath(SavePath), StringComparison.OrdinalIgnoreCase))
            {
                return "Map Saves";
            }
            if (string.Equals(full, NormalizePath(CoverPath), StringComparison.OrdinalIgnoreCase))
            {
                return "Covers";
            }
            if (!string.IsNullOrEmpty(PicturesPath) && string.Equals(full, NormalizePath(PicturesPath), StringComparison.OrdinalIgnoreCase))
            {
                return "Pictures";
            }

            return DisplayName(root);
        }

        public static bool TryNormalizeWhitelistedDirectory(string path, out string normalized)
        {
            normalized = null;
            if (string.IsNullOrEmpty(path) || IsNetworkPath(path))
            {
                return false;
            }

            string candidate;
            try
            {
                candidate = NormalizePath(path);
            }
            catch
            {
                return false;
            }

            if (!Directory.Exists(candidate) || IsHiddenOrSystem(candidate))
            {
                return false;
            }

            string[] roots = GetImageRootPaths();
            for (int i = 0; i < roots.Length; i++)
            {
                if (IsInsideRoot(candidate, roots[i]))
                {
                    normalized = candidate;
                    return true;
                }
            }

            return false;
        }

        public static bool TryNormalizeWhitelistedImage(string path, out string normalized)
        {
            normalized = null;
            if (string.IsNullOrEmpty(path) || IsNetworkPath(path) || !IsSupportedImage(path))
            {
                return false;
            }

            string candidate;
            try
            {
                candidate = NormalizePath(path);
            }
            catch
            {
                return false;
            }

            if (!File.Exists(candidate))
            {
                return false;
            }

            string parent = Path.GetDirectoryName(candidate);
            if (!TryNormalizeWhitelistedDirectory(parent, out _))
            {
                return false;
            }

            normalized = candidate;
            return true;
        }

        public static string[] GetChildDirectories(string directory)
        {
            string normalized;
            if (!TryNormalizeWhitelistedDirectory(directory, out normalized))
            {
                return new string[0];
            }

            string[] dirs = Directory.GetDirectories(normalized, "*", SearchOption.TopDirectoryOnly);
            System.Collections.Generic.List<string> safe = new System.Collections.Generic.List<string>();
            for (int i = 0; i < dirs.Length; i++)
            {
                string child;
                if (TryNormalizeWhitelistedDirectory(dirs[i], out child))
                {
                    safe.Add(child);
                }
            }

            safe.Sort(StringComparer.OrdinalIgnoreCase);
            return safe.ToArray();
        }

        public static string[] GetImageFilesInDirectory(string directory)
        {
            string normalized;
            if (!TryNormalizeWhitelistedDirectory(directory, out normalized))
            {
                return new string[0];
            }

            string[] files = Directory.GetFiles(normalized, "*.*", SearchOption.TopDirectoryOnly);
            System.Collections.Generic.List<string> images = new System.Collections.Generic.List<string>();
            for (int i = 0; i < files.Length; i++)
            {
                string image;
                if (TryNormalizeWhitelistedImage(files[i], out image))
                {
                    images.Add(image);
                }
            }

            images.Sort((a, b) => File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)));
            return images.ToArray();
        }

        public static string SaveDownloadedMap(MapEntry map, byte[] bytes)
        {
            EnsureSaveDirectory();
            string baseName = string.IsNullOrWhiteSpace(map.name) ? "peak-map" : map.name.Trim();
            string safeName = SanitizeFileName(baseName);
            if (string.IsNullOrEmpty(safeName))
            {
                safeName = "peak-map";
            }

            string target = Path.Combine(SavePath, safeName + ".json");
            int suffix = 2;
            while (File.Exists(target))
            {
                target = Path.Combine(SavePath, safeName + "-" + suffix + ".json");
                suffix++;
            }

            File.WriteAllBytes(target, bytes);
            return target;
        }

        public static string SanitizeFileName(string value)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            StringBuilder sb = new StringBuilder(value.Length);
            foreach (char ch in value)
            {
                bool bad = false;
                for (int i = 0; i < invalid.Length; i++)
                {
                    if (ch == invalid[i])
                    {
                        bad = true;
                        break;
                    }
                }

                if (!bad)
                {
                    sb.Append(ch);
                }
            }

            return sb.ToString().Trim();
        }

        public static string DisplayName(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : Path.GetFileName(path);
        }

        public static bool IsSupportedImage(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".webp" || ext == ".gif";
        }

        private static bool IsHiddenOrSystem(string path)
        {
            try
            {
                FileAttributes attr = File.GetAttributes(path);
                return (attr & FileAttributes.Hidden) != 0 || (attr & FileAttributes.System) != 0;
            }
            catch
            {
                return true;
            }
        }

        private static bool IsNetworkPath(string path)
        {
            return path.StartsWith(@"\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal);
        }

        private static bool IsInsideRoot(string candidate, string root)
        {
            string fullCandidate = NormalizePath(candidate);
            string fullRoot = NormalizePath(root);
            if (string.Equals(fullCandidate, fullRoot, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string rootWithSlash = fullRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return fullCandidate.StartsWith(rootWithSlash, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePath(string path)
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        public static string FindMatchingImage(string jsonPath, string[] imageFiles)
        {
            if (string.IsNullOrEmpty(jsonPath) || imageFiles == null || imageFiles.Length == 0)
            {
                return null;
            }

            string jsonName = Path.GetFileNameWithoutExtension(jsonPath);
            for (int i = 0; i < imageFiles.Length; i++)
            {
                string imageName = Path.GetFileNameWithoutExtension(imageFiles[i]);
                if (string.Equals(jsonName, imageName, StringComparison.OrdinalIgnoreCase))
                {
                    return imageFiles[i];
                }
            }

            return null;
        }
    }
}
