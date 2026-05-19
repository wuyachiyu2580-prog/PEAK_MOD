using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using DreamyAscent.Data;
using DreamyAscent.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace DreamyAscent.Services
{
    internal static class DaPlacementRuntimeService
    {
        private const string RuntimeRootName = "DreamyAscent Runtime";
        internal const int MaxRuleCount = 25;
        private static readonly string[] s_templateSpawnerComponents =
        {
            "Spawner",
            "BerryBush",
            "BerryVine",
            "GroundPlaceSpawner",
            "Luggage"
        };

        private static readonly JsonSerializerSettings s_jsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

        public static DaPlacementRuntimeResult RunPlacementRules(DaTerrainData data, DaSegmentData segment)
        {
            DaPlacementRuntimeResult result = new DaPlacementRuntimeResult
            {
                SegmentName = segment != null ? segment.SegmentName : string.Empty
            };

            if (segment == null)
            {
                result.Failures.Add("segment-missing");
                return result;
            }

            if (segment.SourceRoots == null || segment.SourceRoots.Count == 0)
            {
                DaTerrainExportService.TryBindSegmentRuntimeReferences(segment);
            }

            if (segment.PlacementRules == null || segment.PlacementRules.Count == 0)
            {
                result.Failures.Add("placement-rules-empty");
                WritePlacementDiagnostic(data, result);
                return result;
            }

            Bounds segmentBounds = GetSegmentBounds(segment);
            DaLog.Info("Running placement rules. segment=" + segment.SegmentName + ", rules=" + segment.PlacementRules.Count);

            for (int index = 0; index < segment.PlacementRules.Count; index++)
            {
                DaPlacementRuleData rule = segment.PlacementRules[index];
                if (rule == null)
                {
                    continue;
                }

                result.RuleCount++;
                if (!rule.Enabled)
                {
                    result.SkippedRules++;
                    AddRuleDiagnostic(result, rule, "skipped-disabled", 0, 0, null);
                    ClearRuleRuntimeObjects(segment, rule);
                    continue;
                }

                DaSubAreaData subArea = FindSubArea(segment, rule.TargetSubAreaId);
                if (subArea == null || !subArea.Enabled)
                {
                    result.SkippedRules++;
                    AddRuleDiagnostic(result, rule, "skipped-sub-area", 0, 0, null);
                    ClearRuleRuntimeObjects(segment, rule);
                    continue;
                }

                if (!TryFindTemplate(rule.RegistryId, out DaObjectRegistryTemplate template))
                {
                    result.FailedRules++;
                    AddRuleDiagnostic(result, rule, "template-not-found", 0, 0, null);
                    ClearRuleRuntimeObjects(segment, rule);
                    continue;
                }

                if (!IsTemplateAllowed(template, out string rejectReason))
                {
                    result.SkippedRules++;
                    AddRuleDiagnostic(result, rule, "template-rejected:" + rejectReason, 0, 0, template);
                    ClearRuleRuntimeObjects(segment, rule);
                    continue;
                }

                if (!DaObjectRegistryService.IsTemplateAvailableForSegmentVariant(template, segment))
                {
                    result.SkippedRules++;
                    AddRuleDiagnostic(result, rule, "template-rejected:variant-unavailable", 0, 0, template);
                    ClearRuleRuntimeObjects(segment, rule);
                    continue;
                }

                if (!TryResolveSourcePrefab(data, template, out GameObject prefab, out string prefabSource, out string resolveFailure))
                {
                    result.FailedRules++;
                    AddRuleDiagnostic(result, rule, "prefab-not-found:" + resolveFailure, 0, 0, template);
                    ClearRuleRuntimeObjects(segment, rule);
                    continue;
                }

                if (!IsRuntimePrefabAllowed(prefab, out string prefabRejectReason))
                {
                    result.SkippedRules++;
                    AddRuleDiagnostic(result, rule, "prefab-rejected:" + prefabRejectReason, 0, 0, template);
                    ClearRuleRuntimeObjects(segment, rule);
                    continue;
                }

                Transform ruleRoot = GetRuleRoot(segment, rule, true);
                int removed = ClearChildren(ruleRoot);
                int spawned = SpawnRuleObjects(segment, segmentBounds, subArea, rule, template, prefab, prefabSource, ruleRoot, result);
                result.RemovedObjects += removed;
                result.SpawnedObjects += spawned;
                result.AppliedRules++;
                AddRuleDiagnostic(result, rule, rule.Count > MaxRuleCount ? "applied:count-clamped" : "applied", spawned, removed, template, prefab, prefabSource);
            }

            Physics.SyncTransforms();
            WritePlacementDiagnostic(data, result);
            DaLog.Info(string.Format(
                CultureInfo.InvariantCulture,
                "Placement rules finished. segment={0}, rules={1}, applied={2}, spawned={3}, skipped={4}, failed={5}, removed={6}",
                result.SegmentName,
                result.RuleCount,
                result.AppliedRules,
                result.SpawnedObjects,
                result.SkippedRules,
                result.FailedRules,
                result.RemovedObjects));
            return result;
        }

        public static int ClearSegmentRuntimeObjects(DaSegmentData segment)
        {
            Transform segmentRoot = GetSegmentRuntimeRoot(segment, false);
            if (segmentRoot == null)
            {
                return 0;
            }

            int removed = ClearChildren(segmentRoot);
            DaLog.Info("Cleared DreamyAscent runtime placement objects. segment=" + (segment != null ? segment.SegmentName : string.Empty) + ", removed=" + removed);
            return removed;
        }

        public static bool IsTemplateSupportedForDirectPlacement(DaObjectRegistryTemplate template, out string reason)
        {
            return IsTemplateAllowed(template, out reason);
        }

        private static int SpawnRuleObjects(
            DaSegmentData segment,
            Bounds segmentBounds,
            DaSubAreaData subArea,
            DaPlacementRuleData rule,
            DaObjectRegistryTemplate template,
            GameObject prefab,
            string prefabSource,
            Transform ruleRoot,
            DaPlacementRuntimeResult result)
        {
            int requested = Mathf.Clamp(rule.Count, 0, MaxRuleCount);
            if (rule.Count > MaxRuleCount)
            {
                result.Warnings.Add(BuildRuleFailure(rule, template, "count-clamped:" + rule.Count.ToString(CultureInfo.InvariantCulture) + "->" + MaxRuleCount.ToString(CultureInfo.InvariantCulture)));
            }

            int spawned = 0;
            int attempts = Mathf.Max(requested * 8, requested);

            for (int attempt = 0; attempt < attempts && spawned < requested; attempt++)
            {
                if (!TryGetPlacementPose(segmentBounds, subArea, rule, ruleRoot, out Vector3 position, out Vector3 normal, out string failure))
                {
                    if (attempt == attempts - 1)
                    {
                        result.Failures.Add(BuildRuleFailure(rule, template, failure));
                    }

                    continue;
                }

                Quaternion rotation = BuildRotation(rule, prefab, normal);
                GameObject instance = UnityEngine.Object.Instantiate(prefab, position, rotation, ruleRoot);
                instance.name = BuildInstanceName(template, spawned);
                instance.SetActive(true);

                float scale = UnityEngine.Random.Range(Mathf.Min(rule.MinScale, rule.MaxScale), Mathf.Max(rule.MinScale, rule.MaxScale));
                instance.transform.localScale = prefab.transform.localScale * Mathf.Max(0.01f, scale);
                spawned++;

                result.Spawns.Add(new DaPlacementSpawnDiagnostic
                {
                    RuleId = rule.Id,
                    RegistryId = rule.RegistryId,
                    TemplateName = template != null ? template.Name : null,
                    InstanceName = instance.name,
                    PrefabSource = prefabSource,
                    Position = FormatVector(position),
                    Rotation = FormatVector(instance.transform.eulerAngles),
                    Scale = FormatVector(instance.transform.localScale),
                    SurfaceNormal = FormatVector(normal)
                });
            }

            return spawned;
        }

        private static bool TryGetPlacementPose(
            Bounds segmentBounds,
            DaSubAreaData subArea,
            DaPlacementRuleData rule,
            Transform runtimeRoot,
            out Vector3 position,
            out Vector3 normal,
            out string failure)
        {
            normal = Vector3.up;
            failure = null;

            Bounds areaBounds = GetSubAreaBounds(segmentBounds, subArea);
            position = GetRandomPointInBounds(areaBounds, subArea);
            position += ToVector3(rule.LocalOffset);

            if (rule.PlacementMode == DaPlacementMode.RandomInSubArea)
            {
                return true;
            }

            float top = areaBounds.center.y + Mathf.Max(areaBounds.extents.y, segmentBounds.extents.y) + 500f;
            Vector3 origin = new Vector3(position.x, top, position.z);
            float distance = Mathf.Max(1000f, areaBounds.size.y + segmentBounds.size.y + 1000f);
            int mask = LayerMask.GetMask("Terrain", "Map");
            if (mask == 0)
            {
                mask = Physics.DefaultRaycastLayers;
            }

            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, distance, mask, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0)
            {
                failure = "raycast-miss";
                return false;
            }

            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            for (int index = 0; index < hits.Length; index++)
            {
                RaycastHit hit = hits[index];
                if (hit.transform == null || IsUnder(hit.transform, runtimeRoot))
                {
                    continue;
                }

                position = hit.point + ToVector3(rule.LocalOffset);
                normal = hit.normal == Vector3.zero ? Vector3.up : hit.normal;
                return true;
            }

            failure = "raycast-hit-only-runtime-objects";
            return false;
        }

        private static Quaternion BuildRotation(DaPlacementRuleData rule, GameObject prefab, Vector3 normal)
        {
            Quaternion prefabRotation = prefab != null ? prefab.transform.rotation : Quaternion.identity;
            switch (rule.RotationMode)
            {
                case DaPlacementRotationMode.KeepPrefab:
                    return prefabRotation;
                case DaPlacementRotationMode.AlignToSurfaceNormal:
                    return Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.AngleAxis(UnityEngine.Random.Range(0f, 360f), normal) * prefabRotation;
                default:
                    return Quaternion.AngleAxis(UnityEngine.Random.Range(0f, 360f), Vector3.up) * prefabRotation;
            }
        }

        private static bool TryResolveSourcePrefab(
            DaTerrainData data,
            DaObjectRegistryTemplate template,
            out GameObject prefab,
            out string source,
            out string failure)
        {
            prefab = null;
            source = null;
            failure = null;

            if (template == null)
            {
                failure = "template-null";
                return false;
            }

            if (template.SourceExamples != null)
            {
                for (int index = 0; index < template.SourceExamples.Count; index++)
                {
                    DaObjectRegistrySourceExample example = template.SourceExamples[index];
                    if (example == null)
                    {
                        continue;
                    }

                    DaLevelGenStepData step = FindRuntimeStepByPath(data, example.StepPath);
                    if (step == null)
                    {
                        continue;
                    }

                    if (TryResolvePrefabFromStep(step, template, example.Field, out prefab))
                    {
                        source = "sourceExample:" + (example.StepPath ?? string.Empty) + ":" + (example.Field ?? string.Empty);
                        return true;
                    }
                }
            }

            if (TryResolvePrefabFromAllSteps(data, template, out prefab, out source))
            {
                return true;
            }

            failure = "no-runtime-prop-spawner-reference";
            return false;
        }

        private static DaLevelGenStepData FindRuntimeStepByPath(DaTerrainData data, string stepPath)
        {
            if (data == null || data.Map == null || data.Map.Segments == null || string.IsNullOrWhiteSpace(stepPath))
            {
                return null;
            }

            for (int segmentIndex = 0; segmentIndex < data.Map.Segments.Count; segmentIndex++)
            {
                DaSegmentData segment = data.Map.Segments[segmentIndex];
                for (int grouperIndex = 0; segment != null && segment.Groupers != null && grouperIndex < segment.Groupers.Count; grouperIndex++)
                {
                    DaPropGrouperData grouper = segment.Groupers[grouperIndex];
                    for (int stepIndex = 0; grouper != null && grouper.Steps != null && stepIndex < grouper.Steps.Count; stepIndex++)
                    {
                        DaLevelGenStepData step = grouper.Steps[stepIndex];
                        if (step == null)
                        {
                            continue;
                        }

                        if (PathMatches(step.HierarchyPath, stepPath))
                        {
                            return step;
                        }
                    }
                }
            }

            return null;
        }

        private static bool TryResolvePrefabFromAllSteps(DaTerrainData data, DaObjectRegistryTemplate template, out GameObject prefab, out string source)
        {
            prefab = null;
            source = null;
            if (data == null || data.Map == null || data.Map.Segments == null)
            {
                return false;
            }

            for (int segmentIndex = 0; segmentIndex < data.Map.Segments.Count; segmentIndex++)
            {
                DaSegmentData segment = data.Map.Segments[segmentIndex];
                for (int grouperIndex = 0; segment != null && segment.Groupers != null && grouperIndex < segment.Groupers.Count; grouperIndex++)
                {
                    DaPropGrouperData grouper = segment.Groupers[grouperIndex];
                    for (int stepIndex = 0; grouper != null && grouper.Steps != null && stepIndex < grouper.Steps.Count; stepIndex++)
                    {
                        DaLevelGenStepData step = grouper.Steps[stepIndex];
                        if (TryResolvePrefabFromStep(step, template, null, out prefab))
                        {
                            source = "scan:" + (step != null ? step.HierarchyPath : string.Empty);
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static bool TryResolvePrefabFromStep(DaLevelGenStepData step, DaObjectRegistryTemplate template, string sourceField, out GameObject prefab)
        {
            prefab = null;
            PropSpawner spawner = step != null ? step.SourceObject as PropSpawner : null;
            if (spawner == null || spawner.props == null || spawner.props.Length == 0)
            {
                return false;
            }

            if (TryParseArrayIndex(sourceField, "props", out int sourceIndex) &&
                sourceIndex >= 0 &&
                sourceIndex < spawner.props.Length &&
                spawner.props[sourceIndex] != null)
            {
                prefab = spawner.props[sourceIndex];
                return true;
            }

            for (int index = 0; index < spawner.props.Length; index++)
            {
                GameObject candidate = spawner.props[index];
                if (MatchesTemplatePrefab(candidate, template))
                {
                    prefab = candidate;
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesTemplatePrefab(GameObject candidate, DaObjectRegistryTemplate template)
        {
            if (candidate == null || template == null)
            {
                return false;
            }

            string candidateName = CleanObjectName(candidate.name);
            string templateName = CleanObjectName(template.Name);
            string pathLeaf = CleanObjectName(GetPathLeaf(template.GameObjectPath));

            return !string.IsNullOrWhiteSpace(candidateName) &&
                   (string.Equals(candidateName, templateName, StringComparison.Ordinal) ||
                    string.Equals(candidateName, pathLeaf, StringComparison.Ordinal));
        }

        private static bool IsTemplateAllowed(DaObjectRegistryTemplate template, out string reason)
        {
            reason = null;
            if (template == null)
            {
                reason = "null";
                return false;
            }

            if (!template.TechnicalLowRiskPlacementCandidate)
            {
                reason = "not-low-risk";
                return false;
            }

            if (template.HasChildGeneration)
            {
                reason = "child-generation";
                return false;
            }

            if (template.HasSingleItemSpawner)
            {
                reason = "single-item-spawner";
                return false;
            }

            if (template.HasPhotonView)
            {
                reason = "photon-view";
                return false;
            }

            if (TryFindTemplateComponent(template, s_templateSpawnerComponents, out string spawnerComponent))
            {
                reason = "runtime-spawner:" + spawnerComponent;
                return false;
            }

            return true;
        }

        private static bool IsRuntimePrefabAllowed(GameObject prefab, out string reason)
        {
            reason = null;
            if (prefab == null)
            {
                reason = "null";
                return false;
            }

            if (HasComponentNamedInChildren(prefab, "PhotonView"))
            {
                reason = "photon-view";
                return false;
            }

            if (HasComponentNamedInChildren(prefab, "SingleItemSpawner"))
            {
                reason = "single-item-spawner";
                return false;
            }

            if (HasComponentTypeNamedInChildren(prefab, "Spawner", true, out string spawnerComponent))
            {
                reason = "runtime-spawner:" + spawnerComponent;
                return false;
            }

            if (prefab.GetComponentsInChildren<LevelGenStep>(true).Length > 0)
            {
                reason = "child-level-gen-step";
                return false;
            }

            if (prefab.GetComponentsInChildren<PropGrouper>(true).Length > 0)
            {
                reason = "child-prop-grouper";
                return false;
            }

            return true;
        }

        private static bool TryFindTemplate(string registryId, out DaObjectRegistryTemplate template)
        {
            template = null;
            DaObjectRegistry registry = DaObjectRegistryService.GetRegistry();
            if (registry == null || registry.Templates == null || string.IsNullOrWhiteSpace(registryId))
            {
                return false;
            }

            for (int index = 0; index < registry.Templates.Count; index++)
            {
                DaObjectRegistryTemplate candidate = registry.Templates[index];
                if (candidate != null && string.Equals(candidate.RegistryId, registryId, StringComparison.Ordinal))
                {
                    template = candidate;
                    return true;
                }
            }

            return false;
        }

        private static Transform GetRuleRoot(DaSegmentData segment, DaPlacementRuleData rule, bool create)
        {
            Transform segmentRoot = GetSegmentRuntimeRoot(segment, create);
            if (segmentRoot == null || rule == null)
            {
                return null;
            }

            string ruleName = SanitizeNodeName(string.IsNullOrWhiteSpace(rule.Id) ? "rule" : rule.Id);
            Transform existing = segmentRoot.Find(ruleName);
            if (existing != null || !create)
            {
                return existing;
            }

            GameObject ruleObject = new GameObject(ruleName);
            ruleObject.transform.SetParent(segmentRoot, false);
            return ruleObject.transform;
        }

        private static Transform GetSegmentRuntimeRoot(DaSegmentData segment, bool create)
        {
            if (segment == null)
            {
                return null;
            }

            Transform root = GetRuntimeRoot(segment, create);
            if (root == null)
            {
                return null;
            }

            string segmentName = SanitizeNodeName(string.IsNullOrWhiteSpace(segment.SegmentName) ? "segment" : segment.SegmentName);
            Transform existing = root.Find(segmentName);
            if (existing != null || !create)
            {
                return existing;
            }

            GameObject segmentObject = new GameObject(segmentName);
            segmentObject.transform.SetParent(root, false);
            return segmentObject.transform;
        }

        private static Transform GetRuntimeRoot(DaSegmentData segment, bool create)
        {
            Transform parent = GetRuntimeParent(segment);
            if (parent != null)
            {
                Transform existing = parent.Find(RuntimeRootName);
                if (existing != null || !create)
                {
                    return existing;
                }

                GameObject childRoot = new GameObject(RuntimeRootName);
                childRoot.transform.SetParent(parent, false);
                return childRoot.transform;
            }

            GameObject rootObject = GameObject.Find(RuntimeRootName);
            if (rootObject != null || !create)
            {
                return rootObject != null ? rootObject.transform : null;
            }

            rootObject = new GameObject(RuntimeRootName);
            return rootObject.transform;
        }

        private static Transform GetRuntimeParent(DaSegmentData segment)
        {
            if (segment == null || segment.SourceRoots == null)
            {
                return null;
            }

            for (int index = 0; index < segment.SourceRoots.Count; index++)
            {
                Transform root = segment.SourceRoots[index];
                if (root != null)
                {
                    return root;
                }
            }

            return null;
        }

        private static int ClearRuleRuntimeObjects(DaSegmentData segment, DaPlacementRuleData rule)
        {
            Transform ruleRoot = GetRuleRoot(segment, rule, false);
            return ruleRoot != null ? ClearChildren(ruleRoot) : 0;
        }

        private static int ClearChildren(Transform parent)
        {
            if (parent == null)
            {
                return 0;
            }

            int removed = 0;
            for (int index = parent.childCount - 1; index >= 0; index--)
            {
                Transform child = parent.GetChild(index);
                if (child == null)
                {
                    continue;
                }

                removed += CountHierarchyObjects(child);
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }

            return removed;
        }

        private static int CountHierarchyObjects(Transform root)
        {
            if (root == null)
            {
                return 0;
            }

            int count = 1;
            for (int index = 0; index < root.childCount; index++)
            {
                count += CountHierarchyObjects(root.GetChild(index));
            }

            return count;
        }

        private static DaSubAreaData FindSubArea(DaSegmentData segment, string id)
        {
            if (segment == null || segment.SubAreas == null || segment.SubAreas.Count == 0)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(id))
            {
                for (int index = 0; index < segment.SubAreas.Count; index++)
                {
                    DaSubAreaData area = segment.SubAreas[index];
                    if (area != null && string.Equals(area.Id, id, StringComparison.Ordinal))
                    {
                        return area;
                    }
                }
            }

            return segment.SubAreas[0];
        }

        private static Bounds GetSegmentBounds(DaSegmentData segment)
        {
            bool hasBounds = false;
            Bounds bounds = new Bounds(Vector3.zero, new Vector3(60f, 60f, 60f));

            foreach (Transform root in segment.SourceRoots ?? new List<Transform>())
            {
                if (root == null)
                {
                    continue;
                }

                Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
                for (int index = 0; index < renderers.Length; index++)
                {
                    Renderer renderer = renderers[index];
                    if (renderer == null)
                    {
                        continue;
                    }

                    if (!hasBounds)
                    {
                        bounds = renderer.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }
                }

                Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
                for (int index = 0; index < colliders.Length; index++)
                {
                    Collider collider = colliders[index];
                    if (collider == null)
                    {
                        continue;
                    }

                    if (!hasBounds)
                    {
                        bounds = collider.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(collider.bounds);
                    }
                }
            }

            if (!hasBounds && segment.SourceRoots != null && segment.SourceRoots.Count > 0 && segment.SourceRoots[0] != null)
            {
                bounds.center = segment.SourceRoots[0].position;
            }

            return bounds;
        }

        private static Bounds GetSubAreaBounds(Bounds segmentBounds, DaSubAreaData subArea)
        {
            if (subArea == null || subArea.Shape == DaSubAreaShape.SegmentBounds)
            {
                return segmentBounds;
            }

            Vector3 size = ToVector3(subArea.Size);
            if (size.x <= 0f)
            {
                size.x = 40f;
            }

            if (size.y <= 0f)
            {
                size.y = 30f;
            }

            if (size.z <= 0f)
            {
                size.z = 40f;
            }

            return new Bounds(segmentBounds.center + ToVector3(subArea.CenterOffset), size);
        }

        private static Vector3 GetRandomPointInBounds(Bounds bounds, DaSubAreaData subArea)
        {
            float x;
            float z;
            if (subArea != null && subArea.Shape == DaSubAreaShape.Circle)
            {
                float radius = Mathf.Sqrt(UnityEngine.Random.value) * Mathf.Min(bounds.extents.x, bounds.extents.z);
                float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                x = bounds.center.x + Mathf.Cos(angle) * radius;
                z = bounds.center.z + Mathf.Sin(angle) * radius;
            }
            else
            {
                x = UnityEngine.Random.Range(bounds.min.x, bounds.max.x);
                z = UnityEngine.Random.Range(bounds.min.z, bounds.max.z);
            }

            float y = bounds.center.y;
            return new Vector3(x, y, z);
        }

        private static Vector3 ToVector3(DaVector3Data value)
        {
            return value == null ? Vector3.zero : new Vector3(value.X, value.Y, value.Z);
        }

        private static bool HasComponentNamedInChildren(GameObject gameObject, string typeName)
        {
            return HasComponentTypeNamedInChildren(gameObject, typeName, false, out string _);
        }

        private static bool HasComponentTypeNamedInChildren(GameObject gameObject, string typeName, bool includeBaseTypes, out string matchedTypeName)
        {
            Component[] components = gameObject != null ? gameObject.GetComponentsInChildren<Component>(true) : null;
            matchedTypeName = null;
            for (int index = 0; components != null && index < components.Length; index++)
            {
                Component component = components[index];
                if (component == null)
                {
                    continue;
                }

                Type type = component.GetType();
                while (type != null && type != typeof(object))
                {
                    if (string.Equals(type.Name, typeName, StringComparison.Ordinal))
                    {
                        matchedTypeName = component.GetType().Name;
                        return true;
                    }

                    if (!includeBaseTypes)
                    {
                        break;
                    }

                    type = type.BaseType;
                }
            }

            return false;
        }

        private static bool TryFindTemplateComponent(DaObjectRegistryTemplate template, string[] typeNames, out string componentName)
        {
            componentName = null;
            if (template == null || template.Components == null || typeNames == null)
            {
                return false;
            }

            for (int componentIndex = 0; componentIndex < template.Components.Count; componentIndex++)
            {
                string current = template.Components[componentIndex];
                for (int typeIndex = 0; typeIndex < typeNames.Length; typeIndex++)
                {
                    if (ComponentNameMatches(current, typeNames[typeIndex]))
                    {
                        componentName = current;
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool ComponentNameMatches(string componentName, string typeName)
        {
            if (string.IsNullOrWhiteSpace(componentName) || string.IsNullOrWhiteSpace(typeName))
            {
                return false;
            }

            if (string.Equals(componentName, typeName, StringComparison.Ordinal))
            {
                return true;
            }

            int lastDot = componentName.LastIndexOf('.');
            string leaf = lastDot >= 0 ? componentName.Substring(lastDot + 1) : componentName;
            return string.Equals(leaf, typeName, StringComparison.Ordinal);
        }

        private static bool TryParseArrayIndex(string field, string prefix, out int index)
        {
            index = -1;
            if (string.IsNullOrWhiteSpace(field) || string.IsNullOrWhiteSpace(prefix))
            {
                return false;
            }

            string start = prefix + "[";
            string value = field.Trim();
            if (!value.StartsWith(start, StringComparison.Ordinal) || !value.EndsWith("]", StringComparison.Ordinal))
            {
                return false;
            }

            string number = value.Substring(start.Length, value.Length - start.Length - 1);
            return int.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out index);
        }

        private static bool PathMatches(string currentPath, string sourcePath)
        {
            if (string.IsNullOrWhiteSpace(currentPath) || string.IsNullOrWhiteSpace(sourcePath))
            {
                return false;
            }

            string current = NormalizePath(currentPath);
            string source = NormalizePath(sourcePath);
            if (string.Equals(current, source, StringComparison.Ordinal))
            {
                return true;
            }

            string currentTail = GetPathTail(current, 4);
            string sourceTail = GetPathTail(source, 4);
            return !string.IsNullOrWhiteSpace(currentTail) &&
                   string.Equals(currentTail, sourceTail, StringComparison.Ordinal);
        }

        private static string NormalizePath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').Trim('/');
        }

        private static string GetPathTail(string path, int count)
        {
            string[] parts = NormalizePath(path).Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return string.Empty;
            }

            int start = Mathf.Max(0, parts.Length - count);
            return string.Join("/", SubArray(parts, start, parts.Length - start));
        }

        private static string[] SubArray(string[] values, int start, int length)
        {
            string[] result = new string[length];
            Array.Copy(values, start, result, 0, length);
            return result;
        }

        private static string GetPathLeaf(string path)
        {
            string normalized = NormalizePath(path);
            int index = normalized.LastIndexOf("/", StringComparison.Ordinal);
            return index >= 0 ? normalized.Substring(index + 1) : normalized;
        }

        private static string CleanObjectName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string result = value.Trim();
            if (result.EndsWith("(Clone)", StringComparison.Ordinal))
            {
                result = result.Substring(0, result.Length - "(Clone)".Length).Trim();
            }

            return result;
        }

        private static string BuildInstanceName(DaObjectRegistryTemplate template, int index)
        {
            string name = template != null ? template.Name ?? template.DisplayName ?? "PlacedObject" : "PlacedObject";
            return "DA_" + CleanObjectName(name) + "_" + index.ToString("000", CultureInfo.InvariantCulture);
        }

        private static bool IsUnder(Transform transform, Transform parent)
        {
            Transform current = transform;
            while (current != null)
            {
                if (current == parent)
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static string SanitizeNodeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "node";
            }

            char[] chars = value.Trim().ToCharArray();
            for (int index = 0; index < chars.Length; index++)
            {
                char current = chars[index];
                if (current == '/' || current == '\\' || current == ':' || current == '*' || current == '?' || current == '"' || current == '<' || current == '>' || current == '|')
                {
                    chars[index] = '_';
                }
            }

            return new string(chars);
        }

        private static string FormatVector(Vector3 value)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:0.###},{1:0.###},{2:0.###}", value.x, value.y, value.z);
        }

        private static string BuildRuleFailure(DaPlacementRuleData rule, DaObjectRegistryTemplate template, string failure)
        {
            return (rule != null ? rule.Id : string.Empty) + "|" + (template != null ? template.RegistryId : string.Empty) + "|" + (failure ?? string.Empty);
        }

        private static void AddRuleDiagnostic(
            DaPlacementRuntimeResult result,
            DaPlacementRuleData rule,
            string status,
            int spawned,
            int removed,
            DaObjectRegistryTemplate template,
            GameObject prefab = null,
            string prefabSource = null)
        {
            result.Rules.Add(new DaPlacementRuleRuntimeDiagnostic
            {
                RuleId = rule != null ? rule.Id : null,
                RuleName = rule != null ? rule.DisplayName : null,
                RegistryId = rule != null ? rule.RegistryId : null,
                TargetSubAreaId = rule != null ? rule.TargetSubAreaId : null,
                Status = status,
                Spawned = spawned,
                Removed = removed,
                TemplateName = template != null ? template.Name : null,
                TemplateDisplayName = template != null ? template.DisplayName : null,
                PrefabName = prefab != null ? prefab.name : null,
                PrefabSource = prefabSource
            });
        }

        private static void WritePlacementDiagnostic(DaTerrainData data, DaPlacementRuntimeResult result)
        {
            try
            {
                string root = DaDiagnosticService.DiagnosticDirectoryPath;
                if (string.IsNullOrWhiteSpace(root))
                {
                    return;
                }

                string mapKey = data != null && data.Map != null ? data.Map.MapKey : "unknown-map";
                string directory = Path.Combine(root, "PlacementRuntime");
                Directory.CreateDirectory(directory);
                string fileName = SanitizeNodeName(mapKey) + "_" + SanitizeNodeName(result.SegmentName) + ".json";
                string path = Path.Combine(directory, fileName);
                File.WriteAllText(path, JsonConvert.SerializeObject(result, s_jsonSettings));
                DaLog.Info("Placement runtime diagnostic written: " + path);
            }
            catch (Exception ex)
            {
                DaLog.Warn("Failed to write placement runtime diagnostic: " + ex.Message);
            }
        }
    }

    internal sealed class DaPlacementRuntimeResult
    {
        public string SegmentName { get; set; }
        public int RuleCount { get; set; }
        public int AppliedRules { get; set; }
        public int SkippedRules { get; set; }
        public int FailedRules { get; set; }
        public int SpawnedObjects { get; set; }
        public int RemovedObjects { get; set; }
        public List<string> Warnings { get; } = new List<string>();
        public List<string> Failures { get; } = new List<string>();
        public List<DaPlacementRuleRuntimeDiagnostic> Rules { get; } = new List<DaPlacementRuleRuntimeDiagnostic>();
        public List<DaPlacementSpawnDiagnostic> Spawns { get; } = new List<DaPlacementSpawnDiagnostic>();
    }

    internal sealed class DaPlacementRuleRuntimeDiagnostic
    {
        public string RuleId { get; set; }
        public string RuleName { get; set; }
        public string RegistryId { get; set; }
        public string TargetSubAreaId { get; set; }
        public string Status { get; set; }
        public int Spawned { get; set; }
        public int Removed { get; set; }
        public string TemplateName { get; set; }
        public string TemplateDisplayName { get; set; }
        public string PrefabName { get; set; }
        public string PrefabSource { get; set; }
    }

    internal sealed class DaPlacementSpawnDiagnostic
    {
        public string RuleId { get; set; }
        public string RegistryId { get; set; }
        public string TemplateName { get; set; }
        public string InstanceName { get; set; }
        public string PrefabSource { get; set; }
        public string Position { get; set; }
        public string Rotation { get; set; }
        public string Scale { get; set; }
        public string SurfaceNormal { get; set; }
    }
}
