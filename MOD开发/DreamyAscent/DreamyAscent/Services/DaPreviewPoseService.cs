using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using DreamyAscent.Helpers;
using UnityEngine;

namespace DreamyAscent.Services
{
    internal static class DaPreviewPoseService
    {
        private static readonly JsonSerializerSettings s_jsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented
        };

        private static readonly Encoding s_utf8Bom = new UTF8Encoding(true);
        private static readonly Dictionary<string, DaPreviewPoseEntry> s_poses = new Dictionary<string, DaPreviewPoseEntry>(StringComparer.OrdinalIgnoreCase);

        public static string PoseFilePath { get; private set; }

        public static void Initialize(string pluginDirectory)
        {
            string root = pluginDirectory ?? string.Empty;
            PoseFilePath = Path.Combine(root, "DreamyAscent PreviewPoses.json");
            MigrateLegacyPoseFile(root);
            Load();
            DaLog.Info("Preview pose file: " + PoseFilePath);
        }

        public static bool TryGetPose(string segmentName, out Vector3 position, out Vector3 lookAt, out float fieldOfView)
        {
            position = Vector3.zero;
            lookAt = Vector3.zero;
            fieldOfView = 24f;

            if (string.IsNullOrWhiteSpace(segmentName))
            {
                return false;
            }

            if (!s_poses.TryGetValue(segmentName.Trim(), out DaPreviewPoseEntry entry) || entry == null)
            {
                return false;
            }

            position = entry.Position.ToVector3();
            lookAt = entry.LookAt.ToVector3();
            fieldOfView = entry.FieldOfView > 1f ? entry.FieldOfView : 24f;
            return true;
        }

        public static void SavePose(string segmentName, Vector3 position, Vector3 lookAt, float fieldOfView)
        {
            EnsureInitialized();
            if (string.IsNullOrWhiteSpace(segmentName))
            {
                DaLog.Warn("Cannot save preview pose because segment name is missing.");
                return;
            }

            string key = segmentName.Trim();
            s_poses[key] = new DaPreviewPoseEntry
            {
                Position = DaVector3Entry.FromVector3(position),
                LookAt = DaVector3Entry.FromVector3(lookAt),
                FieldOfView = fieldOfView > 1f ? fieldOfView : 24f
            };

            Save();
            DaLog.Info("Saved preview pose: segment=" + key + ", position=" + position + ", lookAt=" + lookAt + ", fov=" + fieldOfView);
        }

        private static void Load()
        {
            s_poses.Clear();
            if (string.IsNullOrWhiteSpace(PoseFilePath) || !File.Exists(PoseFilePath))
            {
                return;
            }

            try
            {
                string json = File.ReadAllText(PoseFilePath, Encoding.UTF8);
                DaPreviewPoseFile file = JsonConvert.DeserializeObject<DaPreviewPoseFile>(json, s_jsonSettings);
                if (file == null || file.Poses == null)
                {
                    return;
                }

                foreach (KeyValuePair<string, DaPreviewPoseEntry> pair in file.Poses)
                {
                    if (!string.IsNullOrWhiteSpace(pair.Key) && pair.Value != null)
                    {
                        s_poses[pair.Key.Trim()] = pair.Value;
                    }
                }

                DaLog.Info("Loaded preview poses: count=" + s_poses.Count);
            }
            catch (Exception ex)
            {
                DaLog.Error("Failed to load preview poses.", ex);
            }
        }

        private static void MigrateLegacyPoseFile(string root)
        {
            if (string.IsNullOrWhiteSpace(PoseFilePath) || File.Exists(PoseFilePath))
            {
                return;
            }

            string oldPath = Path.Combine(root ?? string.Empty, "TerrainCustomiserCN PreviewPoses.json");
            if (!File.Exists(oldPath))
            {
                return;
            }

            try
            {
                File.Copy(oldPath, PoseFilePath, false);
                DaLog.Info("Migrated preview pose file. source=" + oldPath + ", destination=" + PoseFilePath);
            }
            catch (Exception ex)
            {
                DaLog.Warn("Failed to migrate preview pose file: " + ex.Message);
            }
        }

        private static void Save()
        {
            EnsureInitialized();
            try
            {
                string directory = Path.GetDirectoryName(PoseFilePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                DaPreviewPoseFile file = new DaPreviewPoseFile { Poses = s_poses };
                string json = JsonConvert.SerializeObject(file, s_jsonSettings);
                File.WriteAllText(PoseFilePath, json, s_utf8Bom);
            }
            catch (Exception ex)
            {
                DaLog.Error("Failed to save preview poses.", ex);
            }
        }

        private static void EnsureInitialized()
        {
            if (string.IsNullOrWhiteSpace(PoseFilePath))
            {
                throw new InvalidOperationException("DaPreviewPoseService has not been initialized.");
            }
        }

        private sealed class DaPreviewPoseFile
        {
            [JsonProperty("poses")]
            public Dictionary<string, DaPreviewPoseEntry> Poses { get; set; } = new Dictionary<string, DaPreviewPoseEntry>(StringComparer.OrdinalIgnoreCase);
        }

        private sealed class DaPreviewPoseEntry
        {
            [JsonProperty("position")]
            public DaVector3Entry Position { get; set; } = new DaVector3Entry();

            [JsonProperty("lookAt")]
            public DaVector3Entry LookAt { get; set; } = new DaVector3Entry();

            [JsonProperty("fieldOfView")]
            public float FieldOfView { get; set; } = 24f;
        }

        private sealed class DaVector3Entry
        {
            [JsonProperty("x")]
            public float X { get; set; }

            [JsonProperty("y")]
            public float Y { get; set; }

            [JsonProperty("z")]
            public float Z { get; set; }

            public Vector3 ToVector3()
            {
                return new Vector3(X, Y, Z);
            }

            public static DaVector3Entry FromVector3(Vector3 value)
            {
                return new DaVector3Entry
                {
                    X = value.x,
                    Y = value.y,
                    Z = value.z
                };
            }
        }
    }
}


