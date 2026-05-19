using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using DreamyAscent.Data;
using DreamyAscent.Helpers;
using UnityEngine;

namespace DreamyAscent.Services
{
    internal static class DaObjectCatalogDiagnosticService
    {
        private const BindingFlags InstanceFieldFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly JsonSerializerSettings s_jsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented
        };

        private static readonly HashSet<string> s_defaultPropertyNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "nrOfSpawns",
            "randomSpawns",
            "minSpawnCount",
            "minMaxSpawn",
            "scaleMinMax",
            "overallSpawnChance",
            "chanceToUseSpawner",
            "area",
            "radius",
            "circleSize",
            "rayCastSpawn",
            "raycastPosition",
            "rayNearCutoff",
            "rayDirectionOffset",
            "layerType",
            "syncTransforms"
        };

        public static void WriteObjectCatalog(DaTerrainData data, string mapDirectory)
        {
            if (string.IsNullOrWhiteSpace(mapDirectory))
            {
                return;
            }

            try
            {
                DaObjectCatalog catalog = BuildCatalog(data);
                string path = System.IO.Path.Combine(mapDirectory, "ObjectCatalog.json");
                System.IO.File.WriteAllText(path, JsonConvert.SerializeObject(catalog, s_jsonSettings));
                DaLog.Info("Object catalog diagnostic written: " + path);
            }
            catch (Exception ex)
            {
                DaLog.Warn("Failed to write object catalog diagnostic: " + ex.Message);
            }
        }

        public static DaObjectCatalog BuildCatalog(DaTerrainData data)
        {
            DaObjectCatalog catalog = new DaObjectCatalog();
            if (data == null || data.Map == null || data.Map.Segments == null)
            {
                return catalog;
            }

            catalog.MapKey = data.Map.MapKey;
            HashSet<string> itemIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> materialIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (DaSegmentData segment in data.Map.Segments)
            {
                if (segment == null || segment.Groupers == null)
                {
                    continue;
                }

                DaCatalogSegment segmentCatalog = GetOrCreateSegment(catalog, segment);

                foreach (DaPropGrouperData grouper in segment.Groupers)
                {
                    if (grouper == null || grouper.Steps == null)
                    {
                        continue;
                    }

                    foreach (DaLevelGenStepData step in grouper.Steps)
                    {
                        if (step == null)
                        {
                            continue;
                        }

                        CollectOwner(catalog, segmentCatalog, itemIds, materialIds, segment, grouper, step, "step", step.StepType, step.SourceObject);
                        CollectConstraintOwners(catalog, segmentCatalog, itemIds, materialIds, segment, grouper, step, "modifiers", step.Modifiers);
                        CollectConstraintOwners(catalog, segmentCatalog, itemIds, materialIds, segment, grouper, step, "constraints", step.Constraints);
                        CollectConstraintOwners(catalog, segmentCatalog, itemIds, materialIds, segment, grouper, step, "postConstraints", step.PostConstraints);
                    }
                }
            }

            catalog.SegmentCount = catalog.Segments.Count;
            catalog.ItemCount = catalog.Items.Count;
            catalog.MaterialCount = catalog.Materials.Count;
            return catalog;
        }

        private static void CollectConstraintOwners(
            DaObjectCatalog catalog,
            DaCatalogSegment segmentCatalog,
            HashSet<string> itemIds,
            HashSet<string> materialIds,
            DaSegmentData segment,
            DaPropGrouperData grouper,
            DaLevelGenStepData step,
            string bucketName,
            List<DaConstraintData> constraints)
        {
            if (constraints == null)
            {
                return;
            }

            for (int index = 0; index < constraints.Count; index++)
            {
                DaConstraintData constraint = constraints[index];
                if (constraint == null)
                {
                    continue;
                }

                CollectOwner(catalog, segmentCatalog, itemIds, materialIds, segment, grouper, step, bucketName + "[" + index + "]", constraint.Type, constraint.SourceObject);
            }
        }

        private static void CollectOwner(
            DaObjectCatalog catalog,
            DaCatalogSegment segmentCatalog,
            HashSet<string> itemIds,
            HashSet<string> materialIds,
            DaSegmentData segment,
            DaPropGrouperData grouper,
            DaLevelGenStepData step,
            string ownerKind,
            string ownerType,
            object owner)
        {
            if (catalog == null || segmentCatalog == null || owner == null)
            {
                return;
            }

            Type ownerRuntimeType = owner.GetType();
            FieldInfo[] fields = ownerRuntimeType.GetFields(InstanceFieldFlags);
            Array.Sort(fields, (left, right) => string.CompareOrdinal(left.Name, right.Name));

            foreach (FieldInfo field in fields)
            {
                if (field == null || field.IsStatic)
                {
                    continue;
                }

                object value;
                try
                {
                    value = field.GetValue(owner);
                }
                catch
                {
                    continue;
                }

                if (value == null)
                {
                    continue;
                }

                CollectValue(catalog, segmentCatalog, itemIds, materialIds, segment, grouper, step, ownerKind, ownerType, ownerRuntimeType.FullName, field.Name, value);
            }
        }

        private static void CollectValue(
            DaObjectCatalog catalog,
            DaCatalogSegment segmentCatalog,
            HashSet<string> itemIds,
            HashSet<string> materialIds,
            DaSegmentData segment,
            DaPropGrouperData grouper,
            DaLevelGenStepData step,
            string ownerKind,
            string ownerType,
            string ownerRuntimeType,
            string fieldName,
            object value)
        {
            UnityEngine.Object unityObject = value as UnityEngine.Object;
            if (unityObject != null)
            {
                AddCatalogEntry(catalog, segmentCatalog, itemIds, materialIds, segment, grouper, step, ownerKind, ownerType, ownerRuntimeType, fieldName, unityObject, -1);
                return;
            }

            IEnumerable enumerable = value as IEnumerable;
            if (enumerable == null || value is string)
            {
                return;
            }

            int index = 0;
            foreach (object item in enumerable)
            {
                UnityEngine.Object itemObject = item as UnityEngine.Object;
                if (itemObject != null)
                {
                    AddCatalogEntry(catalog, segmentCatalog, itemIds, materialIds, segment, grouper, step, ownerKind, ownerType, ownerRuntimeType, fieldName, itemObject, index);
                }

                index++;
            }
        }

        private static void AddCatalogEntry(
            DaObjectCatalog catalog,
            DaCatalogSegment segmentCatalog,
            HashSet<string> itemIds,
            HashSet<string> materialIds,
            DaSegmentData segment,
            DaPropGrouperData grouper,
            DaLevelGenStepData step,
            string ownerKind,
            string ownerType,
            string ownerRuntimeType,
            string fieldName,
            UnityEngine.Object target,
            int collectionIndex)
        {
            if (target == null)
            {
                return;
            }

            DaCatalogTargetInfo targetInfo = DescribeTarget(target);
            string role = ClassifyRole(ownerType, fieldName, target, targetInfo);
            if (string.IsNullOrWhiteSpace(role))
            {
                return;
            }

            string field = collectionIndex >= 0 ? fieldName + "[" + collectionIndex + "]" : fieldName;
            DaCatalogSource source = new DaCatalogSource
            {
                LevelSlot = segment != null ? segment.LevelSlot : -1,
                Segment = segment != null ? segment.SegmentName : string.Empty,
                SegmentPath = segment != null ? segment.SegmentPath : string.Empty,
                Grouper = grouper != null ? grouper.GrouperName : string.Empty,
                GrouperPath = grouper != null ? grouper.HierarchyPath : string.Empty,
                Step = step != null ? step.StepName : string.Empty,
                StepType = step != null ? step.StepType : string.Empty,
                StepPath = step != null ? step.HierarchyPath : string.Empty,
                VariantSelectionType = segment != null ? segment.VariantSelectionType : string.Empty,
                NormalizedVariantName = segment != null ? segment.NormalizedVariantName : string.Empty,
                OwnerKind = ownerKind,
                OwnerType = ownerType,
                OwnerRuntimeType = ownerRuntimeType,
                Field = field
            };

            if (segment != null && segment.ActiveVariantNames != null)
            {
                source.ActiveVariantNames.AddRange(segment.ActiveVariantNames);
            }

            Material material = target as Material;
            if (material != null)
            {
                AddMaterialEntry(catalog, segmentCatalog, materialIds, role, source, targetInfo);
                return;
            }

            AddItemEntry(catalog, segmentCatalog, itemIds, role, source, targetInfo, step);
        }

        private static void AddItemEntry(
            DaObjectCatalog catalog,
            DaCatalogSegment segmentCatalog,
            HashSet<string> itemIds,
            string role,
            DaCatalogSource source,
            DaCatalogTargetInfo targetInfo,
            DaLevelGenStepData step)
        {
            string id = BuildCatalogId("item", source.Segment, role, targetInfo.StableKey);
            if (!itemIds.Add(id))
            {
                AddSegmentItemRef(segmentCatalog, id);
                return;
            }

            DaCatalogItem item = new DaCatalogItem
            {
                Id = id,
                Segment = source.Segment,
                Kind = role == "single-item-prefab" ? "single-item-prefab" : "prefab",
                Role = role,
                Name = targetInfo.Name,
                DisplayName = DaLocalization.TranslateOrOriginal(targetInfo.Name),
                StableKey = targetInfo.StableKey,
                ObjectType = targetInfo.ObjectType,
                GameObjectPath = targetInfo.GameObjectPath,
                Scene = targetInfo.Scene,
                HasChildGeneration = targetInfo.ChildLevelGenStepCount > 0,
                HasSingleItemSpawner = targetInfo.ChildSingleItemSpawnerCount > 0,
                HasPhotonView = targetInfo.HasPhotonView,
                ChildLevelGenStepCount = targetInfo.ChildLevelGenStepCount,
                ChildSingleItemSpawnerCount = targetInfo.ChildSingleItemSpawnerCount,
                RendererCount = targetInfo.ChildRendererCount,
                Source = source,
                Defaults = BuildDefaults(step)
            };

            item.Components.AddRange(targetInfo.Components);
            item.RendererMaterials.AddRange(targetInfo.RendererMaterials);
            catalog.Items.Add(item);
            AddSegmentItemRef(segmentCatalog, id);
            AddCount(catalog.ItemRoleCounts, role);
        }

        private static void AddMaterialEntry(
            DaObjectCatalog catalog,
            DaCatalogSegment segmentCatalog,
            HashSet<string> materialIds,
            string role,
            DaCatalogSource source,
            DaCatalogTargetInfo targetInfo)
        {
            string id = BuildCatalogId("material", source.Segment, role, targetInfo.StableKey);
            if (!materialIds.Add(id))
            {
                AddSegmentMaterialRef(segmentCatalog, id);
                return;
            }

            DaCatalogMaterial material = new DaCatalogMaterial
            {
                Id = id,
                Segment = source.Segment,
                Role = role,
                Name = targetInfo.Name,
                DisplayName = DaLocalization.TranslateOrOriginal(targetInfo.Name),
                StableKey = targetInfo.StableKey,
                ObjectType = targetInfo.ObjectType,
                Shader = targetInfo.MaterialShader,
                Color = targetInfo.MaterialColor,
                MainTexture = targetInfo.MaterialMainTexture,
                Source = source
            };

            catalog.Materials.Add(material);
            AddSegmentMaterialRef(segmentCatalog, id);
            AddCount(catalog.MaterialRoleCounts, role);
        }

        private static DaCatalogDefaults BuildDefaults(DaLevelGenStepData step)
        {
            DaCatalogDefaults defaults = new DaCatalogDefaults();
            if (step == null || step.Properties == null)
            {
                return defaults;
            }

            for (int index = 0; index < step.Properties.Count; index++)
            {
                DaPropertyData property = step.Properties[index];
                if (property == null || string.IsNullOrWhiteSpace(property.Name) || !s_defaultPropertyNames.Contains(property.Name))
                {
                    continue;
                }

                defaults.Properties[property.Name] = DaRuntimeEditService.FormatForEdit(property.Value);
            }

            return defaults;
        }

        private static string ClassifyRole(string ownerType, string fieldName, UnityEngine.Object target, DaCatalogTargetInfo targetInfo)
        {
            bool isGameObjectReference = target is GameObject || target is Component;
            bool isMaterialReference = target is Material;

            if (isGameObjectReference && string.Equals(fieldName, "props", StringComparison.Ordinal))
            {
                return "step-prop-prefab";
            }

            if (isGameObjectReference &&
                string.Equals(ownerType, "PSM_SingleItemSpawner", StringComparison.Ordinal) &&
                string.Equals(fieldName, "objToSpawn", StringComparison.Ordinal))
            {
                return "single-item-prefab";
            }

            if (isGameObjectReference && targetInfo != null && targetInfo.ChildLevelGenStepCount > 0)
            {
                return "parent-child-template";
            }

            if (!isMaterialReference)
            {
                return string.Empty;
            }

            if (string.Equals(ownerType, "PSM_SetMaterial", StringComparison.Ordinal) && string.Equals(fieldName, "mat", StringComparison.Ordinal))
            {
                return "set-material";
            }

            if (string.Equals(ownerType, "PSM_SetMaterialOnChild", StringComparison.Ordinal) && string.Equals(fieldName, "mat", StringComparison.Ordinal))
            {
                return "set-child-material";
            }

            if (string.Equals(ownerType, "PSM_SetRandomMaterial", StringComparison.Ordinal) && string.Equals(fieldName, "mats", StringComparison.Ordinal))
            {
                return "random-material";
            }

            if (string.Equals(ownerType, "PSM_ReplaceMaterial", StringComparison.Ordinal))
            {
                if (string.Equals(fieldName, "replaceThis", StringComparison.Ordinal))
                {
                    return "replace-material-from";
                }

                if (string.Equals(fieldName, "withThis", StringComparison.Ordinal))
                {
                    return "replace-material-to";
                }
            }

            if (string.Equals(ownerType, "PSC_RequiredMaterial", StringComparison.Ordinal) && string.Equals(fieldName, "RequiredMaterial", StringComparison.Ordinal))
            {
                return "required-material";
            }

            if (string.Equals(ownerType, "PSC_BannedMaterial", StringComparison.Ordinal) && string.Equals(fieldName, "bannedMaterial", StringComparison.Ordinal))
            {
                return "banned-material";
            }

            return "material-reference";
        }

        private static DaCatalogTargetInfo DescribeTarget(UnityEngine.Object target)
        {
            DaCatalogTargetInfo info = new DaCatalogTargetInfo
            {
                Name = target != null ? target.name : string.Empty,
                ObjectType = target != null ? target.GetType().FullName : string.Empty
            };

            GameObject go = null;
            Component component = target as Component;
            if (component != null)
            {
                go = component.gameObject;
            }

            GameObject gameObject = target as GameObject;
            if (gameObject != null)
            {
                go = gameObject;
            }

            Material material = target as Material;
            if (material != null)
            {
                FillMaterialInfo(info, material);
            }

            if (go != null)
            {
                FillGameObjectInfo(info, go);
            }

            info.StableKey = BuildStableKey(info);
            return info;
        }

        private static void FillGameObjectInfo(DaCatalogTargetInfo info, GameObject go)
        {
            info.GameObjectPath = GetHierarchyPath(go.transform);
            info.Scene = go.scene.IsValid() ? go.scene.name : string.Empty;
            info.ChildLevelGenStepCount = go.GetComponentsInChildren<LevelGenStep>(true).Length;
            info.ChildSingleItemSpawnerCount = CountComponentsByTypeName(go, "SingleItemSpawner");
            info.ChildRendererCount = go.GetComponentsInChildren<Renderer>(true).Length;
            info.HasPhotonView = CountComponentsByTypeName(go, "PhotonView") > 0;

            Component[] components = go.GetComponents<Component>();
            for (int index = 0; index < components.Length && index < 40; index++)
            {
                Component component = components[index];
                if (component != null)
                {
                    info.Components.Add(component.GetType().FullName);
                }
            }

            Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < renderers.Length && index < 24; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null)
                {
                    continue;
                }

                Material[] materials = renderer.sharedMaterials;
                if (materials == null)
                {
                    continue;
                }

                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material material = materials[materialIndex];
                    if (material != null && names.Add(material.name))
                    {
                        info.RendererMaterials.Add(material.name);
                    }
                }
            }
        }

        private static void FillMaterialInfo(DaCatalogTargetInfo info, Material material)
        {
            info.MaterialShader = material.shader != null ? material.shader.name : string.Empty;
            if (material.HasProperty("_Color"))
            {
                try
                {
                    Color color = material.color;
                    info.MaterialColor = string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "{0:0.###},{1:0.###},{2:0.###},{3:0.###}",
                        color.r,
                        color.g,
                        color.b,
                        color.a);
                }
                catch { }
            }

            if (material.mainTexture != null)
            {
                info.MaterialMainTexture = material.mainTexture.name;
            }
        }

        private static DaCatalogSegment GetOrCreateSegment(DaObjectCatalog catalog, DaSegmentData segmentData)
        {
            string name = segmentData != null ? segmentData.SegmentName ?? string.Empty : string.Empty;
            for (int index = 0; index < catalog.Segments.Count; index++)
            {
                if (string.Equals(catalog.Segments[index].Name, name, StringComparison.Ordinal))
                {
                    return catalog.Segments[index];
                }
            }

            DaCatalogSegment segment = new DaCatalogSegment
            {
                Name = name,
                LevelSlot = segmentData != null ? segmentData.LevelSlot : -1,
                SegmentPath = segmentData != null ? segmentData.SegmentPath : string.Empty,
                VariantSelectionType = segmentData != null ? segmentData.VariantSelectionType : string.Empty,
                NormalizedVariantName = segmentData != null ? segmentData.NormalizedVariantName : string.Empty,
                DisplayName = DaLocalization.Translate(name, "catalog-segment")
            };

            if (segmentData != null)
            {
                if (segmentData.ActiveVariantNames != null)
                {
                    segment.ActiveVariantNames.AddRange(segmentData.ActiveVariantNames);
                }

                if (segmentData.ActiveVariantPaths != null)
                {
                    segment.ActiveVariantPaths.AddRange(segmentData.ActiveVariantPaths);
                }

                if (segmentData.RootPaths != null)
                {
                    segment.RootPaths.AddRange(segmentData.RootPaths);
                }
            }

            catalog.Segments.Add(segment);
            return segment;
        }

        private static void AddSegmentItemRef(DaCatalogSegment segment, string id)
        {
            if (segment != null && !string.IsNullOrWhiteSpace(id) && !segment.ItemIds.Contains(id))
            {
                segment.ItemIds.Add(id);
            }
        }

        private static void AddSegmentMaterialRef(DaCatalogSegment segment, string id)
        {
            if (segment != null && !string.IsNullOrWhiteSpace(id) && !segment.MaterialIds.Contains(id))
            {
                segment.MaterialIds.Add(id);
            }
        }

        private static void AddCount(Dictionary<string, int> counts, string key)
        {
            if (counts == null || string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            if (counts.ContainsKey(key))
            {
                counts[key]++;
            }
            else
            {
                counts[key] = 1;
            }
        }

        private static int CountComponentsByTypeName(GameObject root, string typeName)
        {
            int count = 0;
            if (root == null || string.IsNullOrWhiteSpace(typeName))
            {
                return count;
            }

            Component[] components = root.GetComponentsInChildren<Component>(true);
            for (int index = 0; index < components.Length; index++)
            {
                Component component = components[index];
                Type type = component != null ? component.GetType() : null;
                if (type != null && string.Equals(type.Name, typeName, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            List<string> nodes = new List<string>();
            Transform current = transform;
            while (current != null)
            {
                nodes.Add(current.name);
                current = current.parent;
            }

            nodes.Reverse();
            return string.Join("/", nodes.ToArray());
        }

        private static string BuildStableKey(DaCatalogTargetInfo info)
        {
            if (info == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(info.GameObjectPath))
            {
                return info.ObjectType + "|" + info.GameObjectPath;
            }

            return info.ObjectType + "|" + info.Name;
        }

        private static string BuildCatalogId(string prefix, string segment, string role, string stableKey)
        {
            return SanitizeId(prefix) + ":" + SanitizeId(segment) + ":" + SanitizeId(role) + ":" + StableHash(stableKey);
        }

        private static string StableHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261;
                string text = value ?? string.Empty;
                for (int index = 0; index < text.Length; index++)
                {
                    hash ^= text[index];
                    hash *= 16777619;
                }

                return hash.ToString("x8", System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        private static string SanitizeId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "unknown";
            }

            char[] chars = value.Trim().ToLowerInvariant().ToCharArray();
            for (int index = 0; index < chars.Length; index++)
            {
                char c = chars[index];
                if (!(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9'))
                {
                    chars[index] = '-';
                }
            }

            string result = new string(chars).Trim('-');
            while (result.Contains("--"))
            {
                result = result.Replace("--", "-");
            }

            return string.IsNullOrWhiteSpace(result) ? "unknown" : result;
        }

        private sealed class DaCatalogTargetInfo
        {
            public string StableKey { get; set; }
            public string Name { get; set; }
            public string ObjectType { get; set; }
            public string GameObjectPath { get; set; }
            public string Scene { get; set; }
            public int ChildLevelGenStepCount { get; set; }
            public int ChildSingleItemSpawnerCount { get; set; }
            public int ChildRendererCount { get; set; }
            public bool HasPhotonView { get; set; }
            public List<string> Components { get; } = new List<string>();
            public List<string> RendererMaterials { get; } = new List<string>();
            public string MaterialShader { get; set; }
            public string MaterialColor { get; set; }
            public string MaterialMainTexture { get; set; }
        }
    }
}


