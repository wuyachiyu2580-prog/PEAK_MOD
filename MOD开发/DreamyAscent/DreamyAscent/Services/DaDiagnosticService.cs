using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using DreamyAscent.Data;
using DreamyAscent.Helpers;
using UnityEngine;

namespace DreamyAscent.Services
{
    internal static class DaDiagnosticService
    {
        private static readonly JsonSerializerSettings s_jsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

        public static string DiagnosticDirectoryPath { get; private set; }

        public static void Initialize(string pluginDirectory)
        {
            DiagnosticDirectoryPath = Path.Combine(pluginDirectory ?? string.Empty, "DreamyAscent Diagnostics");
            Directory.CreateDirectory(DiagnosticDirectoryPath);
            DaLog.Info("Diagnostic directory: " + DiagnosticDirectoryPath);
        }

        public static void WriteRuntimeExport(DaTerrainData data)
        {
            EnsureInitialized();

            string mapDirectory = GetMapDiagnosticDirectory(data, true);
            string path = Path.Combine(mapDirectory, "RuntimeExport.json");
            File.WriteAllText(path, DaSaveService.ToJson(data));
            DaLog.Info("Runtime export diagnostic written: " + path);

            WriteNameMap(data, mapDirectory);
            DaObjectReferenceDiagnosticService.WriteObjectReferenceMap(data, mapDirectory);
            DaObjectCatalogDiagnosticService.WriteObjectCatalog(data, mapDirectory);
            DaTemplateSnapshotDiagnosticService.WriteTemplateSnapshotMatchReport(data, mapDirectory);
            WriteTemplateBaselineReport(data, mapDirectory);
            DaGeneratedChildrenSnapshotDiagnosticService.WriteGeneratedChildrenSnapshot(data, mapDirectory);
        }

        public static void WriteCustomBlankRemaining(DaTerrainData data, DaSegmentData segment)
        {
            if (segment == null)
            {
                return;
            }

            try
            {
                EnsureInitialized();

                DaCustomBlankRemainingDiagnostic diagnostic = BuildCustomBlankRemaining(data, segment);
                string mapDirectory = GetMapDiagnosticDirectory(data, false);
                string safeSegmentName = SanitizeFileName(segment.SegmentName);
                string path = Path.Combine(mapDirectory, "CustomBlankRemaining_" + safeSegmentName + ".json");
                File.WriteAllText(path, JsonConvert.SerializeObject(diagnostic, s_jsonSettings));
                DaLog.Info(string.Format(
                    "Custom blank remaining diagnostic written: {0}, candidates={1}, renderers={2}, colliders={3}",
                    path,
                    diagnostic.CandidateCount,
                    diagnostic.RendererObjectCount,
                    diagnostic.ColliderObjectCount));
            }
            catch (Exception ex)
            {
                DaLog.Warn("Failed to write custom blank remaining diagnostic: " + ex.Message);
            }
        }

        private static void WriteNameMap(DaTerrainData data, string mapDirectory)
        {
            DaNameMap nameMap = new DaNameMap();

            if (data != null && data.Map != null && data.Map.Segments != null)
            {
                foreach (DaSegmentData segment in data.Map.Segments)
                {
                    AddName(nameMap.Segments, segment.SegmentName);

                    foreach (DaPropGrouperData grouper in segment.Groupers ?? Enumerable.Empty<DaPropGrouperData>())
                    {
                        AddName(nameMap.Groupers, grouper.GrouperName);

                        foreach (DaLevelGenStepData step in grouper.Steps ?? Enumerable.Empty<DaLevelGenStepData>())
                        {
                            AddName(nameMap.Steps, step.StepName);
                            AddName(nameMap.StepTypes, step.StepType);

                            AddProperties(nameMap.Properties, step.Properties);
                            AddConstraints(nameMap.ConstraintTypes, nameMap.Properties, step.Modifiers);
                            AddConstraints(nameMap.ConstraintTypes, nameMap.Properties, step.Constraints);
                            AddConstraints(nameMap.ConstraintTypes, nameMap.Properties, step.PostConstraints);
                        }
                    }
                }
            }

            Sort(nameMap.Segments);
            Sort(nameMap.Groupers);
            Sort(nameMap.Steps);
            Sort(nameMap.StepTypes);
            Sort(nameMap.Properties);
            Sort(nameMap.ConstraintTypes);

            string path = Path.Combine(mapDirectory, "NameMap.json");
            File.WriteAllText(path, JsonConvert.SerializeObject(nameMap, s_jsonSettings));
            DaLog.Info("Name map diagnostic written: " + path);
        }

