using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
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

        public static string BackupPath
        {
            get { return Path.Combine(SavePath, "Backups"); }
        }

        private static string IndexPath
        {
            get { return Path.Combine(Application.persistentDataPath, "PeakMapBrowser", "map-index.json"); }
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
            Directory.CreateDirectory(BackupPath);
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
            return SaveDownloadedMap(map, bytes, false);
        }

        public static string SaveDownloadedMap(MapEntry map, byte[] bytes, bool allowOverwriteLocalChanges)
        {
            if (map == null)
            {
                throw new ArgumentNullException("map");
            }
            if (bytes == null)
            {
                throw new ArgumentNullException("bytes");
            }

            EnsureSaveDirectory();
            MapDownloadInfo info = GetDownloadInfo(map);
            if (info.Status == MapDownloadStatus.UpToDate)
            {
                return info.Path;
            }
            if (info.Status == MapDownloadStatus.LocalModified && !allowOverwriteLocalChanges)
            {
                throw new MapSaveException(MapDownloadStatus.LocalModified, "LOCAL_MODIFIED");
            }

            string baseName = string.IsNullOrWhiteSpace(map.name) ? "peak-map" : map.name.Trim();
            string safeName = SanitizeFileName(baseName);
            if (string.IsNullOrEmpty(safeName))
            {
                safeName = "peak-map";
            }

            LocalMapRecord record = FindRecord(map.id);
            string target = record == null ? null : record.path;
            if (string.IsNullOrEmpty(target) || !IsInsideRoot(target, SavePath))
            {
                target = Path.Combine(SavePath, safeName + ".json");
                int suffix = 2;
                while (File.Exists(target))
                {
                    target = Path.Combine(SavePath, safeName + "-" + suffix + ".json");
                    suffix++;
                }
            }

            string temp = target + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllBytes(temp, bytes);
                if (File.Exists(target))
                {
                    string backup = Path.Combine(BackupPath,
                        Path.GetFileNameWithoutExtension(target) + "-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + ".json");
                    File.Copy(target, backup, false);
                    File.Replace(temp, target, null);
                }
                else
                {
                    File.Move(temp, target);
                }

                SaveRecord(map, target, ComputeSha256(bytes));
            }
            finally
            {
                if (File.Exists(temp))
                {
                    File.Delete(temp);
                }
            }

            return target;
        }

        public static MapDownloadInfo GetDownloadInfo(MapEntry map)
        {
            MapDownloadInfo info = new MapDownloadInfo();
            if (map == null || string.IsNullOrEmpty(map.id))
            {
                info.Status = MapDownloadStatus.New;
                return info;
            }

            LocalMapRecord record = FindRecord(map.id);
            if (record == null || string.IsNullOrEmpty(record.path) || !File.Exists(record.path))
            {
                info.Status = MapDownloadStatus.New;
                info.CurrentRevision = map.revision;
                return info;
            }

            info.Path = record.path;
            info.LocalRevision = record.revision;
            info.CurrentRevision = map.revision;
            info.LocalHash = record.sha256;
            string currentHash = ComputeSha256(record.path);
            if (!string.Equals(currentHash, record.sha256, StringComparison.OrdinalIgnoreCase))
            {
                info.Status = MapDownloadStatus.LocalModified;
            }
            else if (map.revision > record.revision)
            {
                info.Status = MapDownloadStatus.UpdateAvailable;
            }
            else
            {
                info.Status = MapDownloadStatus.UpToDate;
            }

            return info;
        }

        private static LocalMapRecord FindRecord(string mapId)
        {
            if (string.IsNullOrEmpty(mapId))
            {
                return null;
            }

            LocalMapIndex index = LoadIndex();
            for (int i = 0; i < index.maps.Count; i++)
            {
                if (string.Equals(index.maps[i].map_id, mapId, StringComparison.Ordinal))
                {
                    return index.maps[i];
                }
            }

            return null;
        }

        private static void SaveRecord(MapEntry map, string path, string sha256)
        {
            LocalMapIndex index = LoadIndex();
            LocalMapRecord record = null;
            for (int i = 0; i < index.maps.Count; i++)
            {
                if (string.Equals(index.maps[i].map_id, map.id, StringComparison.Ordinal))
                {
                    record = index.maps[i];
                    break;
                }
            }

            if (record == null)
            {
                record = new LocalMapRecord();
                record.map_id = map.id;
                index.maps.Add(record);
            }

            record.path = path;
            record.name = map.name ?? string.Empty;
            record.revision = map.revision;
            record.sha256 = sha256;
            SaveIndex(index);
        }

        private static LocalMapIndex LoadIndex()
        {
            try
            {
                if (!File.Exists(IndexPath))
                {
                    return new LocalMapIndex();
                }

                LocalMapIndex index = JsonConvert.DeserializeObject<LocalMapIndex>(File.ReadAllText(IndexPath));
                return index ?? new LocalMapIndex();
            }
            catch
            {
                return new LocalMapIndex();
            }
        }

        private static void SaveIndex(LocalMapIndex index)
        {
            string directory = Path.GetDirectoryName(IndexPath);
            Directory.CreateDirectory(directory);
            string temp = IndexPath + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(temp, JsonConvert.SerializeObject(index, Formatting.Indented), new UTF8Encoding(false));
                if (File.Exists(IndexPath))
                {
                    File.Replace(temp, IndexPath, null);
                }
                else
                {
                    File.Move(temp, IndexPath);
                }
            }
            finally
            {
                if (File.Exists(temp))
                {
                    File.Delete(temp);
                }
            }
        }

        private static string ComputeSha256(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            {
                using (SHA256 sha = SHA256.Create())
                {
                    return ToHex(sha.ComputeHash(stream));
                }
            }
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return ToHex(sha.ComputeHash(bytes));
            }
        }

        private static string ToHex(byte[] bytes)
        {
            StringBuilder sb = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++)
            {
                sb.Append(bytes[i].ToString("x2"));
            }

            return sb.ToString();
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

    internal enum MapDownloadStatus
    {
        New,
        UpToDate,
        UpdateAvailable,
        LocalModified
    }

    internal sealed class MapDownloadInfo
    {
        public MapDownloadStatus Status;
        public string Path;
        public int LocalRevision;
        public int CurrentRevision;
        public string LocalHash;
    }

    internal sealed class LocalMapIndex
    {
        public List<LocalMapRecord> maps = new List<LocalMapRecord>();
    }

    internal sealed class LocalMapRecord
    {
        public string map_id;
        public string path;
        public string name;
        public int revision;
        public string sha256;
    }

    internal sealed class MapSaveException : Exception
    {
        public readonly MapDownloadStatus Status;

        public MapSaveException(MapDownloadStatus status, string message)
            : base(message)
        {
            Status = status;
        }
    }
}
