using System;
using System.Diagnostics;
using System.IO;
using DreamyAscent.Data;
using DreamyAscent.Helpers;

namespace DreamyAscent.Services
{
    internal static class DaImportExportService
    {
        public static string ExportDirectoryPath { get; private set; }

        public static string ImportDirectoryPath { get; private set; }

        public static string FileDirectoryPath { get; private set; }

        public static void Initialize(string pluginDirectory)
        {
            string root = pluginDirectory ?? string.Empty;
            FileDirectoryPath = Path.Combine(root, "DreamyAscent Files");
            ExportDirectoryPath = FileDirectoryPath;
            ImportDirectoryPath = FileDirectoryPath;
            Directory.CreateDirectory(FileDirectoryPath);
            MigrateLegacyFiles(root);
            DaLog.Info("Terrain file directory: " + FileDirectoryPath);
        }

        public static string ExportCurrent(DaTerrainData data)
        {
            if (data == null || data.Map == null)
            {
                DaLog.Warn("Cannot export because terrain data is missing.");
                return string.Empty;
            }

            string mapKey = string.IsNullOrWhiteSpace(data.Map.MapKey) ? "unknown" : data.Map.MapKey;
            string name = Sanitize(mapKey) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".json";
            string path = Path.Combine(ExportDirectoryPath, name);
            File.WriteAllText(path, DaSaveService.ToJson(data));
            DaLog.Info("Exported terrain file: " + path);
            return path;
        }

        public static string[] GetImportFiles()
        {
            Directory.CreateDirectory(ImportDirectoryPath);
            string[] files = Directory.GetFiles(ImportDirectoryPath, "*.json");
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            Array.Reverse(files);
            return files;
        }

        public static bool TryImportFile(string path, out DaTerrainData data)
        {
            data = null;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                DaLog.Warn("Import file does not exist: " + path);
                return false;
            }

            try
            {
                data = DaSaveService.LoadFromString(File.ReadAllText(path));
                DaLog.Info("Imported terrain file: " + path);
                return data != null;
            }
            catch (Exception ex)
            {
                DaLog.Error("Failed to import terrain file " + path + ": " + ex);
                return false;
            }
        }

        public static bool TryOpenFileDirectory()
        {
            try
            {
                Directory.CreateDirectory(FileDirectoryPath);
                Process.Start("explorer.exe", "\"" + FileDirectoryPath + "\"");
                DaLog.Info("Opened terrain file directory: " + FileDirectoryPath);
                return true;
            }
            catch (Exception ex)
            {
                DaLog.Warn("Failed to open terrain file directory: " + ex.Message);
                return false;
            }
        }
        private static string Sanitize(string value)
        {
            string normalized = value ?? string.Empty;
            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                normalized = normalized.Replace(invalidChar, '_');
            }

            return string.IsNullOrWhiteSpace(normalized) ? "terrain" : normalized.Trim();
        }

        private static void MigrateLegacyFiles(string root)
        {
            CopyLegacyJsonFiles(Path.Combine(root ?? string.Empty, "TerrainCustomiserCN Files"));
            CopyLegacyJsonFiles(Path.Combine(root ?? string.Empty, "TerrainCustomiserCN Exports"));
            CopyLegacyJsonFiles(Path.Combine(root ?? string.Empty, "TerrainCustomiserCN Imports"));
            CopyLegacyJsonFiles(Path.Combine(root ?? string.Empty, "DreamyAscent Exports"));
            CopyLegacyJsonFiles(Path.Combine(root ?? string.Empty, "DreamyAscent Imports"));
        }

        private static void CopyLegacyJsonFiles(string legacyDirectory)
        {
            if (string.IsNullOrWhiteSpace(legacyDirectory) ||
                !Directory.Exists(legacyDirectory) ||
                string.Equals(legacyDirectory, FileDirectoryPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                string[] files = Directory.GetFiles(legacyDirectory, "*.json");
                int copied = 0;
                for (int index = 0; index < files.Length; index++)
                {
                    string source = files[index];
                    string destination = Path.Combine(FileDirectoryPath, Path.GetFileName(source));
                    if (File.Exists(destination))
                    {
                        continue;
                    }

                    File.Copy(source, destination, false);
                    copied++;
                }

                if (copied > 0)
                {
                    DaLog.Info("Migrated terrain JSON files. source=" + legacyDirectory + ", copied=" + copied);
                }
            }
            catch (Exception ex)
            {
                DaLog.Warn("Failed to migrate terrain JSON files from " + legacyDirectory + ": " + ex.Message);
            }
        }
    }
}


