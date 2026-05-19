using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using DreamyAscent.Data;
using DreamyAscent.Helpers;

namespace DreamyAscent.Services
{
    internal static class DaSaveService
    {
        private static readonly JsonSerializerSettings s_jsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

        public static string SaveDirectoryPath { get; private set; }

        public static void Initialize(string pluginDirectory)
        {
            SaveDirectoryPath = Path.Combine(pluginDirectory ?? string.Empty, "Map Saves");
            Directory.CreateDirectory(SaveDirectoryPath);
            DaLog.Info("Save directory: " + SaveDirectoryPath);
        }

        public static List<string> GetSaveNames()
        {
            EnsureInitialized();
            return Directory.GetFiles(SaveDirectoryPath, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static DaTerrainData LoadRaw(string saveName)
        {
            EnsureInitialized();
            string path = GetSaveFilePath(saveName);
            if (!File.Exists(path))
            {
                DaLog.Warn("Save file does not exist: " + path);
                return null;
            }

            string json = File.ReadAllText(path);
            DaLog.Info("Loaded save file: " + path);
            return JsonConvert.DeserializeObject<DaTerrainData>(json, s_jsonSettings);
        }

        public static DaTerrainData LoadFromString(string json)
        {
            return string.IsNullOrWhiteSpace(json)
                ? null
                : JsonConvert.DeserializeObject<DaTerrainData>(json, s_jsonSettings);
        }

        public static string ToJson(DaTerrainData data)
        {
            return JsonConvert.SerializeObject(data ?? new DaTerrainData(), s_jsonSettings);
        }

        public static void Save(string saveName, DaTerrainData data)
        {
            EnsureInitialized();
            string normalizedName = NormalizeSaveName(saveName);
            string path = GetSaveFilePath(normalizedName);
            string json = ToJson(data);
            File.WriteAllText(path, json);
            DaLog.Info("Saved file: " + path);
        }

        public static void Delete(string saveName)
        {
            EnsureInitialized();
            string path = GetSaveFilePath(saveName);
            if (!File.Exists(path))
            {
                return;
            }

            File.Delete(path);
            DaLog.Info("Deleted save file: " + path);
        }

        public static string NormalizeSaveName(string saveName)
        {
            if (string.IsNullOrWhiteSpace(saveName))
            {
                return string.Empty;
            }

            string normalized = saveName;
            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                normalized = normalized.Replace(invalidChar, '_');
            }

            return normalized.Trim();
        }

        public static DaTerrainData EnsureDefaultSaveExists()
        {
            EnsureInitialized();
            string defaultPath = GetSaveFilePath("Default");
            if (!File.Exists(defaultPath))
            {
                DaTerrainData data = new DaTerrainData();
                Save("Default", data);
                return data;
            }

            return LoadRaw("Default");
        }

        public static JObject ToJObject(DaTerrainData data)
        {
            return JObject.FromObject(data ?? new DaTerrainData(), JsonSerializer.Create(s_jsonSettings));
        }

        private static void EnsureInitialized()
        {
            if (string.IsNullOrWhiteSpace(SaveDirectoryPath))
            {
                throw new InvalidOperationException("DaSaveService has not been initialized.");
            }
        }

        private static string GetSaveFilePath(string saveName)
        {
            return Path.Combine(SaveDirectoryPath, NormalizeSaveName(saveName) + ".json");
        }
    }
}


