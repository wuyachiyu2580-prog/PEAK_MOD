using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DreamyAscent.Data;
using DreamyAscent.Helpers;
using UnityEngine;
using Zorro.Core;

namespace DreamyAscent.Services
{
    internal static class DaTerrainExportService
    {
        private const BindingFlags InstanceFieldFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private sealed class VariantRule
        {
            public VariantRule(string biomeName, string selectionType, params string[] variants)
            {
                BiomeName = biomeName;
                SelectionType = selectionType;
                Variants = variants ?? Array.Empty<string>();
            }

            public string BiomeName { get; }
            public string SelectionType { get; }
            public string[] Variants { get; }
        }

        private static readonly HashSet<string> s_propertyWhitelist = new HashSet<string>(StringComparer.Ordinal)
        {
            "mute",
            "height",
            "rayLength",
            "nrOfSpawns",
            "randomSpawns",
            "minSpawnCount",
            "rayCastSpawn",
            "raycastPosition",
            "rayNearCutoff",
            "rayDirectionOffset",
            "layerType",
            "syncTransforms",
            "minMaxSpawn",
            "area",
            "chanceToUseSpawner",
            "overallSpawnChance",
            "scaleMinMax",
            "radius",
            "circleSize",
            "inverted"
        };

        private static readonly HashSet<string> s_modConstraintWhitelist = new HashSet<string>(StringComparer.Ordinal)
        {
            "PSC_Height",
            "PSC_LineCheck",
            "PSC_Normal",
            "PSC_Perlin",
            "PSC_SameTypeDistance",
            "PSM_RandomScale",
            "PSM_RayDirectionOffset",
            "PSM_SpecificRotation",
            "PSM_UpLerp",
            "PSM_RandomRotation",
            "PSM_LocalOffset",
            "PSM_RandomOffset",
            "PSM_PlacementOffset",
            "PSM_SetUpRotationToNormal",
            "PSC_BannedMaterial",
            "PSC_CircleMask",
            "PSM_ChildSpawners",
            "PSM_SingleItemSpawner"
        };

        private static readonly Dictionary<string, VariantRule> s_variantRules = new Dictionary<string, VariantRule>(StringComparer.Ordinal)
        {
            ["Beach_Segment"] = new VariantRule("Shore", "BiomeVariant", "Default", "SnakeBeach", "RedBeach", "BlueBeach", "JellyHell", "BlackSand"),
            ["Jungle_Segment"] = new VariantRule("Tropics", "BiomeVariant", "Default", "Lava", "Pillars", "Thorny", "Bombs", "Ivy", "SkyJungle"),
            ["Roots Segment"] = new VariantRule("Roots", "VariantObject", "Default", "Cave Mania", "Deep Water", "Bomb Beetle", "Deep Woods", "Clearcut"),
            ["Snow_Segment"] = new VariantRule("Alpine", "BiomeVariant", "Default", "Lava", "Spiky", "GeyserHell"),
            ["Desert_Segment"] = new VariantRule("Mesa", "VariantObject", "NoVariant", "ScorpionsHell", "CacusHell", "CactusForest", "DynamiteHell", "TornadoHell", "TumblerHell")
        };

        public static DaTerrainData LastExportedTerrain { get; private set; }

        public static bool TryExportCurrent(out DaTerrainData data)
        {
            data = new DaTerrainData();

            if (!MapHandler.Exists)
            {
                DaLog.ThrottleInfo("export-waiting-map", "Waiting for MapHandler.");
                return false;
            }

            MapHandler handler = Singleton<MapHandler>.Instance;
            if (handler == null || handler.segments == null || handler.segments.Length == 0)
            {
                DaLog.ThrottleInfo("export-waiting-segments", "MapHandler exists, but segments are not ready.");
                return false;
            }

            int segmentCount = 0;
            int grouperCount = 0;
            int stepCount = 0;

            for (int index = 0; index < handler.segments.Length; index++)
            {
                MapHandler.MapSegment segment = handler.segments[index];
                List<Transform> roots = ResolveSegmentRoots(segment, index);
                if (roots.Count == 0)
                {
                    continue;
                }

                DaSegmentData segmentData = BuildSegment(GetSegmentName(segment, index), segment, roots, ref grouperCount, ref stepCount);
                data.Map.Segments.Add(segmentData);
                segmentCount++;
            }

            if (segmentCount == 0)
            {
                DaLog.Warn("Terrain export found no usable segments.");
                return false;
            }

            data.Map.MapKey = BuildMapKey(data.Map.Segments);
            InheritSegmentRuntimeConfig(data, LastExportedTerrain);
            LastExportedTerrain = data;
            DaTemplateBaselineService.Invalidate();
            DaObjectCatalogService.Invalidate();
            DaLog.Info(string.Format(
                "Terrain export completed. mapKey={0}, segments={1}, groupers={2}, steps={3}",
                data.Map.MapKey,
                segmentCount,
                grouperCount,
                stepCount));
            return true;
        }

