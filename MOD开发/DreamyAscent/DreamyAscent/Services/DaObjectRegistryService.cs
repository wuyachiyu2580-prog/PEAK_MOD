using System;
using System.Collections.Generic;
using System.IO;
using DreamyAscent.Data;
using DreamyAscent.Helpers;
using Newtonsoft.Json;

namespace DreamyAscent.Services
{
    internal static class DaObjectRegistryService
    {
        private const string RegistryFileName = "object-registry-input.json";
        private static readonly JsonSerializerSettings s_jsonSettings = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Include
        };

        private static DaObjectRegistry s_registry;
        private static bool s_loaded;

        public static string DataDirectoryPath { get; private set; }

        public static string RegistryFilePath { get; private set; }

        public static void Initialize(string pluginDirectory)
        {
            DataDirectoryPath = DaPathResolver.ResolveDataDirectory(pluginDirectory);
            Directory.CreateDirectory(DataDirectoryPath);
            RegistryFilePath = Path.Combine(DataDirectoryPath, RegistryFileName);
            DaLog.Info("Object registry data path: " + RegistryFilePath);
        }

        public static DaObjectRegistry GetRegistry()
        {
            EnsureLoaded();
            return s_registry;
        }

        public static List<DaObjectRegistryTemplate> GetRecommendedTemplatesForSegment(string segmentName)
        {
            return GetRecommendedTemplatesForSegment(segmentName, null);
        }

        public static List<DaObjectRegistryTemplate> GetRecommendedTemplatesForSegment(DaSegmentData segment)
        {
            return GetRecommendedTemplatesForSegment(
                segment != null ? segment.SegmentName : null,
                segment != null ? segment.NormalizedVariantName : null);
        }

        public static bool IsTemplateAvailableForSegmentVariant(DaObjectRegistryTemplate template, DaSegmentData segment)
        {
            if (template == null || segment == null)
            {
                return false;
            }

            return IsTemplateAvailableForSegmentVariant(template, segment.SegmentName, segment.NormalizedVariantName);
        }

        private static List<DaObjectRegistryTemplate> GetRecommendedTemplatesForSegment(string segmentName, string normalizedVariantName)
        {
            EnsureLoaded();
            List<DaObjectRegistryTemplate> result = new List<DaObjectRegistryTemplate>();
            if (s_registry == null || s_registry.Templates == null || string.IsNullOrWhiteSpace(segmentName))
            {
                return result;
            }

            for (int index = 0; index < s_registry.Templates.Count; index++)
            {
                DaObjectRegistryTemplate template = s_registry.Templates[index];
                if (template == null || !template.RecommendedFirstPassCandidate || template.Segments == null)
                {
                    continue;
                }

                for (int segmentIndex = 0; segmentIndex < template.Segments.Count; segmentIndex++)
                {
                    if (string.Equals(template.Segments[segmentIndex], segmentName, StringComparison.Ordinal))
                    {
                        if (IsTemplateAvailableForSegmentVariant(template, segmentName, normalizedVariantName))
                        {
                            result.Add(template);
                        }

                        break;
                    }
                }
            }

            return result;
        }

        private static bool IsTemplateAvailableForSegmentVariant(DaObjectRegistryTemplate template, string segmentName, string normalizedVariantName)
        {
            if (template == null || string.IsNullOrWhiteSpace(segmentName))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(normalizedVariantName) ||
                template.Variants == null ||
                !template.Variants.TryGetValue(segmentName, out List<string> variants) ||
                variants == null ||
                variants.Count == 0)
            {
                return true;
            }

            for (int index = 0; index < variants.Count; index++)
            {
                if (string.Equals(variants[index], normalizedVariantName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
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
                DaLog.OnceWarn("object-registry-missing", "Object registry file not found: " + (RegistryFilePath ?? string.Empty));
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(RegistryFilePath))
                using (JsonTextReader jsonReader = new JsonTextReader(reader))
                {
                    s_registry = JsonSerializer.Create(s_jsonSettings).Deserialize<DaObjectRegistry>(jsonReader);
                }

                int templateCount = s_registry != null && s_registry.Templates != null ? s_registry.Templates.Count : 0;
                int materialCount = s_registry != null && s_registry.Materials != null ? s_registry.Materials.Count : 0;
                DaLog.Info("Object registry loaded. templates=" + templateCount + ", materials=" + materialCount);
            }
            catch (Exception ex)
            {
                s_registry = null;
                DaLog.Warn("Failed to load object registry: " + ex.Message);
            }
        }
    }
}
