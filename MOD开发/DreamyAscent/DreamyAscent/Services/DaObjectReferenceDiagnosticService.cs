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
    internal static class DaObjectReferenceDiagnosticService
    {
        private const BindingFlags InstanceFieldFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly JsonSerializerSettings s_jsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented
        };

        public static void WriteObjectReferenceMap(DaTerrainData data, string mapDirectory)
        {
            if (string.IsNullOrWhiteSpace(mapDirectory))
            {
                return;
            }

            try
            {
                DaObjectReferenceMap result = Build(data);
                string path = System.IO.Path.Combine(mapDirectory, "ObjectReferenceMap.json");
                System.IO.File.WriteAllText(path, JsonConvert.SerializeObject(result, s_jsonSettings));
                DaLog.Info("Object reference diagnostic written: " + path);
            }
            catch (Exception ex)
            {
                DaLog.Warn("Failed to write object reference diagnostic: " + ex.Message);
            }
        }

        private static DaObjectReferenceMap Build(DaTerrainData data)
        {
            DaObjectReferenceMap result = new DaObjectReferenceMap();
            if (data == null || data.Map == null || data.Map.Segments == null)
            {
                return result;
            }

            result.MapKey = data.Map.MapKey;

            foreach (DaSegmentData segment in data.Map.Segments)
            {
                if (segment == null || segment.Groupers == null)
                {
                    continue;
                }

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

                        CollectOwner(result, segment, grouper, step, "step", step.StepType, step.SourceObject);
                        CollectConstraintOwners(result, segment, grouper, step, "modifiers", step.Modifiers);
                        CollectConstraintOwners(result, segment, grouper, step, "constraints", step.Constraints);
                        CollectConstraintOwners(result, segment, grouper, step, "postConstraints", step.PostConstraints);
                    }
                }
            }

            result.ReferenceCount = result.References.Count;
            result.UniqueObjectCount = result.UniqueObjects.Count;
            return result;
        }

        private static void CollectConstraintOwners(
            DaObjectReferenceMap result,
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

                CollectOwner(result, segment, grouper, step, bucketName + "[" + index + "]", constraint.Type, constraint.SourceObject);
            }
        }

        private static void CollectOwner(
            DaObjectReferenceMap result,
            DaSegmentData segment,
            DaPropGrouperData grouper,
            DaLevelGenStepData step,
            string ownerKind,
            string ownerType,
            object owner)
        {
            if (result == null || owner == null)
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

                CollectValue(result, segment, grouper, step, ownerKind, ownerType, ownerRuntimeType.FullName, field.Name, value);
            }
        }

        private static void CollectValue(
            DaObjectReferenceMap result,
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
                AddReference(result, segment, grouper, step, ownerKind, ownerType, ownerRuntimeType, fieldName, unityObject, -1);
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
                    AddReference(result, segment, grouper, step, ownerKind, ownerType, ownerRuntimeType, fieldName, itemObject, index);
                }

                index++;
            }
        }

        private static void AddReference(
            DaObjectReferenceMap result,
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

            DaObjectReferenceEntry entry = new DaObjectReferenceEntry
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
                Field = collectionIndex >= 0 ? fieldName + "[" + collectionIndex + "]" : fieldName,
                Target = DescribeObject(target)
            };

            if (segment != null && segment.ActiveVariantNames != null)
            {
                entry.ActiveVariantNames.AddRange(segment.ActiveVariantNames);
            }

            FillReferenceClassification(entry, fieldName, target);
            result.References.Add(entry);
            AddCount(result.RoleCounts, entry.Role);
            if (entry.CatalogCandidate)
            {
                result.CatalogCandidateCount++;
                AddCount(result.CandidateReasonCounts, entry.CandidateReason);
            }

            if (entry.Target != null)
            {
                AddCount(result.ObjectTypeCounts, entry.Target.ObjectType);
                if (entry.Target.ChildLevelGenStepCount > 0)
                {
                    result.ParentChildCandidateCount++;
                }

                if (entry.Target.ChildSingleItemSpawnerCount > 0)
                {
                    result.SingleItemSpawnerShellCount++;
                }
            }

            string key = entry.Target != null ? entry.Target.StableKey : null;
            if (!string.IsNullOrWhiteSpace(key) && !result.UniqueObjects.ContainsKey(key))
            {
                result.UniqueObjects[key] = entry.Target;
            }
        }

        private static void FillReferenceClassification(DaObjectReferenceEntry entry, string baseFieldName, UnityEngine.Object target)
        {
            if (entry == null)
            {
                return;
            }

            Type targetType = target != null ? target.GetType() : null;
            bool isGameObjectReference = target is GameObject || target is Component;
            bool isMaterialReference = target is Material;
            string ownerType = entry.OwnerType ?? string.Empty;
            string fieldName = baseFieldName ?? string.Empty;

            entry.Role = isMaterialReference ? "material-reference" : "unity-object-reference";

            if (isGameObjectReference && string.Equals(fieldName, "props", StringComparison.Ordinal))
            {
                entry.Role = "step-prop-prefab";
                entry.CatalogCandidate = true;
                entry.CandidateReason = "step props prefab";
            }
            else if (isGameObjectReference && string.Equals(ownerType, "PSM_SingleItemSpawner", StringComparison.Ordinal) && string.Equals(fieldName, "objToSpawn", StringComparison.Ordinal))
            {
                entry.Role = "single-item-prefab";
                entry.CatalogCandidate = true;
                entry.CandidateReason = "single item prefab";
            }
            else if (isMaterialReference)
            {
                entry.CatalogCandidate = true;
                entry.CandidateReason = "material reference";
                entry.Role = ClassifyMaterialRole(ownerType, fieldName);
            }

            if (entry.Target != null && isGameObjectReference)
            {
                if (entry.Target.ChildLevelGenStepCount > 0)
                {
                    entry.HasChildGeneration = true;
                    if (!entry.CatalogCandidate)
                    {
                        entry.CatalogCandidate = true;
                        entry.CandidateReason = "contains child LevelGenStep";
                    }
                }

                if (entry.Target.ChildSingleItemSpawnerCount > 0)
                {
                    entry.HasSingleItemSpawner = true;
                }
            }

            entry.TargetRuntimeType = targetType != null ? targetType.FullName : string.Empty;
        }

        private static string ClassifyMaterialRole(string ownerType, string fieldName)
        {
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

        private static DaObjectInfo DescribeObject(UnityEngine.Object target)
        {
            DaObjectInfo info = new DaObjectInfo
            {
                Name = target != null ? target.name : string.Empty,
                ObjectType = target != null ? target.GetType().FullName : string.Empty,
                InstanceId = target != null ? target.GetInstanceID() : 0
            };

            GameObject go = null;
            Component component = target as Component;
            if (component != null)
            {
                go = component.gameObject;
                info.ComponentType = component.GetType().FullName;
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

        private static void FillGameObjectInfo(DaObjectInfo info, GameObject go)
        {
            info.GameObjectPath = GetHierarchyPath(go.transform);
            info.Scene = go.scene.IsValid() ? go.scene.name : string.Empty;
            info.ActiveSelf = go.activeSelf;
            info.Layer = go.layer;

            Component[] components = go.GetComponents<Component>();
            for (int index = 0; index < components.Length && index < 80; index++)
            {
                Component component = components[index];
                if (component != null)
                {
                    info.Components.Add(component.GetType().FullName);
                }
            }

            info.ChildCount = go.transform.childCount;
            info.ChildLevelGenStepCount = go.GetComponentsInChildren<LevelGenStep>(true).Length;
            info.ChildSingleItemSpawnerCount = CountComponentsByTypeName(go, "SingleItemSpawner");
            info.ChildRendererCount = go.GetComponentsInChildren<Renderer>(true).Length;
            info.HasPhotonView = CountComponentsByTypeName(go, "PhotonView") > 0;

            Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length && index < 24; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null)
                {
                    continue;
                }

                DaRendererInfo rendererInfo = new DaRendererInfo
                {
                    Path = GetHierarchyPath(renderer.transform),
                    RendererType = renderer.GetType().FullName,
                    Enabled = renderer.enabled
                };

                Material[] materials = renderer.sharedMaterials;
                if (materials != null)
                {
                    for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                    {
                        Material material = materials[materialIndex];
                        rendererInfo.Materials.Add(material != null ? material.name : string.Empty);
                    }
                }

                info.Renderers.Add(rendererInfo);
            }
        }

        private static void FillMaterialInfo(DaObjectInfo info, Material material)
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

        private static string BuildStableKey(DaObjectInfo info)
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

        private sealed class DaObjectReferenceMap
        {
            public string MapKey { get; set; }
            public int ReferenceCount { get; set; }
            public int UniqueObjectCount { get; set; }
            public int CatalogCandidateCount { get; set; }
            public int ParentChildCandidateCount { get; set; }
            public int SingleItemSpawnerShellCount { get; set; }
            public Dictionary<string, int> RoleCounts { get; } = new Dictionary<string, int>(StringComparer.Ordinal);
            public Dictionary<string, int> CandidateReasonCounts { get; } = new Dictionary<string, int>(StringComparer.Ordinal);
            public Dictionary<string, int> ObjectTypeCounts { get; } = new Dictionary<string, int>(StringComparer.Ordinal);
            public List<DaObjectReferenceEntry> References { get; } = new List<DaObjectReferenceEntry>();
            public Dictionary<string, DaObjectInfo> UniqueObjects { get; } = new Dictionary<string, DaObjectInfo>(StringComparer.Ordinal);
        }

        private sealed class DaObjectReferenceEntry
        {
            public int LevelSlot { get; set; }
            public string Segment { get; set; }
            public string SegmentPath { get; set; }
            public string Grouper { get; set; }
            public string GrouperPath { get; set; }
            public string Step { get; set; }
            public string StepType { get; set; }
            public string StepPath { get; set; }
            public string VariantSelectionType { get; set; }
            public string NormalizedVariantName { get; set; }
            public List<string> ActiveVariantNames { get; } = new List<string>();
            public string OwnerKind { get; set; }
            public string OwnerType { get; set; }
            public string OwnerRuntimeType { get; set; }
            public string Field { get; set; }
            public string Role { get; set; }
            public bool CatalogCandidate { get; set; }
            public string CandidateReason { get; set; }
            public bool HasChildGeneration { get; set; }
            public bool HasSingleItemSpawner { get; set; }
            public string TargetRuntimeType { get; set; }
            public DaObjectInfo Target { get; set; }
        }

        private sealed class DaObjectInfo
        {
            public string StableKey { get; set; }
            public string Name { get; set; }
            public string ObjectType { get; set; }
            public string ComponentType { get; set; }
            public int InstanceId { get; set; }
            public string GameObjectPath { get; set; }
            public string Scene { get; set; }
            public bool ActiveSelf { get; set; }
            public int Layer { get; set; }
            public List<string> Components { get; } = new List<string>();
            public int ChildCount { get; set; }
            public int ChildLevelGenStepCount { get; set; }
            public int ChildSingleItemSpawnerCount { get; set; }
            public int ChildRendererCount { get; set; }
            public bool HasPhotonView { get; set; }
            public List<DaRendererInfo> Renderers { get; } = new List<DaRendererInfo>();
            public string MaterialShader { get; set; }
            public string MaterialColor { get; set; }
            public string MaterialMainTexture { get; set; }
        }

        private sealed class DaRendererInfo
        {
            public string Path { get; set; }
            public string RendererType { get; set; }
            public bool Enabled { get; set; }
            public List<string> Materials { get; } = new List<string>();
        }
    }
}