        private static void WriteTemplateBaselineReport(DaTerrainData data, string mapDirectory)
        {
            try
            {
                DaTemplateBaselineReport report = DaTemplateBaselineService.BuildReport(data);
                string path = Path.Combine(mapDirectory, "TemplateBaselineReport.json");
                File.WriteAllText(path, JsonConvert.SerializeObject(report, s_jsonSettings));
                DaLog.Info(string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "Template baseline report written: {0}, ready={1}, warnings={2}",
                    path,
                    report.ReadyCount,
                    report.WarningCount));
            }
            catch (Exception ex)
            {
                DaLog.Warn("Failed to write template baseline report: " + ex.Message);
            }
        }

        private static string GetMapDiagnosticDirectory(DaTerrainData data, bool createUniqueSampleDirectory)
        {
            string mapKey = BuildMapDirectoryBase(data);
            string variantSuffix = BuildVariantDirectorySuffix(data);
            string folderName = string.IsNullOrWhiteSpace(variantSuffix)
                ? mapKey
                : mapKey + "__" + variantSuffix;
            folderName = ShortenFolderName(folderName, mapKey + "|" + variantSuffix);

            if (createUniqueSampleDirectory)
            {
                folderName = BuildUniqueSampleFolderName(mapKey + "|" + variantSuffix);
            }

            string path = Path.Combine(DiagnosticDirectoryPath, folderName);
            Directory.CreateDirectory(path);
            return path;
        }