        public static bool TryBindSegmentRuntimeReferences(DaSegmentData targetSegment)
        {
            if (targetSegment == null || string.IsNullOrWhiteSpace(targetSegment.SegmentName))
            {
                return false;
            }

            if (!MapHandler.Exists)
            {
                DaLog.Warn("Cannot rebind segment runtime references because MapHandler is missing.");
                return false;
            }

            MapHandler handler = Singleton<MapHandler>.Instance;
            if (handler == null || handler.segments == null || handler.segments.Length == 0)
            {
                DaLog.Warn("Cannot rebind segment runtime references because segments are missing.");
                return false;
            }

            for (int index = 0; index < handler.segments.Length; index++)
            {
                MapHandler.MapSegment sourceSegment = handler.segments[index];
                string segmentName = GetSegmentName(sourceSegment, index);
                if (!string.Equals(segmentName, targetSegment.SegmentName, StringComparison.Ordinal))
                {
                    continue;
                }

                List<Transform> roots = ResolveSegmentRoots(sourceSegment, index);
                if (roots.Count == 0)
                {
                    DaLog.Warn("Runtime rebind found segment but no roots. segment=" + targetSegment.SegmentName);
                    return false;
                }

                targetSegment.SourceRoots = new List<Transform>(roots);
                targetSegment.SourceSegment = sourceSegment;
                targetSegment.LevelSlot = index;
                targetSegment.SegmentPath = GetHierarchyPath(sourceSegment.segmentParent != null ? sourceSegment.segmentParent.transform : null);
                FillVariantMetadata(targetSegment, sourceSegment, roots);
                FillRootPaths(targetSegment, roots);

                List<PropGrouper> runtimeGroupers = GetUniqueGroupers(roots);
                runtimeGroupers.Sort(CompareByHierarchyPath);

                int boundGroupers = BindGrouperReferences(targetSegment, runtimeGroupers, roots);
                DaLog.Info(string.Format(
                    "Runtime references rebound. segment={0}, roots={1}, groupers={2}",
                    targetSegment.SegmentName,
                    roots.Count,
                    boundGroupers));
                return boundGroupers > 0 || targetSegment.Groupers == null || targetSegment.Groupers.Count == 0;
            }

            DaLog.Warn("Runtime rebind could not find segment: " + targetSegment.SegmentName);
            return false;
        }

        private static int BindGrouperReferences(DaSegmentData targetSegment, List<PropGrouper> runtimeGroupers, List<Transform> roots)
        {
            int bound = 0;
            HashSet<int> used = new HashSet<int>();

            for (int targetIndex = 0; targetSegment.Groupers != null && targetIndex < targetSegment.Groupers.Count; targetIndex++)
            {
                DaPropGrouperData targetGrouper = targetSegment.Groupers[targetIndex];
                if (targetGrouper == null || string.IsNullOrWhiteSpace(targetGrouper.GrouperName))
                {
                    continue;
                }

                PropGrouper runtimeGrouper = FindMatchingRuntimeGrouper(targetGrouper, runtimeGroupers, roots, used);
                if (runtimeGrouper == null)
                {
                    continue;
                }

                used.Add(runtimeGrouper.GetInstanceID());
                targetGrouper.SourceObject = runtimeGrouper;
                targetGrouper.HierarchyPath = GetHierarchyPath(runtimeGrouper.transform);
                BindStepReferences(targetGrouper, runtimeGrouper);
                bound++;
            }

            return bound;
        }

        private static PropGrouper FindMatchingRuntimeGrouper(
            DaPropGrouperData targetGrouper,
            List<PropGrouper> runtimeGroupers,
            List<Transform> roots,
            HashSet<int> used)
        {
            if (!string.IsNullOrWhiteSpace(targetGrouper.HierarchyPath))
            {
                for (int index = 0; index < runtimeGroupers.Count; index++)
                {
                    PropGrouper candidate = runtimeGroupers[index];
                    if (candidate == null ||
                        used.Contains(candidate.GetInstanceID()) ||
                        IsRootTransform(candidate.transform, roots) ||
                        HasLevelGenStepAncestor(candidate.transform, roots))
                    {
                        continue;
                    }

                    if (string.Equals(GetHierarchyPath(candidate.transform), targetGrouper.HierarchyPath, StringComparison.Ordinal))
                    {
                        return candidate;
                    }
                }
            }

            for (int index = 0; index < runtimeGroupers.Count; index++)
            {
                PropGrouper candidate = runtimeGroupers[index];
                if (candidate == null ||
                    used.Contains(candidate.GetInstanceID()) ||
                    IsRootTransform(candidate.transform, roots) ||
                    HasLevelGenStepAncestor(candidate.transform, roots) ||
                    !string.Equals(candidate.name, targetGrouper.GrouperName, StringComparison.Ordinal))
                {
                    continue;
                }

                return candidate;
            }

            return null;
        }

