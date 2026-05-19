using System;
using System.Collections.Generic;
using System.IO;
using DreamyAscent.Data;
using DreamyAscent.Helpers;
using Newtonsoft.Json;

namespace DreamyAscent.Services
{
    internal static class DaParentChildRegistryService
    {
        private const string RegistryFileName = "parent-child-registry.json";
        private static readonly JsonSerializerSettings s_jsonSettings = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Include
        };

        private static DaParentChildRegistry s_registry;
        private static bool s_loaded;

        public static string RegistryFilePath { get; private set; }

        public static void Initialize(string pluginDirectory)
        {
            string dataDirectory = DaPathResolver.ResolveDataDirectory(pluginDirectory);
            Directory.CreateDirectory(dataDirectory);
            RegistryFilePath = Path.Combine(dataDirectory, RegistryFileName);
            DaLog.Info("Parent-child registry data path: " + RegistryFilePath);
        }

        public static DaParentChildRegistry GetRegistry()
        {
            EnsureLoaded();
            return s_registry;
        }

        public static List<DaParentChildRegistryTemplate> GetTemplatesForSegment(DaSegmentData segment)
        {
            EnsureLoaded();
            List<DaParentChildRegistryTemplate> result = new List<DaParentChildRegistryTemplate>();
            if (segment == null || s_registry == null || s_registry.Templates == null)
            {
                return result;
            }

            for (int index = 0; index < s_registry.Templates.Count; index++)
            {
                DaParentChildRegistryTemplate template = s_registry.Templates[index];
                if (template == null || template.Segments == null)
                {
                    continue;
                }

                if (template.Segments.Contains(segment.SegmentName))
                {
                    result.Add(template);
                }
            }

            return result;
        }

        public static void Invalidate()
        {
            s_loaded = false;
            s_registry = null;
        }

        private static void EnsureLoaded()
        {
            if (s_loaded)
            {
                return;
            }

            s_loaded = true;
            if (string.IsNullOrWhiteSpace(RegistryFilePath) || !File.Exists(RegistryFilePath))
            {
                DaLog.OnceWarn("parent-child-registry-missing", "Parent-child registry file not found: " + (RegistryFilePath ?? string.Empty));
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(RegistryFilePath))
                using (JsonTextReader jsonReader = new JsonTextReader(reader))
                {
                    s_registry = JsonSerializer.Create(s_jsonSettings).Deserialize<DaParentChildRegistry>(jsonReader);
                }

                int templateCount = s_registry != null && s_registry.Templates != null ? s_registry.Templates.Count : 0;
                int segmentCount = s_registry != null && s_registry.Segments != null ? s_registry.Segments.Count : 0;
                DaLog.Info("Parent-child registry loaded. templates=" + templateCount + ", segments=" + segmentCount);
            }
            catch (Exception ex)
            {
                s_registry = null;
                DaLog.Warn("Failed to load parent-child registry: " + ex.Message);
            }
        }
    }
}