        private static string BuildUniqueSampleFolderName(string uniqueKey)
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            string hash = ComputeShortHash(uniqueKey + "|" + stamp + "|" + Guid.NewGuid().ToString("N"));
            return "S_" + stamp + "_" + hash;
        }

        private static string BuildVariantDirectorySuffix(DaTerrainData data)
        {
            if (data == null || data.Map == null || data.Map.Segments == null || data.Map.Segments.Count == 0)
            {
                return string.Empty;
            }

            List<string> parts = new List<string>();
            foreach (DaSegmentData segment in data.Map.Segments)
            {
                if (segment == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(segment.NormalizedVariantName))
                {
                    continue;
                }

                if (string.Equals(segment.NormalizedVariantName, "DirectSegmentRoot", StringComparison.Ordinal))
                {
                    continue;
                }

                string segmentShort = string.IsNullOrWhiteSpace(segment.SegmentName)
                    ? ("segment-" + segment.LevelSlot)
                    : GetSegmentDirectoryLabel(segment.SegmentName);
                string part = string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "L{0}-{1}-{2}",
                    segment.LevelSlot,
                    segmentShort,
                    segment.NormalizedVariantName);
                parts.Add(SanitizeFileName(part));
            }

            return string.Join("__", parts.ToArray());
        }

        private static string BuildMapDirectoryBase(DaTerrainData data)
        {
            if (data == null || data.Map == null || data.Map.Segments == null || data.Map.Segments.Count == 0)
            {
                return "unknown-map";
            }

            List<string> parts = new List<string>();
            foreach (DaSegmentData segment in data.Map.Segments)
            {
                if (segment == null)
                {
                    continue;
                }

                parts.Add(GetSegmentDirectoryLabel(segment.SegmentName));
            }

            return parts.Count > 0
                ? string.Join("__", parts.ToArray())
                : "unknown-map";
        }

        private static string GetSegmentDirectoryLabel(string segmentName)
        {
            if (string.IsNullOrWhiteSpace(segmentName))
            {
                return "segment";
            }

            switch (segmentName)
            {
                case "Beach_Segment":
                    return "Beach";
                case "Jungle_Segment":
                    return "Jungle";
                case "Roots Segment":
                    return "Roots";
                case "Snow_Segment":
                    return "Snow";
                case "Desert_Segment":
                    return "Desert";
                case "Caldera_Segment":
                    return "Caldera";
                case "Volcano_Segment":
                    return "Volcano";
                default:
                    return SanitizeFileName(segmentName.Replace("_Segment", string.Empty).Replace(" Segment", string.Empty).Replace(" ", string.Empty));
            }
        }

        private static string ShortenFolderName(string folderName, string uniqueKey)
        {
            if (string.IsNullOrWhiteSpace(folderName))
            {
                return "unknown-map";
            }

            const int maxFolderNameLength = 72;
            if (folderName.Length <= maxFolderNameLength)
            {
                return folderName;
            }

            string hash = ComputeShortHash(uniqueKey);
            int keepLength = Math.Max(16, maxFolderNameLength - hash.Length - 2);
            string prefix = folderName.Substring(0, keepLength).TrimEnd('_', '-');
            return prefix + "__" + hash;
        }

        private static string ComputeShortHash(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            using (SHA1 sha1 = SHA1.Create())
            {
                byte[] hash = sha1.ComputeHash(bytes);
                StringBuilder builder = new StringBuilder(8);
                for (int index = 0; index < 4; index++)
                {
                    builder.Append(hash[index].ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }

        private static DaCustomBlankRemainingDiagnostic BuildCustomBlankRemaining(DaTerrainData data, DaSegmentData segment)
        {
            DaCustomBlankRemainingDiagnostic diagnostic = new DaCustomBlankRemainingDiagnostic
            {
                MapKey = data != null && data.Map != null ? data.Map.MapKey : null,
                SegmentName = segment.SegmentName,
                EditMode = segment.EditMode.ToString()
            };

            HashSet<GameObject> seen = new HashSet<GameObject>();
            foreach (Transform root in segment.SourceRoots ?? Enumerable.Empty<Transform>())
            {
                if (root == null)
                {
                    continue;
                }

                diagnostic.SourceRoots.Add(GetHierarchyPath(root));
                Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
                for (int index = 0; index < renderers.Length; index++)
                {
                    Renderer renderer = renderers[index];
                    if (renderer != null)
                    {
                        AddRemainingCandidate(diagnostic, seen, root, renderer.gameObject);
                    }
                }

                Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
                for (int index = 0; index < colliders.Length; index++)
                {
                    Collider collider = colliders[index];
                    if (collider != null)
                    {
                        AddRemainingCandidate(diagnostic, seen, root, collider.gameObject);
                    }
                }

                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                for (int index = 0; index < transforms.Length; index++)
                {
                    Transform transform = transforms[index];
                    if (transform != null && IsCustomBlankSuspicious(transform))
                    {
                        AddRemainingCandidate(diagnostic, seen, root, transform.gameObject);
                    }
                }
            }

            diagnostic.Candidates.Sort((left, right) => string.Compare(left.Path, right.Path, StringComparison.OrdinalIgnoreCase));
            diagnostic.CandidateCount = diagnostic.Candidates.Count;
            diagnostic.RendererObjectCount = diagnostic.Candidates.Count(candidate => candidate.RendererCount > 0);
            diagnostic.ColliderObjectCount = diagnostic.Candidates.Count(candidate => candidate.ColliderCount > 0);
            return diagnostic;
        }

        private static void AddRemainingCandidate(
            DaCustomBlankRemainingDiagnostic diagnostic,
            HashSet<GameObject> seen,
            Transform root,
            GameObject gameObject)
        {
            if (gameObject == null || seen.Contains(gameObject))
            {
                return;
            }

            seen.Add(gameObject);
            Transform transform = gameObject.transform;
            DaCustomBlankRemainingCandidate candidate = new DaCustomBlankRemainingCandidate
            {
                Name = gameObject.name,
                Path = GetHierarchyPath(transform),
                ParentPath = transform.parent != null ? GetHierarchyPath(transform.parent) : null,
                RootPath = root != null ? GetHierarchyPath(root) : null,
                ActiveSelf = gameObject.activeSelf,
                ActiveInHierarchy = gameObject.activeInHierarchy,
                Layer = gameObject.layer,
                Tag = SafeTag(gameObject),
                LocalPosition = FormatVector(transform.localPosition),
                WorldPosition = FormatVector(transform.position),
                RendererCount = gameObject.GetComponents<Renderer>().Length,
                ChildRendererCount = gameObject.GetComponentsInChildren<Renderer>(true).Length,
                ColliderCount = gameObject.GetComponents<Collider>().Length,
                ChildColliderCount = gameObject.GetComponentsInChildren<Collider>(true).Length,
                HasLevelGenStep = gameObject.GetComponent<LevelGenStep>() != null,
                HasPropGrouper = gameObject.GetComponent<PropGrouper>() != null,
                AncestorLevelGenStep = GetAncestorName<LevelGenStep>(transform),
                AncestorPropGrouper = GetAncestorName<PropGrouper>(transform)
            };

            Component[] components = gameObject.GetComponents<Component>();
            for (int index = 0; index < components.Length; index++)
            {
                Component component = components[index];
                candidate.Components.Add(component != null ? component.GetType().Name : "<missing>");
            }

            diagnostic.Candidates.Add(candidate);
        }

        private static bool IsCustomBlankSuspicious(Transform transform)
        {
            if (transform == null)
            {
                return false;
            }

            if (ContainsSuspiciousText(transform.name) || ContainsSuspiciousText(GetHierarchyPath(transform)))
            {
                return true;
            }

            Component[] components = transform.GetComponents<Component>();
            for (int index = 0; index < components.Length; index++)
            {
                Component component = components[index];
                if (component != null && ContainsSuspiciousText(component.GetType().Name))
                {
                    return true;
                }
            }

            Renderer renderer = transform.GetComponent<Renderer>();
            if (renderer == null || renderer.sharedMaterials == null)
            {
                return false;
            }

            Material[] materials = renderer.sharedMaterials;
            for (int index = 0; index < materials.Length; index++)
            {
                Material material = materials[index];
                if (material != null && ContainsSuspiciousText(material.name))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsSuspiciousText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return value.IndexOf("Capy", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("Capybara", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("M_Capybara", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("卡皮", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("水豚", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            Stack<string> names = new Stack<string>();
            Transform current = transform;
            while (current != null)
            {
                names.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", names.ToArray());
        }

        private static string GetAncestorName<T>(Transform transform) where T : Component
        {
            Transform current = transform != null ? transform.parent : null;
            while (current != null)
            {
                T component = current.GetComponent<T>();
                if (component != null)
                {
                    return current.name;
                }

                current = current.parent;
            }

            return null;
        }

        private static string FormatVector(Vector3 value)
        {
            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "({0:0.###}, {1:0.###}, {2:0.###})",
                value.x,
                value.y,
                value.z);
        }

        private static string SafeTag(GameObject gameObject)
        {
            try
            {
                return gameObject.tag;
            }
            catch (UnityException)
            {
                return null;
            }
        }

        private static string SanitizeFileName(string value)
        {
            string safe = string.IsNullOrWhiteSpace(value) ? "segment" : value;
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                safe = safe.Replace(invalid, '_');
            }

            return safe.Replace(' ', '-');
        }

        private static void AddConstraints(List<string> constraintTypes, List<string> properties, List<DaConstraintData> constraints)
        {
            foreach (DaConstraintData constraint in constraints ?? Enumerable.Empty<DaConstraintData>())
            {
                AddName(constraintTypes, constraint.Type);
                AddProperties(properties, constraint.Properties);
            }
        }

        private static void AddProperties(List<string> target, List<DaPropertyData> properties)
        {
            foreach (DaPropertyData property in properties ?? Enumerable.Empty<DaPropertyData>())
            {
                AddName(target, property.Name);
            }
        }

        private static void AddName(List<string> target, string value)
        {
            if (!string.IsNullOrWhiteSpace(value) && !target.Contains(value))
            {
                target.Add(value);
            }
        }

        private static void Sort(List<string> values)
        {
            values.Sort(StringComparer.OrdinalIgnoreCase);
        }

        private static void EnsureInitialized()
        {
            if (string.IsNullOrWhiteSpace(DiagnosticDirectoryPath))
            {
                throw new InvalidOperationException("DaDiagnosticService has not been initialized.");
            }
        }

        private sealed class DaNameMap
        {
            public List<string> Segments { get; } = new List<string>();
            public List<string> Groupers { get; } = new List<string>();
            public List<string> Steps { get; } = new List<string>();
            public List<string> StepTypes { get; } = new List<string>();
            public List<string> Properties { get; } = new List<string>();
            public List<string> ConstraintTypes { get; } = new List<string>();
        }

        private sealed class DaCustomBlankRemainingDiagnostic
        {
            public string MapKey { get; set; }
            public string SegmentName { get; set; }
            public string EditMode { get; set; }
            public int CandidateCount { get; set; }
            public int RendererObjectCount { get; set; }
            public int ColliderObjectCount { get; set; }
            public List<string> SourceRoots { get; } = new List<string>();
            public List<DaCustomBlankRemainingCandidate> Candidates { get; } = new List<DaCustomBlankRemainingCandidate>();
        }

        private sealed class DaCustomBlankRemainingCandidate
        {
            public string Name { get; set; }
            public string Path { get; set; }
            public string ParentPath { get; set; }
            public string RootPath { get; set; }
            public bool ActiveSelf { get; set; }
            public bool ActiveInHierarchy { get; set; }
            public int Layer { get; set; }
            public string Tag { get; set; }
            public string LocalPosition { get; set; }
            public string WorldPosition { get; set; }
            public int RendererCount { get; set; }
            public int ChildRendererCount { get; set; }
            public int ColliderCount { get; set; }
            public int ChildColliderCount { get; set; }
            public bool HasLevelGenStep { get; set; }
            public bool HasPropGrouper { get; set; }
            public string AncestorLevelGenStep { get; set; }
            public string AncestorPropGrouper { get; set; }
            public List<string> Components { get; } = new List<string>();
        }
    }
}