        private static void BindStepReferences(DaPropGrouperData targetGrouper, PropGrouper runtimeGrouper)
        {
            List<LevelGenStep> runtimeSteps = GetDirectLevelGenSteps(runtimeGrouper);
            HashSet<int> used = new HashSet<int>();

            for (int targetIndex = 0; targetGrouper.Steps != null && targetIndex < targetGrouper.Steps.Count; targetIndex++)
            {
                DaLevelGenStepData targetStep = targetGrouper.Steps[targetIndex];
                if (targetStep == null)
                {
                    continue;
                }

                LevelGenStep runtimeStep = FindMatchingRuntimeStep(targetStep, runtimeSteps, used);
                if (runtimeStep == null)
                {
                    continue;
                }

                used.Add(runtimeStep.GetInstanceID());
                targetStep.SourceObject = runtimeStep;
                targetStep.HierarchyPath = GetHierarchyPath(runtimeStep.transform);
                BindConstraintReferences(runtimeStep, "modifiers", targetStep.Modifiers);
                BindConstraintReferences(runtimeStep, "constraints", targetStep.Constraints);
                BindConstraintReferences(runtimeStep, "postConstraints", targetStep.PostConstraints);
            }
        }

        private static List<LevelGenStep> GetDirectLevelGenSteps(PropGrouper runtimeGrouper)
        {
            List<LevelGenStep> steps = new List<LevelGenStep>();
            if (runtimeGrouper == null)
            {
                return steps;
            }

            for (int childIndex = 0; childIndex < runtimeGrouper.transform.childCount; childIndex++)
            {
                Transform child = runtimeGrouper.transform.GetChild(childIndex);
                LevelGenStep step = child.GetComponent<LevelGenStep>();
                if (step != null)
                {
                    steps.Add(step);
                }
            }

            return steps;
        }

        private static LevelGenStep FindMatchingRuntimeStep(DaLevelGenStepData targetStep, List<LevelGenStep> runtimeSteps, HashSet<int> used)
        {
            if (!string.IsNullOrWhiteSpace(targetStep.HierarchyPath))
            {
                for (int index = 0; index < runtimeSteps.Count; index++)
                {
                    LevelGenStep candidate = runtimeSteps[index];
                    if (candidate == null || used.Contains(candidate.GetInstanceID()))
                    {
                        continue;
                    }

                    if (string.Equals(GetHierarchyPath(candidate.transform), targetStep.HierarchyPath, StringComparison.Ordinal))
                    {
                        return candidate;
                    }
                }
            }

            for (int index = 0; index < runtimeSteps.Count; index++)
            {
                LevelGenStep candidate = runtimeSteps[index];
                if (candidate == null ||
                    used.Contains(candidate.GetInstanceID()) ||
                    !string.Equals(candidate.name, targetStep.StepName, StringComparison.Ordinal) ||
                    !string.Equals(candidate.GetType().Name, targetStep.StepType, StringComparison.Ordinal))
                {
                    continue;
                }

                return candidate;
            }

            return null;
        }

        private static void BindConstraintReferences(LevelGenStep runtimeStep, string fieldName, List<DaConstraintData> targetConstraints)
        {
            if (runtimeStep == null || targetConstraints == null || targetConstraints.Count == 0)
            {
                return;
            }

            FieldInfo field = runtimeStep.GetType().GetField(fieldName, InstanceFieldFlags);
            if (field == null || !(field.GetValue(runtimeStep) is IEnumerable enumerable))
            {
                return;
            }

            List<object> runtimeConstraints = new List<object>();
            foreach (object item in enumerable)
            {
                if (item != null)
                {
                    runtimeConstraints.Add(item);
                }
            }

            HashSet<int> used = new HashSet<int>();
            for (int targetIndex = 0; targetIndex < targetConstraints.Count; targetIndex++)
            {
                DaConstraintData target = targetConstraints[targetIndex];
                if (target == null || string.IsNullOrWhiteSpace(target.Type))
                {
                    continue;
                }

                for (int runtimeIndex = 0; runtimeIndex < runtimeConstraints.Count; runtimeIndex++)
                {
                    object candidate = runtimeConstraints[runtimeIndex];
                    if (candidate == null ||
                        used.Contains(runtimeIndex) ||
                        !string.Equals(candidate.GetType().Name, target.Type, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    target.SourceObject = candidate;
                    used.Add(runtimeIndex);
                    break;
                }
            }
        }

        private static void InheritSegmentRuntimeConfig(DaTerrainData target, DaTerrainData previous)
        {
            if (target == null || target.Map == null || target.Map.Segments == null ||
                previous == null || previous.Map == null || previous.Map.Segments == null ||
                !string.Equals(target.Map.MapKey, previous.Map.MapKey, StringComparison.Ordinal))
            {
                return;
            }

            Dictionary<string, DaSegmentData> previousSegments = new Dictionary<string, DaSegmentData>(StringComparer.Ordinal);
            for (int index = 0; index < previous.Map.Segments.Count; index++)
            {
                DaSegmentData segment = previous.Map.Segments[index];
                if (segment != null && !string.IsNullOrWhiteSpace(segment.SegmentName))
                {
                    previousSegments[segment.SegmentName] = segment;
                }
            }

            int inheritedModes = 0;
            int inheritedPlacementConfigs = 0;
            for (int index = 0; index < target.Map.Segments.Count; index++)
            {
                DaSegmentData segment = target.Map.Segments[index];
                if (segment == null || string.IsNullOrWhiteSpace(segment.SegmentName))
                {
                    continue;
                }

                if (previousSegments.TryGetValue(segment.SegmentName, out DaSegmentData previousSegment))
                {
                    segment.EditMode = previousSegment.EditMode;
                    inheritedModes++;

                    if (previousSegment.HasPlacementConfigSpecified ||
                        previousSegment.SubAreas != null && previousSegment.SubAreas.Count > 0 ||
                        previousSegment.PlacementRules != null && previousSegment.PlacementRules.Count > 0)
                    {
                        segment.SubAreas = DaRuntimeEditService.CloneSubAreas(previousSegment.SubAreas);
                        segment.PlacementRules = DaRuntimeEditService.ClonePlacementRules(previousSegment.PlacementRules, segment.SubAreas);
                        inheritedPlacementConfigs++;
                    }
                }
            }

            if (inheritedModes > 0 || inheritedPlacementConfigs > 0)
            {
                DaLog.Info("Inherited segment runtime config after export rescan. mapKey=" + target.Map.MapKey + ", modes=" + inheritedModes + ", placementConfigs=" + inheritedPlacementConfigs);
            }
        }

        private static DaSegmentData BuildSegment(string segmentName, MapHandler.MapSegment sourceSegment, List<Transform> roots, ref int grouperCount, ref int stepCount)
        {
            DaSegmentData segmentData = new DaSegmentData
            {
                SegmentName = segmentName,
                SourceRoots = new List<Transform>(roots),
                SourceSegment = sourceSegment
            };

            segmentData.LevelSlot = FindLevelSlot(sourceSegment);
            segmentData.SegmentPath = GetHierarchyPath(sourceSegment != null && sourceSegment.segmentParent != null ? sourceSegment.segmentParent.transform : null);
            FillVariantMetadata(segmentData, sourceSegment, roots);
            FillRootPaths(segmentData, roots);

            List<PropGrouper> groupers = GetUniqueGroupers(roots);
            groupers.Sort(CompareByHierarchyPath);
            int localStepCount = 0;

            foreach (PropGrouper grouper in groupers)
            {
                if (grouper == null ||
                    IsRootTransform(grouper.transform, roots) ||
                    HasLevelGenStepAncestor(grouper.transform, roots))
                {
                    continue;
                }

                DaPropGrouperData grouperData = BuildGrouper(grouper, ref stepCount);
                if (grouperData.Steps.Count == 0)
                {
                    continue;
                }

                segmentData.Groupers.Add(grouperData);
                grouperCount++;
                localStepCount += grouperData.Steps.Count;
            }

            FinalizeVariantMetadata(segmentData);

            if (segmentData.Groupers.Count == 0)
            {
                DaLog.OnceWarn(
                    "segment-no-groupers:" + segmentName,
                    "No exportable prop groupers were found under segment " + segmentName + ".");
            }
            else
            {
                DaLog.Info(string.Format(
                    "Segment export: segment={0}, roots={1}, groupers={2}, steps={3}",
                    segmentName,
                    roots.Count,
                    segmentData.Groupers.Count,
                    localStepCount));
            }

            return segmentData;
        }

        private static DaPropGrouperData BuildGrouper(PropGrouper grouper, ref int stepCount)
        {
            DaPropGrouperData grouperData = new DaPropGrouperData
            {
                GrouperName = grouper.name,
                HierarchyPath = GetHierarchyPath(grouper.transform),
                SourceObject = grouper
            };

            for (int childIndex = 0; childIndex < grouper.transform.childCount; childIndex++)
            {
                Transform child = grouper.transform.GetChild(childIndex);
                if (child == null)
                {
                    continue;
                }

                LevelGenStep step = child.GetComponent<LevelGenStep>();
                if (step == null)
                {
                    continue;
                }

                grouperData.Steps.Add(BuildStep(step));
                stepCount++;
            }

            return grouperData;
        }

        private static DaLevelGenStepData BuildStep(LevelGenStep step)
        {
            Type stepType = step.GetType();
            DaLevelGenStepData stepData = new DaLevelGenStepData
            {
                StepName = step.name,
                StepType = stepType.Name,
                HierarchyPath = GetHierarchyPath(step.transform),
                SourceObject = step
            };

            FieldInfo[] fields = stepType.GetFields(InstanceFieldFlags);
            Array.Sort(fields, (left, right) => string.CompareOrdinal(left.Name, right.Name));

            foreach (FieldInfo field in fields)
            {
                if (!ShouldCaptureField(field))
                {
                    continue;
                }

                if (!TryGetFieldValue(field, step, out object value))
                {
                    continue;
                }

                stepData.Properties.Add(new DaPropertyData
                {
                    Name = field.Name,
                    Value = value,
                    InitialValue = value
                });
            }

            PopulateConstraintList(step, "modifiers", stepData.Modifiers);
            PopulateConstraintList(step, "constraints", stepData.Constraints);
            PopulateConstraintList(step, "postConstraints", stepData.PostConstraints);

            if (stepData.Properties.Count == 0 &&
                stepData.Modifiers.Count == 0 &&
                stepData.Constraints.Count == 0 &&
                stepData.PostConstraints.Count == 0)
            {
                DaLog.OnceWarn(
                    "step-empty:" + stepType.FullName,
                    "Step exported without editable fields: " + stepType.FullName);
            }

            return stepData;
        }

        private static void PopulateConstraintList(LevelGenStep step, string fieldName, List<DaConstraintData> targetList)
        {
            FieldInfo field = step.GetType().GetField(fieldName, InstanceFieldFlags);
            if (field == null)
            {
                return;
            }

            if (!(field.GetValue(step) is IEnumerable enumerable))
            {
                return;
            }

            foreach (object item in enumerable)
            {
                if (item == null)
                {
                    continue;
                }

                Type itemType = item.GetType();
                List<DaPropertyData> properties = BuildPropertyList(itemType, item);
                if (properties.Count == 0)
                {
                    if (!s_modConstraintWhitelist.Contains(itemType.Name))
                    {
                        DaLog.OnceWarn(
                            "constraint-no-properties:" + itemType.FullName,
                            "Encountered modifier/constraint type without exportable fields: " + itemType.FullName);
                    }

                    continue;
                }

                targetList.Add(new DaConstraintData
                {
                    Type = itemType.Name,
                    SourceObject = item,
                    Properties = properties
                });
            }
        }

        private static List<DaPropertyData> BuildPropertyList(Type sourceType, object source)
        {
            List<DaPropertyData> properties = new List<DaPropertyData>();
            FieldInfo[] fields = sourceType.GetFields(InstanceFieldFlags);
            Array.Sort(fields, (left, right) => string.CompareOrdinal(left.Name, right.Name));

            foreach (FieldInfo field in fields)
            {
                if (!ShouldCaptureField(field))
                {
                    continue;
                }

                if (!TryGetFieldValue(field, source, out object value))
                {
                    continue;
                }

                properties.Add(new DaPropertyData
                {
                    Name = field.Name,
                    Value = value,
                    InitialValue = value
                });
            }

            return properties;
        }

        private static List<Transform> ResolveSegmentRoots(MapHandler.MapSegment segment, int index)
        {
            List<Transform> roots = new List<Transform>();
            GameObject parentObject = segment.segmentParent;
            if (parentObject == null)
            {
                DaLog.OnceWarn("segment-null-parent:" + index, "Segment " + index + " has no segment parent.");
                return roots;
            }

            Transform parent = parentObject.transform;
            string segmentName = parentObject.name ?? ("Segment_" + index);

            if (IsDirectSegmentRoot(segmentName))
            {
                roots.Add(parent);
                return roots;
            }

            for (int childIndex = 0; childIndex < parent.childCount; childIndex++)
            {
                Transform child = parent.GetChild(childIndex);
                if (!child.gameObject.activeSelf)
                {
                    continue;
                }

                if (HasComponentNamed(child, "BiomeVariant"))
                {
                    roots.Add(child);
                }
            }

            if (roots.Count > 0)
            {
                return roots;
            }

            int totalGrouperCount = parent.GetComponentsInChildren<PropGrouper>(true).Length;
            if (totalGrouperCount > 0)
            {
                DaLog.OnceWarn(
                    "segment-no-biome-variant:" + segmentName,
                    "No active BiomeVariant was found under " + segmentName + "; using whole segment parent with variant-branch filtering. totalGroupers=" + totalGrouperCount);
                roots.Add(parent);
                return roots;
            }

            DaLog.OnceWarn(
                "segment-root-fallback:" + segmentName,
                "No active BiomeVariant or prop groupers were found under " + segmentName + ".");
            return roots;
        }

        private static List<PropGrouper> GetUniqueGroupers(List<Transform> roots)
        {
            List<PropGrouper> groupers = new List<PropGrouper>();
            HashSet<int> seen = new HashSet<int>();

            foreach (Transform root in roots)
            {
                if (root == null)
                {
                    continue;
                }

                PropGrouper[] found = root.GetComponentsInChildren<PropGrouper>(true);
                for (int index = 0; index < found.Length; index++)
                {
                    PropGrouper grouper = found[index];
                if (grouper == null || IsUnderInactiveVariantBranch(grouper.transform, root))
                {
                    continue;
                }

                    int id = grouper.GetInstanceID();
                    if (seen.Add(id))
                    {
                        groupers.Add(grouper);
                    }
                }
            }

            return groupers;
        }

        private static bool IsUnderInactiveVariantBranch(Transform transform, Transform root)
        {
            Transform current = transform;
            while (current != null && current != root)
            {
                if (!current.gameObject.activeSelf && IsVariantBranchTransform(current))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static bool IsVariantBranchTransform(Transform transform)
        {
            return transform != null &&
                   (HasComponentNamed(transform, "VariantObject") ||
                    HasComponentNamed(transform, "BiomeVariant") ||
                    IsVariantBranchName(transform.name));
        }

        private static bool IsVariantBranchName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            return name.StartsWith("-", StringComparison.Ordinal) &&
                   name.IndexOf("Variant", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsRootTransform(Transform transform, List<Transform> roots)
        {
            for (int index = 0; index < roots.Count; index++)
            {
                if (transform == roots[index])
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasLevelGenStepAncestor(Transform transform, List<Transform> roots)
        {
            Transform current = transform != null ? transform.parent : null;
            while (current != null)
            {
                if (IsRootTransform(current, roots))
                {
                    return false;
                }

                if (current.GetComponent<LevelGenStep>() != null)
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static string GetSegmentName(MapHandler.MapSegment segment, int index)
        {
            GameObject parentObject = segment.segmentParent;
            if (parentObject != null && !string.IsNullOrWhiteSpace(parentObject.name))
            {
                return parentObject.name;
            }

            return "Segment_" + index;
        }

        private static bool HasComponentNamed(Component component, string typeName)
        {
            Component[] components = component.GetComponents<Component>();
            for (int index = 0; index < components.Length; index++)
            {
                Component current = components[index];
                if (current != null && string.Equals(current.GetType().Name, typeName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsDirectSegmentRoot(string segmentName)
        {
            return segmentName.StartsWith("Volcano", StringComparison.OrdinalIgnoreCase) ||
                   segmentName.StartsWith("Caldera", StringComparison.OrdinalIgnoreCase) ||
                   segmentName.StartsWith("Desert", StringComparison.OrdinalIgnoreCase);
        }

        private static int FindLevelSlot(MapHandler.MapSegment sourceSegment)
        {
            if (sourceSegment == null || !MapHandler.Exists)
            {
                return -1;
            }

            MapHandler handler = Singleton<MapHandler>.Instance;
            if (handler == null || handler.segments == null)
            {
                return -1;
            }

            for (int index = 0; index < handler.segments.Length; index++)
            {
                if (ReferenceEquals(handler.segments[index], sourceSegment))
                {
                    return index;
                }
            }

            return -1;
        }

        private static void FillRootPaths(DaSegmentData segmentData, List<Transform> roots)
        {
            if (segmentData == null)
            {
                return;
            }

            segmentData.RootPaths.Clear();
            if (roots == null)
            {
                return;
            }

            for (int index = 0; index < roots.Count; index++)
            {
                Transform root = roots[index];
                string path = GetHierarchyPath(root);
                if (!string.IsNullOrWhiteSpace(path))
                {
                    segmentData.RootPaths.Add(path);
                }
            }
        }

        private static void FillVariantMetadata(DaSegmentData segmentData, MapHandler.MapSegment sourceSegment, List<Transform> roots)
        {
            if (segmentData == null)
            {
                return;
            }

            segmentData.VariantSelectionType = GetVariantSelectionType(sourceSegment);
            segmentData.ActiveVariantNames.Clear();
            segmentData.ActiveVariantPaths.Clear();
            segmentData.NormalizedVariantName = null;

            if (roots == null)
            {
                return;
            }

            bool addedFromRoots = false;
            for (int index = 0; index < roots.Count; index++)
            {
                Transform root = roots[index];
                if (root == null)
                {
                    continue;
                }

                if (HasComponentNamed(root, "BiomeVariant") || HasComponentNamed(root, "VariantObject"))
                {
                    segmentData.ActiveVariantNames.Add(root.name ?? string.Empty);
                    segmentData.ActiveVariantPaths.Add(GetHierarchyPath(root));
                    addedFromRoots = true;
                }
            }

            if (!addedFromRoots && sourceSegment != null && sourceSegment.segmentParent != null)
            {
                CollectActiveVariantNodes(
                    sourceSegment.segmentParent.transform,
                    "VariantObject",
                    segmentData.ActiveVariantNames,
                    segmentData.ActiveVariantPaths);
            }

            segmentData.NormalizedVariantName = NormalizeVariantName(segmentData);
        }

        private static void FinalizeVariantMetadata(DaSegmentData segmentData)
        {
            if (segmentData == null || string.IsNullOrWhiteSpace(segmentData.SegmentName))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(segmentData.NormalizedVariantName))
            {
                return;
            }

            if (!s_variantRules.TryGetValue(segmentData.SegmentName, out VariantRule rule) ||
                rule == null ||
                rule.Variants == null ||
                rule.Variants.Length == 0)
            {
                return;
            }

            if (TryMatchVariantFromExportedHierarchy(segmentData, rule, out string matchedVariant, out string matchedName, out string matchedPath))
            {
                segmentData.NormalizedVariantName = matchedVariant;
                AddDistinct(segmentData.ActiveVariantNames, matchedName);
                AddDistinct(segmentData.ActiveVariantPaths, matchedPath);
                return;
            }

            if (string.Equals(segmentData.VariantSelectionType, "VariantObject", StringComparison.Ordinal))
            {
                segmentData.NormalizedVariantName = rule.Variants[0];
                return;
            }
        }

        private static string GetVariantSelectionType(MapHandler.MapSegment sourceSegment)
        {
            if (sourceSegment == null || sourceSegment.segmentParent == null)
            {
                return "none";
            }

            Transform parent = sourceSegment.segmentParent.transform;
            bool hasBiomeVariant = false;
            bool hasVariantObject = false;
            for (int childIndex = 0; childIndex < parent.childCount; childIndex++)
            {
                Transform child = parent.GetChild(childIndex);
                if (child == null)
                {
                    continue;
                }

                if (HasComponentNamed(child, "BiomeVariant"))
                {
                    hasBiomeVariant = true;
                }

                if (HasComponentNamedInChildren(child, "VariantObject"))
                {
                    hasVariantObject = true;
                }
            }

            if (hasBiomeVariant)
            {
                return "BiomeVariant";
            }

            if (hasVariantObject)
            {
                return "VariantObject";
            }

            return "DirectSegmentRoot";
        }

        private static string NormalizeVariantName(DaSegmentData segmentData)
        {
            if (segmentData == null || string.IsNullOrWhiteSpace(segmentData.SegmentName))
            {
                return null;
            }

            if (!s_variantRules.TryGetValue(segmentData.SegmentName, out VariantRule rule) ||
                rule == null ||
                rule.Variants == null ||
                rule.Variants.Length == 0)
            {
                return null;
            }

            List<string> activeNames = segmentData.ActiveVariantNames ?? new List<string>();
            List<string> activePaths = segmentData.ActiveVariantPaths ?? new List<string>();

            for (int variantIndex = 0; variantIndex < rule.Variants.Length; variantIndex++)
            {
                string variant = rule.Variants[variantIndex];
                if (string.IsNullOrWhiteSpace(variant))
                {
                    continue;
                }

                if (IsVariantMatch(variant, activeNames, activePaths))
                {
                    return variant;
                }
            }

            return string.Empty;
        }

        private static bool IsVariantMatch(string variant, List<string> activeNames, List<string> activePaths)
        {
            string variantToken = NormalizeVariantToken(variant);
            if (string.IsNullOrWhiteSpace(variantToken))
            {
                return false;
            }

            for (int index = 0; index < activeNames.Count; index++)
            {
                string current = NormalizeVariantToken(activeNames[index]);
                if (!string.IsNullOrWhiteSpace(current) && current.IndexOf(variantToken, StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            for (int index = 0; index < activePaths.Count; index++)
            {
                string current = NormalizeVariantToken(activePaths[index]);
                if (!string.IsNullOrWhiteSpace(current) && current.IndexOf(variantToken, StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryMatchVariantFromExportedHierarchy(
            DaSegmentData segmentData,
            VariantRule rule,
            out string matchedVariant,
            out string matchedName,
            out string matchedPath)
        {
            matchedVariant = null;
            matchedName = null;
            matchedPath = null;

            if (segmentData == null || rule == null || rule.Variants == null || segmentData.Groupers == null)
            {
                return false;
            }

            for (int variantIndex = 0; variantIndex < rule.Variants.Length; variantIndex++)
            {
                string variant = rule.Variants[variantIndex];
                if (string.IsNullOrWhiteSpace(variant))
                {
                    continue;
                }

                for (int grouperIndex = 0; grouperIndex < segmentData.Groupers.Count; grouperIndex++)
                {
                    DaPropGrouperData grouper = segmentData.Groupers[grouperIndex];
                    if (grouper == null)
                    {
                        continue;
                    }

                    if (IsVariantTokenMatch(variant, grouper.GrouperName) ||
                        IsVariantTokenMatch(variant, grouper.HierarchyPath))
                    {
                        matchedVariant = variant;
                        matchedName = grouper.GrouperName;
                        matchedPath = grouper.HierarchyPath;
                        return true;
                    }

                    if (grouper.Steps == null)
                    {
                        continue;
                    }

                    for (int stepIndex = 0; stepIndex < grouper.Steps.Count; stepIndex++)
                    {
                        DaLevelGenStepData step = grouper.Steps[stepIndex];
                        if (step == null)
                        {
                            continue;
                        }

                        if (IsVariantTokenMatch(variant, step.StepName) ||
                            IsVariantTokenMatch(variant, step.HierarchyPath))
                        {
                            matchedVariant = variant;
                            matchedName = step.StepName;
                            matchedPath = step.HierarchyPath;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static bool IsVariantTokenMatch(string variant, string candidate)
        {
            string variantToken = NormalizeVariantToken(variant);
            string candidateToken = NormalizeVariantToken(candidate);
            return !string.IsNullOrWhiteSpace(variantToken) &&
                   !string.IsNullOrWhiteSpace(candidateToken) &&
                   candidateToken.IndexOf(variantToken, StringComparison.Ordinal) >= 0;
        }

        private static void AddDistinct(List<string> target, string value)
        {
            if (target == null || string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            for (int index = 0; index < target.Count; index++)
            {
                if (string.Equals(target[index], value, StringComparison.Ordinal))
                {
                    return;
                }
            }

            target.Add(value);
        }

        private static string NormalizeVariantToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            char[] chars = value.ToLowerInvariant().ToCharArray();
            List<char> result = new List<char>(chars.Length);
            for (int index = 0; index < chars.Length; index++)
            {
                char current = chars[index];
                if (char.IsLetterOrDigit(current))
                {
                    result.Add(current);
                }
            }

            return new string(result.ToArray());
        }

        private static void CollectActiveVariantNodes(Transform root, string typeName, List<string> names, List<string> paths)
        {
            if (root == null || string.IsNullOrWhiteSpace(typeName) || names == null || paths == null)
            {
                return;
            }

            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            Component[] components = root.GetComponentsInChildren<Component>(true);
            for (int index = 0; index < components.Length; index++)
            {
                Component component = components[index];
                if (component == null || !string.Equals(component.GetType().Name, typeName, StringComparison.Ordinal))
                {
                    continue;
                }

                Transform transform = component.transform;
                if (transform == null || !transform.gameObject.activeSelf)
                {
                    continue;
                }

                string path = GetHierarchyPath(transform);
                if (string.IsNullOrWhiteSpace(path) || !seen.Add(path))
                {
                    continue;
                }

                names.Add(transform.name ?? string.Empty);
                paths.Add(path);
            }
        }

        private static bool HasComponentNamedInChildren(Transform root, string typeName)
        {
            if (root == null || string.IsNullOrWhiteSpace(typeName))
            {
                return false;
            }

            Component[] components = root.GetComponentsInChildren<Component>(true);
            for (int index = 0; index < components.Length; index++)
            {
                Component current = components[index];
                if (current != null && string.Equals(current.GetType().Name, typeName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ShouldCaptureField(FieldInfo field)
        {
            if (field == null || field.IsStatic || field.IsInitOnly)
            {
                return false;
            }

            if (field.Name.StartsWith("<", StringComparison.Ordinal) ||
                field.Name.StartsWith("_", StringComparison.Ordinal) ||
                string.Equals(field.Name, "currentSpawns", StringComparison.Ordinal))
            {
                return false;
            }

            if (s_propertyWhitelist.Contains(field.Name))
            {
                return true;
            }

            Type fieldType = field.FieldType;
            if (typeof(UnityEngine.Object).IsAssignableFrom(fieldType))
            {
                return false;
            }

            if (fieldType.IsArray)
            {
                return false;
            }

            if (typeof(IEnumerable).IsAssignableFrom(fieldType) && fieldType != typeof(string))
            {
                return false;
            }

            return IsSerializableLeafType(fieldType);
        }

        private static bool TryGetFieldValue(FieldInfo field, object source, out object value)
        {
            value = null;

            try
            {
                object rawValue = field.GetValue(source);
                if (rawValue == null)
                {
                    return false;
                }

                value = rawValue;
                return true;
            }
            catch (Exception ex)
            {
                DaLog.OnceWarn(
                    "field-read-failed:" + field.DeclaringType.FullName + ":" + field.Name,
                    "Failed to read field " + field.Name + " from " + field.DeclaringType.FullName + ": " + ex.Message);
                return false;
            }
        }

        private static bool IsSerializableLeafType(Type type)
        {
            if (type.IsPrimitive || type.IsEnum)
            {
                return true;
            }

            return type == typeof(string) ||
                   type == typeof(decimal) ||
                   type == typeof(Vector2) ||
                   type == typeof(Vector3) ||
                   type == typeof(Vector4) ||
                   type == typeof(Vector2Int) ||
                   type == typeof(Vector3Int) ||
                   type == typeof(Quaternion) ||
                   type == typeof(Color) ||
                   type == typeof(Bounds) ||
                   type == typeof(Rect) ||
                   type == typeof(LayerMask);
        }

        private static int CompareByHierarchyPath(PropGrouper left, PropGrouper right)
        {
            return string.CompareOrdinal(GetHierarchyPath(left != null ? left.transform : null), GetHierarchyPath(right != null ? right.transform : null));
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            List<string> nodes = new List<string>();
            while (transform != null)
            {
                nodes.Add(transform.name);
                transform = transform.parent;
            }

            nodes.Reverse();
            return string.Join("/", nodes.ToArray());
        }

        private static string BuildMapKey(List<DaSegmentData> segments)
        {
            if (segments == null || segments.Count == 0)
            {
                return "unknown";
            }

            List<string> names = new List<string>();
            for (int index = 0; index < segments.Count; index++)
            {
                string name = segments[index] != null ? segments[index].SegmentName : null;
                names.Add(SanitizeMapKeyPart(string.IsNullOrWhiteSpace(name) ? ("segment-" + index) : name));
            }

            return string.Join("__", names.ToArray());
        }

        private static string SanitizeMapKeyPart(string value)
        {
            char[] chars = value.ToCharArray();
            for (int index = 0; index < chars.Length; index++)
            {
                char current = chars[index];
                if (!char.IsLetterOrDigit(current))
                {
                    chars[index] = '-';
                }
            }

            string sanitized = new string(chars).Trim('-');
            while (sanitized.Contains("--"))
            {
                sanitized = sanitized.Replace("--", "-");
            }

            return string.IsNullOrWhiteSpace(sanitized) ? "segment" : sanitized;
        }
    }
}


