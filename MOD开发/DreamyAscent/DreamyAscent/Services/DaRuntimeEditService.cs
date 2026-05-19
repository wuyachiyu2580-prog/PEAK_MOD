using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using DreamyAscent.Data;
using DreamyAscent.Helpers;
using UnityEngine;

namespace DreamyAscent.Services
{
    internal static class DaRuntimeEditService
    {
        private const BindingFlags InstanceFieldFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const bool GenerationTraceEnabled = true;
        private const int GenerationTraceMaxStackFrames = 14;
        private const int GenerationTraceMaxStepChanges = 32;
        private const float RuntimeSpawnCleanupRadius = 4f;
        private const float RuntimeSingleItemSpawnCleanupRadius = 8f;
        private const float RuntimeCoconutSpawnCleanupRadius = 18f;
        private const int RuntimeSpawnDiagnosticMaxMatchedItems = 12;
        private const int RuntimeSpawnDiagnosticMaxNearbyItems = 12;
        private const int RuntimePostGenerationDiagnosticMaxItems = 20;
        private static readonly HashSet<string> CustomBlankPreservedStepNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "Start"
        };
        private static readonly Dictionary<string, HashSet<string>> SkippedOfficialGrouperStepWhitelist = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        {
            [BuildSkippedGrouperKey("Jungle_Segment", "Rocks_Plat")] = new HashSet<string>(StringComparer.Ordinal)
            {
                "ErikSpawner",
                "Rocks"
            },
            [BuildSkippedGrouperKey("Jungle_Segment", "Rocks_Wall")] = new HashSet<string>(StringComparer.Ordinal)
            {
                "Spires",
                "Waterfalls",
                "Pillars"
            },
            [BuildSkippedGrouperKey("Roots Segment", "PlateauRocks")] = new HashSet<string>(StringComparer.Ordinal)
            {
                "Connecting rocks",
                "ErikSpawner",
                "Rings",
                "Small Rocks"
            },
            [BuildSkippedGrouperKey("Roots Segment", "WallRocks")] = new HashSet<string>(StringComparer.Ordinal)
            {
                "Roots",
                "Shelf Shrooms",
                "Caves",
                "Big Redwood",
                "Pillars"
            }
        };

        public static bool TrySetStepProperty(DaLevelGenStepData step, DaPropertyData property, string rawValue)
        {
            if (step == null || step.SourceObject == null)
            {
                DaLog.Warn("Cannot edit step property because runtime step reference is missing.");
                return false;
            }

            return TrySetProperty(step.SourceObject, step.StepName, property, rawValue);
        }

        public static bool TrySetConstraintProperty(DaConstraintData constraint, DaPropertyData property, string rawValue)
        {
            if (constraint == null || constraint.SourceObject == null)
            {
                DaLog.Warn("Cannot edit constraint property because runtime constraint reference is missing.");
                return false;
            }

            return TrySetProperty(constraint.SourceObject, constraint.Type, property, rawValue);
        }

        public static bool RunGrouper(DaPropGrouperData grouper)
        {
            return RunGrouper(grouper, null, "manual-grouper");
        }

        private static bool RunGrouper(DaPropGrouperData grouper, DaSegmentData segment, string context)
        {
            if (grouper == null || grouper.SourceObject == null)
            {
                DaLog.Warn("Cannot generate grouper because runtime grouper reference is missing.");
                return false;
            }

            try
            {
                GrouperGenerationSnapshot before = BuildGrouperGenerationSnapshot(grouper);
                LogGrouperGenerationSnapshot("before", segment, grouper, before, context);
                ClearSpawnedRuntimeItemsBeforeGeneration(segment, grouper, context);
                ClearSpawnedRuntimeRopesBeforeGeneration(segment, grouper, context);
                if (!RunGrouperGeneration(grouper.SourceObject))
                {
                    return false;
                }

                int assignedViewIds = AssignUnassignedPhotonViewIdsForGrouper(grouper, segment, context);
                RefreshRuntimeSpawnsAfterGeneration(segment, grouper, context);
                if (assignedViewIds > 0)
                {
                    TrySendOutgoingPhotonCommands();
                }

                UnityEngine.Physics.SyncTransforms();
                GrouperGenerationSnapshot after = BuildGrouperGenerationSnapshot(grouper);
                LogGrouperGenerationSnapshot("after", segment, grouper, after, context);
                LogGrouperGenerationDelta(segment, grouper, before, after, context);
                DaLog.Info("Generated grouper: " + grouper.GrouperName);
                return true;
            }
            catch (Exception ex)
            {
                DaLog.Error("Failed to generate grouper " + grouper.GrouperName + ": " + ex);
                return false;
            }
        }

        public static int RunSegment(DaSegmentData segment)
        {
            int generated = 0;
            if (segment == null || segment.Groupers == null)
            {
                return generated;
            }

            LogSegmentGenerationTraceEnter(segment);

            if (NeedsRuntimeReferenceRebind(segment))
            {
                DaTerrainExportService.TryBindSegmentRuntimeReferences(segment);
                DaLog.Info("Generation trace: runtime references rebound. segment=" + segment.SegmentName +
                    ", missingGroupers=" + CountMissingRuntimeGroupers(segment).ToString(CultureInfo.InvariantCulture) +
                    ", missingSteps=" + CountMissingRuntimeSteps(segment).ToString(CultureInfo.InvariantCulture));
            }

            if (segment.EditMode == DaSegmentEditMode.CustomBlank)
            {
                int removed = ClearSegmentRuntimeChildren(segment);
                UnityEngine.Physics.SyncTransforms();
                DaDiagnosticService.WriteCustomBlankRemaining(DaTerrainExportService.LastExportedTerrain, segment);
                DaLog.Info(string.Format(
                    "Applied custom blank segment: {0}, removed={1}",
                    segment.SegmentName,
                    removed));
                return removed;
            }

            SegmentGenerationSnapshot beforeSegment = BuildSegmentGenerationSnapshot(segment);
            generated = RunOfficialSegment(segment);
            SegmentGenerationSnapshot afterSegment = BuildSegmentGenerationSnapshot(segment);
            LogSegmentGenerationDelta(segment, beforeSegment, afterSegment, "official-segment");

            DaLog.Info(string.Format(
                "Generated segment: {0}, groupers={1}",
                segment.SegmentName,
                generated));
            return generated;
        }

        private static bool NeedsRuntimeReferenceRebind(DaSegmentData segment)
        {
            if (segment == null)
            {
                return false;
            }

            if (segment.SourceSegment == null || segment.SourceRoots == null || segment.SourceRoots.Count == 0)
            {
                return true;
            }

            for (int index = 0; segment.Groupers != null && index < segment.Groupers.Count; index++)
            {
                DaPropGrouperData grouper = segment.Groupers[index];
                if (grouper != null && grouper.SourceObject == null)
                {
                    return true;
                }
            }

            return false;
        }

        private static int RunOfficialSegment(DaSegmentData segment)
        {
            int generated = 0;
            if (segment == null || segment.Groupers == null)
            {
                return generated;
            }

            bool hasReadyBaseline = DaTemplateBaselineService.HasCurrentVariantDefaultTemplate(segment);
            if (hasReadyBaseline)
            {
                DaTemplateBaselineData baseline = DaTemplateBaselineService.GetCurrentVariantDefaultTemplate(segment);
                DaLog.Info("Using current variant default template for segment generation. segment=" + segment.SegmentName +
                    ", variant=" + (baseline != null ? baseline.NormalizedVariantName ?? string.Empty : string.Empty) +
                    ", snapshot=" + (baseline != null ? baseline.SnapshotId : string.Empty) +
                    ", groupers=" + (baseline != null ? baseline.MatchedGrouperCount.ToString(CultureInfo.InvariantCulture) : "0") +
                    "/" + (baseline != null ? baseline.SnapshotGrouperCount.ToString(CultureInfo.InvariantCulture) : "0"));
            }

            for (int index = 0; index < segment.Groupers.Count; index++)
            {
                DaPropGrouperData grouper = segment.Groupers[index];
                if (grouper == null)
                {
                    continue;
                }

                if (hasReadyBaseline && !DaTemplateBaselineService.ContainsRuntimeGrouper(segment, grouper))
                {
                    DaLog.Info("Skipped non-baseline grouper for official segment run. segment=" + segment.SegmentName + ", grouper=" + grouper.GrouperName);
                    continue;
                }

                if (ShouldSkipGrouperForOfficialSegmentRun(segment, grouper))
                {
                    int whitelistedSteps = RunWhitelistedStepsForSkippedOfficialGrouper(segment, grouper);
                    DaLog.Info("Skipped high-risk grouper for official segment run. segment=" + segment.SegmentName + ", grouper=" + grouper.GrouperName +
                        (whitelistedSteps > 0 ? ", whitelistedSteps=" + whitelistedSteps.ToString(CultureInfo.InvariantCulture) : string.Empty));
                    continue;
                }

                if (RunGrouper(grouper, segment, "official-segment"))
                {
                    generated++;
                }
            }

            return generated;
        }

        private static int ClearSpawnedRuntimeItemsBeforeGeneration(DaSegmentData segment, DaPropGrouperData grouper, string context)
        {
            if (grouper == null || grouper.SourceObject == null)
            {
                return 0;
            }

            List<Component> runtimeItems = FindRuntimeSpawnedItemComponents();
            HashSet<GameObject> candidates = new HashSet<GameObject>();
            Dictionary<GameObject, string> matchReasons = new Dictionary<GameObject, string>();
            int trackerMatches = 0;
            int trackerResets = 0;
            int spatialMatches = 0;
            int spawnerCount = 0;

            Component[] components = grouper.SourceObject.GetComponentsInChildren<Component>(true);
            HashSet<Component> processedSpawners = new HashSet<Component>();
            HashSet<Component> processedTrackers = new HashSet<Component>();
            for (int index = 0; components != null && index < components.Length; index++)
            {
                Component component = components[index];
                if (component == null)
                {
                    continue;
                }

                if (IsTypeNamed(component.GetType(), "SpawnedItemTracker") && processedTrackers.Add(component))
                {
                    trackerMatches += CollectTrackedSpawnedItems(component, candidates, matchReasons);
                    if (ResetSpawnTrackerState(component))
                    {
                        trackerResets++;
                    }
                }

                if (!IsKnownRuntimeItemSpawner(component) || !processedSpawners.Add(component))
                {
                    continue;
                }

                spawnerCount++;
                spatialMatches += CollectNearbySpawnedItemsForSpawner(component, runtimeItems, candidates, matchReasons);
            }

            LogRuntimeSpawnCleanupDiagnostics(segment, grouper, context, runtimeItems, candidates, matchReasons);
            int destroyed = DestroyRuntimeItems(candidates, matchReasons);
            if (destroyed == 0)
            {
                LogNearbyRuntimeItemDiagnostics(segment, grouper, runtimeItems);
            }

            if (destroyed > 0 || trackerResets > 0 || spawnerCount > 0)
            {
                DaLog.Info("Generation trace: cleared spawned runtime items before regeneration." +
                    " context=" + (context ?? string.Empty) +
                    ", segment=" + (segment != null ? segment.SegmentName ?? string.Empty : string.Empty) +
                    ", grouper=" + (grouper.GrouperName ?? string.Empty) +
                    ", spawners=" + spawnerCount.ToString(CultureInfo.InvariantCulture) +
                    ", trackerMatches=" + trackerMatches.ToString(CultureInfo.InvariantCulture) +
                    ", spatialMatches=" + spatialMatches.ToString(CultureInfo.InvariantCulture) +
                    ", trackerResets=" + trackerResets.ToString(CultureInfo.InvariantCulture) +
                    ", destroyed=" + destroyed.ToString(CultureInfo.InvariantCulture));
            }

            return destroyed;
        }

        private static int ClearSpawnedRuntimeRopesBeforeGeneration(DaSegmentData segment, DaPropGrouperData grouper, string context)
        {
            if (grouper == null || grouper.SourceObject == null)
            {
                return 0;
            }

            Component[] components = grouper.SourceObject.GetComponentsInChildren<Component>(true);
            HashSet<GameObject> candidates = new HashSet<GameObject>();
            Dictionary<GameObject, string> matchReasons = new Dictionary<GameObject, string>();
            HashSet<Component> processedAnchors = new HashSet<Component>();
            int anchorCount = 0;
            int fieldMatches = 0;
            int attachedMatches = 0;

            for (int index = 0; components != null && index < components.Length; index++)
            {
                Component component = components[index];
                if (component == null || !IsTypeNamed(component.GetType(), "RopeAnchorWithRope") || !processedAnchors.Add(component))
                {
                    continue;
                }

                anchorCount++;
                GameObject ropeInstance = GetRopeAnchorWithRopeInstance(component);
                if (IsRuntimeSpawnedRopeObject(ropeInstance) && candidates.Add(ropeInstance))
                {
                    AddRuntimeSpawnMatchReason(matchReasons, ropeInstance, "ropeAnchorField:" + GetRuntimeHierarchyPath(component.transform));
                    fieldMatches++;
                }

                Component anchor = GetRopeAnchorComponent(component);
                attachedMatches += CollectRopesAttachedToAnchor(anchor, candidates, matchReasons, component);
                ResetRopeAnchorWithRopeRuntimeFields(component);
            }

            int destroyed = DestroyRuntimeRopeObjects(candidates, matchReasons);
            if (destroyed > 0 || anchorCount > 0)
            {
                DaLog.Info("Generation trace: cleared spawned runtime ropes before regeneration." +
                    " context=" + (context ?? string.Empty) +
                    ", segment=" + (segment != null ? segment.SegmentName ?? string.Empty : string.Empty) +
                    ", grouper=" + (grouper.GrouperName ?? string.Empty) +
                    ", anchors=" + anchorCount.ToString(CultureInfo.InvariantCulture) +
                    ", fieldMatches=" + fieldMatches.ToString(CultureInfo.InvariantCulture) +
                    ", attachedMatches=" + attachedMatches.ToString(CultureInfo.InvariantCulture) +
                    ", destroyed=" + destroyed.ToString(CultureInfo.InvariantCulture));
            }

            return destroyed;
        }

        private static void RefreshRuntimeSpawnsAfterGeneration(DaSegmentData segment, DaPropGrouperData grouper, string context)
        {
            if (grouper == null || grouper.SourceObject == null)
            {
                return;
            }

            bool canSpawn = CanRunMasterRuntimeSpawns();
            int itemSpawnerCount = 0;
            int itemSpawnedViews = 0;
            int ropeAnchorCount = 0;
            int ropeSpawned = 0;
            int ropeExisting = 0;
            int ropeAssignedViewIds = 0;

            if (canSpawn)
            {
                itemSpawnedViews = TrySpawnRuntimeItemsAfterGeneration(grouper, out itemSpawnerCount);
                ropeSpawned = TrySpawnRuntimeRopesAfterGeneration(grouper, out ropeAnchorCount, out ropeExisting, out ropeAssignedViewIds);
                if (itemSpawnedViews > 0 || ropeSpawned > 0)
                {
                    TrySendOutgoingPhotonCommands();
                }
            }
            else
            {
                itemSpawnerCount = CountRuntimeItemSpawners(grouper);
                ropeAnchorCount = CountRopeAnchorsWithRope(grouper);
            }

            DaLog.Info("Generation trace: refreshed runtime spawns after generation." +
                " context=" + (context ?? string.Empty) +
                ", segment=" + (segment != null ? segment.SegmentName ?? string.Empty : string.Empty) +
                ", grouper=" + (grouper.GrouperName ?? string.Empty) +
                ", canSpawn=" + (canSpawn ? "true" : "false") +
                ", itemSpawners=" + itemSpawnerCount.ToString(CultureInfo.InvariantCulture) +
                ", itemSpawnedViews=" + itemSpawnedViews.ToString(CultureInfo.InvariantCulture) +
                ", ropeAnchors=" + ropeAnchorCount.ToString(CultureInfo.InvariantCulture) +
                ", ropeExisting=" + ropeExisting.ToString(CultureInfo.InvariantCulture) +
                ", ropeAssignedViewIds=" + ropeAssignedViewIds.ToString(CultureInfo.InvariantCulture) +
                ", ropeSpawned=" + ropeSpawned.ToString(CultureInfo.InvariantCulture));

            LogPostGenerationRuntimeSpawnDiagnostics(segment, grouper, context);
        }

        private static int AssignUnassignedPhotonViewIdsForGrouper(DaPropGrouperData grouper, DaSegmentData segment, string context)
        {
            if (grouper == null || grouper.SourceObject == null || !CanRunMasterRuntimeSpawns())
            {
                return 0;
            }

            int assigned = 0;
            Type photonViewType = FindTypeByName("PhotonView");
            if (photonViewType == null)
            {
                return 0;
            }

            Component[] photonViews = grouper.SourceObject.GetComponentsInChildren(photonViewType, true) as Component[];
            for (int index = 0; photonViews != null && index < photonViews.Length; index++)
            {
                Component photonView = photonViews[index];
                if (photonView == null || photonView.gameObject == null || !photonView.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (EnsurePhotonViewId(photonView.gameObject))
                {
                    assigned++;
                }
            }

            if (assigned > 0)
            {
                DaLog.Info("Generation trace: assigned PhotonView ids after grouper generation." +
                    " context=" + (context ?? string.Empty) +
                    ", segment=" + (segment != null ? segment.SegmentName ?? string.Empty : string.Empty) +
                    ", grouper=" + (grouper.GrouperName ?? string.Empty) +
                    ", assigned=" + assigned.ToString(CultureInfo.InvariantCulture));
            }

            return assigned;
        }

        private static int TrySpawnRuntimeItemsAfterGeneration(DaPropGrouperData grouper, out int spawnerCount)
        {
            spawnerCount = 0;
            if (grouper == null || grouper.SourceObject == null)
            {
                return 0;
            }

            int spawnedViews = 0;
            Component[] components = grouper.SourceObject.GetComponentsInChildren<Component>(true);
            HashSet<Component> processedSpawners = new HashSet<Component>();
            for (int index = 0; components != null && index < components.Length; index++)
            {
                Component component = components[index];
                if (!ShouldRefreshRuntimeItemSpawner(component) || !processedSpawners.Add(component))
                {
                    continue;
                }

                spawnerCount++;
                try
                {
                    MethodInfo method = component.GetType().GetMethod("TrySpawnItems", InstanceFieldFlags);
                    object result = method != null ? method.Invoke(component, null) : null;
                    int count = CountEnumerableItems(result as IEnumerable);
                    spawnedViews += count;
                    if (count > 0)
                    {
                        DaLog.Info("Generation trace: runtime item spawner refreshed after generation." +
                            " spawner=" + GetRuntimeHierarchyPath(component.transform) +
                            ", spawnedViews=" + count.ToString(CultureInfo.InvariantCulture));
                    }
                }
                catch (Exception ex)
                {
                    DaLog.OnceWarn(
                        "runtime-item-spawn-refresh-failed:" + GetRuntimeHierarchyPath(component.transform),
                        "Failed to refresh runtime item spawner after generation: " + component.name + ": " + ex.Message);
                }
            }

            return spawnedViews;
        }

        private static int TrySpawnRuntimeRopesAfterGeneration(
            DaPropGrouperData grouper,
            out int anchorCount,
            out int existingRopes,
            out int assignedViewIds)
        {
            anchorCount = 0;
            existingRopes = 0;
            assignedViewIds = 0;
            if (grouper == null || grouper.SourceObject == null)
            {
                return 0;
            }

            int spawned = 0;
            Component[] components = grouper.SourceObject.GetComponentsInChildren<Component>(true);
            HashSet<Component> processedAnchors = new HashSet<Component>();
            for (int index = 0; components != null && index < components.Length; index++)
            {
                Component component = components[index];
                if (component == null ||
                    !component.gameObject.activeInHierarchy ||
                    !IsTypeNamed(component.GetType(), "RopeAnchorWithRope") ||
                    !processedAnchors.Add(component))
                {
                    continue;
                }

                anchorCount++;
                GameObject existing = GetRopeAnchorWithRopeInstance(component);
                if (IsRuntimeSpawnedRopeObject(existing))
                {
                    existingRopes++;
                    continue;
                }

                if (EnsurePhotonViewId(component.gameObject))
                {
                    assignedViewIds++;
                }

                try
                {
                    MethodInfo method = component.GetType().GetMethod("SpawnRope", InstanceFieldFlags);
                    object result = method != null ? method.Invoke(component, null) : null;
                    GameObject ropeObject = GetObjectGameObject(result) ?? GetRopeAnchorWithRopeInstance(component);
                    if (IsRuntimeSpawnedRopeObject(ropeObject))
                    {
                        spawned++;
                        DaLog.Info("Generation trace: runtime rope refreshed after generation." +
                            " anchor=" + GetRuntimeHierarchyPath(component.transform) +
                            ", rope=" + DescribeRuntimeRopeObject(ropeObject, grouper));
                    }
                    else
                    {
                        DaLog.Info("Generation trace: runtime rope refresh produced no rope." +
                            " anchor=" + GetRuntimeHierarchyPath(component.transform) +
                            ", photonView=" + DescribePhotonView(FindComponentByTypeName(component.gameObject, "PhotonView")));
                    }
                }
                catch (Exception ex)
                {
                    DaLog.OnceWarn(
                        "runtime-rope-refresh-failed:" + GetRuntimeHierarchyPath(component.transform),
                        "Failed to refresh runtime rope after generation: " + component.name + ": " + ex.Message);
                }
            }

            return spawned;
        }

        private static bool ShouldRefreshRuntimeItemSpawner(Component component)
        {
            if (component == null ||
                !component.gameObject.activeInHierarchy ||
                !IsKnownRuntimeItemSpawner(component) ||
                IsTypeNamed(component.GetType(), "Luggage"))
            {
                return false;
            }

            return component.GetType().GetMethod("TrySpawnItems", InstanceFieldFlags) != null;
        }

        private static int CountRuntimeItemSpawners(DaPropGrouperData grouper)
        {
            if (grouper == null || grouper.SourceObject == null)
            {
                return 0;
            }

            int count = 0;
            Component[] components = grouper.SourceObject.GetComponentsInChildren<Component>(true);
            HashSet<Component> processed = new HashSet<Component>();
            for (int index = 0; components != null && index < components.Length; index++)
            {
                Component component = components[index];
                if (ShouldRefreshRuntimeItemSpawner(component) && processed.Add(component))
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountRopeAnchorsWithRope(DaPropGrouperData grouper)
        {
            if (grouper == null || grouper.SourceObject == null)
            {
                return 0;
            }

            int count = 0;
            Component[] components = grouper.SourceObject.GetComponentsInChildren<Component>(true);
            HashSet<Component> processed = new HashSet<Component>();
            for (int index = 0; components != null && index < components.Length; index++)
            {
                Component component = components[index];
                if (component != null &&
                    IsTypeNamed(component.GetType(), "RopeAnchorWithRope") &&
                    processed.Add(component))
                {
                    count++;
                }
            }

            return count;
        }

        private static void LogPostGenerationRuntimeSpawnDiagnostics(DaSegmentData segment, DaPropGrouperData grouper, string context)
        {
            if (grouper == null || grouper.SourceObject == null)
            {
                return;
            }

            int loggedItems = 0;
            int nearbyItems = 0;
            List<Component> runtimeItems = FindRuntimeSpawnedItemComponents();
            for (int index = 0; index < runtimeItems.Count; index++)
            {
                Component item = runtimeItems[index];
                GameObject itemObject = item != null ? item.gameObject : null;
                if (itemObject == null)
                {
                    continue;
                }

                Component nearestSpawner = FindNearestKnownRuntimeItemSpawner(itemObject.transform, grouper, out float nearestDistance);
                if (nearestSpawner == null || nearestDistance > GetRuntimePostGenerationDiagnosticRadius(nearestSpawner))
                {
                    continue;
                }

                nearbyItems++;
                if (loggedItems < RuntimePostGenerationDiagnosticMaxItems)
                {
                    DaLog.Info("Generation trace: post-generation runtime item." +
                        " context=" + (context ?? string.Empty) +
                        ", segment=" + (segment != null ? segment.SegmentName ?? string.Empty : string.Empty) +
                        ", grouper=" + (grouper.GrouperName ?? string.Empty) +
                        ", item=" + DescribeRuntimeSpawnedItem(itemObject, grouper));
                    loggedItems++;
                }
            }

            int loggedRopes = 0;
            int nearbyRopes = 0;
            List<Component> ropes = FindSceneComponentsByTypeName("Rope");
            for (int index = 0; index < ropes.Count; index++)
            {
                Component rope = ropes[index];
                GameObject ropeObject = rope != null ? rope.gameObject : null;
                if (!IsRuntimeSpawnedRopeObject(ropeObject) || !IsRopeRelevantToGrouper(ropeObject, grouper, out float nearestDistance))
                {
                    continue;
                }

                nearbyRopes++;
                if (loggedRopes < RuntimePostGenerationDiagnosticMaxItems)
                {
                    DaLog.Info("Generation trace: post-generation runtime rope." +
                        " context=" + (context ?? string.Empty) +
                        ", segment=" + (segment != null ? segment.SegmentName ?? string.Empty : string.Empty) +
                        ", grouper=" + (grouper.GrouperName ?? string.Empty) +
                        ", nearestDistance=" + nearestDistance.ToString("F2", CultureInfo.InvariantCulture) +
                        ", rope=" + DescribeRuntimeRopeObject(ropeObject, grouper));
                    loggedRopes++;
                }
            }

            DaLog.Info("Generation trace: post-generation runtime summary." +
                " context=" + (context ?? string.Empty) +
                ", segment=" + (segment != null ? segment.SegmentName ?? string.Empty : string.Empty) +
                ", grouper=" + (grouper.GrouperName ?? string.Empty) +
                ", nearbyRuntimeItems=" + nearbyItems.ToString(CultureInfo.InvariantCulture) +
                ", nearbyRuntimeRopes=" + nearbyRopes.ToString(CultureInfo.InvariantCulture));
        }

        private static float GetRuntimePostGenerationDiagnosticRadius(Component spawner)
        {
            string expectedName = GetExpectedRuntimeSpawnedItemName(spawner);
            return Mathf.Sqrt(GetRuntimeSpawnCleanupRadiusSqr(spawner, expectedName)) + 4f;
        }

        private static int CollectRopesAttachedToAnchor(
            Component anchor,
            HashSet<GameObject> candidates,
            Dictionary<GameObject, string> matchReasons,
            Component ropeAnchorWithRope)
        {
            if (anchor == null || candidates == null)
            {
                return 0;
            }

            int matches = 0;
            List<Component> ropes = FindSceneComponentsByTypeName("Rope");
            for (int index = 0; index < ropes.Count; index++)
            {
                Component rope = ropes[index];
                GameObject ropeObject = rope != null ? rope.gameObject : null;
                if (!IsRuntimeSpawnedRopeObject(ropeObject))
                {
                    continue;
                }

                Component attachedAnchor = GetRopeAttachedAnchor(ropeObject);
                if (attachedAnchor == null || attachedAnchor.gameObject != anchor.gameObject)
                {
                    continue;
                }

                if (candidates.Add(ropeObject))
                {
                    AddRuntimeSpawnMatchReason(
                        matchReasons,
                        ropeObject,
                        "ropeAttachedAnchor:" + GetRuntimeHierarchyPath(ropeAnchorWithRope != null ? ropeAnchorWithRope.transform : anchor.transform));
                    matches++;
                }
            }

            return matches;
        }

        private static int DestroyRuntimeRopeObjects(HashSet<GameObject> candidates, Dictionary<GameObject, string> matchReasons)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return 0;
            }

            int destroyed = 0;
            foreach (GameObject candidate in candidates)
            {
                if (candidate == null)
                {
                    continue;
                }

                bool removed = TryDestroyViaPhoton(candidate);
                if (!removed)
                {
                    UnityEngine.Object.DestroyImmediate(candidate);
                }

                string reason = string.Empty;
                if (matchReasons != null)
                {
                    matchReasons.TryGetValue(candidate, out reason);
                }

                DaLog.Info("Generation trace: runtime rope destroyed before regeneration." +
                    " rope=" + (candidate.name ?? string.Empty) +
                    ", reason=" + (reason ?? string.Empty) +
                    ", viaPhoton=" + (removed ? "true" : "false"));
                destroyed++;
            }

            return destroyed;
        }

        private static bool IsRuntimeSpawnedRopeObject(GameObject gameObject)
        {
            if (gameObject == null || !IsSceneObject(gameObject) || !gameObject.activeInHierarchy)
            {
                return false;
            }

            return HasComponentWithTypeName(gameObject, "Rope") ||
                   (HasComponentWithTypeName(gameObject, "PhotonView") && ContainsText(gameObject.name, "RopeDynamic"));
        }

        private static GameObject GetRopeAnchorWithRopeInstance(Component component)
        {
            FieldInfo field = component != null ? GetFieldInfo(component.GetType(), "ropeInstance") : null;
            return field != null ? field.GetValue(component) as GameObject : null;
        }

        private static Component GetRopeAnchorWithRopeRope(Component component)
        {
            FieldInfo field = component != null ? GetFieldInfo(component.GetType(), "rope") : null;
            return field != null ? field.GetValue(component) as Component : null;
        }

        private static Component GetRopeAnchorComponent(Component ropeAnchorWithRope)
        {
            if (ropeAnchorWithRope == null)
            {
                return null;
            }

            FieldInfo field = GetFieldInfo(ropeAnchorWithRope.GetType(), "anchor");
            Component anchor = field != null ? field.GetValue(ropeAnchorWithRope) as Component : null;
            return anchor ?? FindComponentByTypeName(ropeAnchorWithRope.gameObject, "RopeAnchor");
        }

        private static void ResetRopeAnchorWithRopeRuntimeFields(Component component)
        {
            if (component == null)
            {
                return;
            }

            FieldInfo ropeInstanceField = GetFieldInfo(component.GetType(), "ropeInstance");
            if (ropeInstanceField != null)
            {
                ropeInstanceField.SetValue(component, null);
            }

            FieldInfo ropeField = GetFieldInfo(component.GetType(), "rope");
            if (ropeField != null)
            {
                ropeField.SetValue(component, null);
            }
        }

        private static Component GetRopeAttachedAnchor(GameObject ropeObject)
        {
            Component rope = FindComponentByTypeName(ropeObject, "Rope");
            FieldInfo field = rope != null ? GetFieldInfo(rope.GetType(), "attachedToAnchor") : null;
            return field != null ? field.GetValue(rope) as Component : null;
        }

        private static bool IsRopeRelevantToGrouper(GameObject ropeObject, DaPropGrouperData grouper, out float nearestDistance)
        {
            nearestDistance = float.MaxValue;
            if (ropeObject == null || grouper == null || grouper.SourceObject == null)
            {
                return false;
            }

            Component attachedAnchor = GetRopeAttachedAnchor(ropeObject);
            if (attachedAnchor != null && IsUnderAny(attachedAnchor.transform, new HashSet<Transform> { grouper.SourceObject.transform }))
            {
                nearestDistance = 0f;
                return true;
            }

            Component nearestAnchor = FindNearestRopeAnchorWithRope(ropeObject.transform, grouper, out nearestDistance);
            return nearestAnchor != null && nearestDistance <= 24f;
        }

        private static Component FindNearestRopeAnchorWithRope(Transform transform, DaPropGrouperData grouper, out float nearestDistance)
        {
            nearestDistance = float.MaxValue;
            if (transform == null || grouper == null || grouper.SourceObject == null)
            {
                return null;
            }

            Component nearest = null;
            Component[] components = grouper.SourceObject.GetComponentsInChildren<Component>(true);
            for (int index = 0; components != null && index < components.Length; index++)
            {
                Component component = components[index];
                if (component == null || !IsTypeNamed(component.GetType(), "RopeAnchorWithRope"))
                {
                    continue;
                }

                Vector3 anchorPosition = GetRopeAnchorPosition(component);
                float distance = Vector3.Distance(transform.position, anchorPosition);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = component;
                }
            }

            return nearest;
        }

        private static Vector3 GetRopeAnchorPosition(Component ropeAnchorWithRope)
        {
            Component anchor = GetRopeAnchorComponent(ropeAnchorWithRope);
            FieldInfo anchorPointField = anchor != null ? GetFieldInfo(anchor.GetType(), "anchorPoint") : null;
            Transform anchorPoint = anchorPointField != null ? anchorPointField.GetValue(anchor) as Transform : null;
            if (anchorPoint != null)
            {
                return anchorPoint.position;
            }

            return ropeAnchorWithRope != null ? ropeAnchorWithRope.transform.position : Vector3.zero;
        }

        private static string DescribeRuntimeRopeObject(GameObject ropeObject, DaPropGrouperData grouper)
        {
            if (ropeObject == null)
            {
                return "<null>";
            }

            Component photonView = FindComponentByTypeName(ropeObject, "PhotonView");
            Component attachedAnchor = GetRopeAttachedAnchor(ropeObject);
            Component nearestAnchor = FindNearestRopeAnchorWithRope(ropeObject.transform, grouper, out float nearestDistance);
            return (ropeObject.name ?? string.Empty) +
                ", path=" + GetRuntimeHierarchyPath(ropeObject.transform) +
                ", pos=" + FormatVector3(ropeObject.transform.position) +
                ", photonView=" + DescribePhotonView(photonView) +
                ", attachedAnchor=" + (attachedAnchor != null ? GetRuntimeHierarchyPath(attachedAnchor.transform) : string.Empty) +
                ", nearestAnchor=" + (nearestAnchor != null ? GetRuntimeHierarchyPath(nearestAnchor.transform) : string.Empty) +
                ", nearestDistance=" + (nearestAnchor != null ? nearestDistance.ToString("F2", CultureInfo.InvariantCulture) : string.Empty);
        }

        private static bool EnsurePhotonViewId(GameObject gameObject)
        {
            Component photonView = FindComponentByTypeName(gameObject, "PhotonView");
            if (photonView == null)
            {
                return false;
            }

            PropertyInfo viewIdProperty = photonView.GetType().GetProperty("ViewID", InstanceFieldFlags);
            if (viewIdProperty == null || !viewIdProperty.CanWrite)
            {
                return false;
            }

            int currentViewId = Convert.ToInt32(viewIdProperty.GetValue(photonView, null), CultureInfo.InvariantCulture);
            if (currentViewId != 0)
            {
                return false;
            }

            int viewId = TryAllocatePhotonViewId();
            if (viewId == 0)
            {
                return false;
            }

            viewIdProperty.SetValue(photonView, viewId, null);
            DaLog.Info("Generation trace: assigned PhotonView id for generated runtime object." +
                " object=" + GetRuntimeHierarchyPath(gameObject.transform) +
                ", viewId=" + viewId.ToString(CultureInfo.InvariantCulture));
            return true;
        }

        private static int TryAllocatePhotonViewId()
        {
            if (!CanRunMasterRuntimeSpawns())
            {
                return 0;
            }

            Type photonNetworkType = FindTypeByName("PhotonNetwork");
            if (photonNetworkType == null)
            {
                return 0;
            }

            MethodInfo[] methods = photonNetworkType.GetMethods(BindingFlags.Public | BindingFlags.Static);
            for (int index = 0; methods != null && index < methods.Length; index++)
            {
                MethodInfo method = methods[index];
                if (method == null || !string.Equals(method.Name, "AllocateViewID", StringComparison.Ordinal))
                {
                    continue;
                }

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType == typeof(bool))
                {
                    try
                    {
                        object result = method.Invoke(null, new object[] { false });
                        return result != null ? Convert.ToInt32(result, CultureInfo.InvariantCulture) : 0;
                    }
                    catch (Exception ex)
                    {
                        DaLog.OnceWarn("runtime-spawn-allocate-view-id-failed", "Failed to allocate PhotonView id after runtime generation: " + ex.Message);
                        return 0;
                    }
                }
            }

            return 0;
        }

        private static bool CanRunMasterRuntimeSpawns()
        {
            Type photonNetworkType = FindTypeByName("PhotonNetwork");
            if (photonNetworkType == null)
            {
                return false;
            }

            if (GetStaticBoolProperty(photonNetworkType, "OfflineMode"))
            {
                return true;
            }

            return GetStaticBoolProperty(photonNetworkType, "InRoom") &&
                   GetStaticBoolProperty(photonNetworkType, "IsMasterClient");
        }

        private static bool GetStaticBoolProperty(Type type, string propertyName)
        {
            PropertyInfo property = type != null ? type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static) : null;
            if (property == null || property.PropertyType != typeof(bool))
            {
                return false;
            }

            try
            {
                return (bool)property.GetValue(null, null);
            }
            catch
            {
                return false;
            }
        }

        private static void TrySendOutgoingPhotonCommands()
        {
            try
            {
                Type photonNetworkType = FindTypeByName("PhotonNetwork");
                PropertyInfo networkingClientProperty = photonNetworkType != null
                    ? photonNetworkType.GetProperty("NetworkingClient", BindingFlags.Public | BindingFlags.Static)
                    : null;
                object networkingClient = networkingClientProperty != null ? networkingClientProperty.GetValue(null, null) : null;
                PropertyInfo peerProperty = networkingClient != null
                    ? networkingClient.GetType().GetProperty("LoadBalancingPeer", InstanceFieldFlags)
                    : null;
                object peer = peerProperty != null ? peerProperty.GetValue(networkingClient, null) : null;
                MethodInfo sendMethod = peer != null ? peer.GetType().GetMethod("SendOutgoingCommands", BindingFlags.Public | BindingFlags.Instance) : null;
                sendMethod?.Invoke(peer, null);
            }
            catch (Exception ex)
            {
                DaLog.OnceWarn("runtime-spawn-send-outgoing-commands-failed", "Failed to flush Photon commands after runtime generation: " + ex.Message);
            }
        }

        private static int CountEnumerableItems(IEnumerable items)
        {
            if (items == null)
            {
                return 0;
            }

            int count = 0;
            foreach (object _ in items)
            {
                count++;
            }

            return count;
        }

        private static bool RunGrouperGeneration(PropGrouper grouper)
        {
            if (grouper == null)
            {
                return false;
            }

            if (!VerifyGrouperForRuntimeRun(grouper))
            {
                return false;
            }

            grouper.RunAll(true);
            int lateSteps = RunLateStepsAfterOfficialRunIfNeeded(grouper);
            DaLog.Info("Generation trace: official grouper pipeline used. grouper=" + grouper.name +
                ", timing=" + grouper.timing +
                ", lateSupplementSteps=" + lateSteps.ToString(CultureInfo.InvariantCulture));
            return true;
        }

        private static int RunLateStepsAfterOfficialRunIfNeeded(PropGrouper grouper)
        {
            if (grouper == null)
            {
                return 0;
            }

            if (HasExternalPropGrouperRunAllPostfix())
            {
                return 0;
            }

            List<LevelGenStep> lateSteps = CollectLateStepsForGrouper(grouper);
            int executedSteps = 0;
            for (int index = 0; index < lateSteps.Count; index++)
            {
                LevelGenStep step = lateSteps[index];
                if (step == null)
                {
                    continue;
                }

                if (TryInvokeLevelGenGo(step))
                {
                    executedSteps++;
                }
            }

            if (executedSteps > 0)
            {
                DaLog.Info("Generation trace: manual late-step supplement used. grouper=" + grouper.name +
                    ", steps=" + executedSteps.ToString(CultureInfo.InvariantCulture));
            }

            return executedSteps;
        }

        private static List<LevelGenStep> CollectLateStepsForGrouper(PropGrouper grouper)
        {
            List<LevelGenStep> result = new List<LevelGenStep>();
            LevelGenStep[] steps = grouper != null ? grouper.GetComponentsInChildren<LevelGenStep>(true) : null;
            for (int index = 0; steps != null && index < steps.Length; index++)
            {
                LevelGenStep step = steps[index];
                PropGrouper nearestGrouper = step != null ? step.GetComponentInParent<PropGrouper>() : null;
                if (nearestGrouper != null && nearestGrouper.timing == PropGrouper.PropGrouperTiming.Late)
                {
                    result.Add(step);
                }
            }

            return result;
        }

        private static bool HasExternalPropGrouperRunAllPostfix()
        {
            try
            {
                MethodInfo runAll = AccessTools.Method(typeof(PropGrouper), "RunAll");
                Patches patches = runAll != null ? Harmony.GetPatchInfo(runAll) : null;
                if (patches == null || patches.Postfixes == null)
                {
                    return false;
                }

                for (int index = 0; index < patches.Postfixes.Count; index++)
                {
                    Patch patch = patches.Postfixes[index];
                    string owner = patch != null ? patch.owner ?? string.Empty : string.Empty;
                    if (owner.IndexOf("terraincustomiser", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        owner.IndexOf("terrainrandomiser", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                DaLog.OnceWarn("generation-trace-harmony-postfix-check-failed", "Failed to inspect PropGrouper.RunAll patches: " + ex.Message);
            }

            return false;
        }

        private static bool VerifyGrouperForRuntimeRun(PropGrouper grouper)
        {
            if (grouper == null)
            {
                return false;
            }

            PropSpawner[] propSpawners = grouper.GetComponentsInChildren<PropSpawner>();
            for (int index = 0; propSpawners != null && index < propSpawners.Length; index++)
            {
                PropSpawner spawner = propSpawners[index];
                if (spawner == null)
                {
                    continue;
                }

                if (spawner.props == null)
                {
                    DaLog.Error("Missing spawns on " + spawner.name);
                    return false;
                }

                for (int propIndex = 0; propIndex < spawner.props.Length; propIndex++)
                {
                    if (spawner.props[propIndex] == null)
                    {
                        DaLog.Error("Missing prefab on " + spawner.name);
                        return false;
                    }
                }
            }

            return true;
        }

        private static List<Component> FindRuntimeSpawnedItemComponents()
        {
            List<Component> result = new List<Component>();
            HashSet<GameObject> seen = new HashSet<GameObject>();
            AddSceneComponentsByTypeName("Item", result, seen);

            List<Component> photonViews = FindSceneComponentsByTypeName("PhotonView");
            for (int index = 0; index < photonViews.Count; index++)
            {
                Component photonView = photonViews[index];
                GameObject gameObject = photonView != null ? photonView.gameObject : null;
                if (!IsRuntimeSpawnedItemCandidate(gameObject) || !seen.Add(gameObject))
                {
                    continue;
                }

                result.Add(photonView);
            }

            return result;
        }

        private static void AddSceneComponentsByTypeName(string typeName, List<Component> target, HashSet<GameObject> seen)
        {
            if (target == null || seen == null)
            {
                return;
            }

            List<Component> found = FindSceneComponentsByTypeName(typeName);
            for (int index = 0; index < found.Count; index++)
            {
                Component component = found[index];
                GameObject gameObject = component != null ? component.gameObject : null;
                if (gameObject == null || !seen.Add(gameObject))
                {
                    continue;
                }

                target.Add(component);
            }
        }

        private static List<Component> FindSceneComponentsByTypeName(string typeName)
        {
            List<Component> result = new List<Component>();
            Type type = FindTypeByName(typeName);
            if (type == null)
            {
                return result;
            }

            UnityEngine.Object[] objects = Resources.FindObjectsOfTypeAll(type);
            for (int index = 0; objects != null && index < objects.Length; index++)
            {
                Component component = objects[index] as Component;
                if (component != null && IsSceneObject(component.gameObject))
                {
                    result.Add(component);
                }
            }

            return result;
        }

        private static bool IsKnownRuntimeItemSpawner(Component component)
        {
            if (component == null)
            {
                return false;
            }

            Type type = component.GetType();
            return IsTypeNamed(type, "SingleItemSpawner") ||
                   IsTypeNamed(type, "Spawner");
        }

        private static bool IsKnownCoconutSpawner(Component component)
        {
            if (component == null)
            {
                return false;
            }

            string expectedName = GetExpectedRuntimeSpawnedItemName(component);
            if (string.Equals(expectedName, "Item_Coconut", StringComparison.Ordinal))
            {
                return true;
            }

            if (!IsTypeNamed(component.GetType(), "Spawner"))
            {
                return false;
            }

            string hierarchyPath = GetRuntimeHierarchyPath(component.transform);
            return ContainsText(component.name, "Coconut") ||
                   ContainsText(hierarchyPath, "CoconutSpawnList") ||
                   ContainsText(hierarchyPath, "Jungle_PalmTree") ||
                   ContainsText(hierarchyPath, "PalmTree");
        }

        private static bool IsTypeNamed(Type type, string typeName)
        {
            while (type != null)
            {
                if (string.Equals(type.Name, typeName, StringComparison.Ordinal))
                {
                    return true;
                }

                type = type.BaseType;
            }

            return false;
        }

        private static int CollectTrackedSpawnedItems(Component tracker, HashSet<GameObject> candidates, Dictionary<GameObject, string> matchReasons)
        {
            if (tracker == null || candidates == null)
            {
                return 0;
            }

            int matches = 0;
            PropertyInfo spawnedItemsProperty = tracker.GetType().GetProperty("SpawnedItems", InstanceFieldFlags);
            IEnumerable spawnedItems = spawnedItemsProperty != null ? spawnedItemsProperty.GetValue(tracker, null) as IEnumerable : null;
            if (spawnedItems != null)
            {
                foreach (object item in spawnedItems)
                {
                    GameObject candidate = GetObjectGameObject(item);
                    if (candidate != null && candidates.Add(candidate))
                    {
                        AddRuntimeSpawnMatchReason(matchReasons, candidate, "tracker:" + GetRuntimeHierarchyPath(tracker.transform));
                        matches++;
                    }
                }
            }

            FieldInfo spawnedViewsField = tracker.GetType().GetField("_spawnedItems", InstanceFieldFlags);
            IList spawnedViews = spawnedViewsField != null ? spawnedViewsField.GetValue(tracker) as IList : null;
            for (int index = 0; spawnedViews != null && index < spawnedViews.Count; index++)
            {
                GameObject candidate = GetObjectGameObject(spawnedViews[index]);
                if (candidate != null && candidates.Add(candidate))
                {
                    AddRuntimeSpawnMatchReason(matchReasons, candidate, "trackerField:" + GetRuntimeHierarchyPath(tracker.transform));
                    matches++;
                }
            }

            return matches;
        }

        private static bool ResetSpawnTrackerState(Component tracker)
        {
            if (tracker == null)
            {
                return false;
            }

            bool changed = false;

            FieldInfo spawnedViewsField = tracker.GetType().GetField("_spawnedItems", InstanceFieldFlags);
            IList spawnedViews = spawnedViewsField != null ? spawnedViewsField.GetValue(tracker) as IList : null;
            if (spawnedViews != null && spawnedViews.Count > 0)
            {
                spawnedViews.Clear();
                changed = true;
            }

            FieldInfo historyField = tracker.GetType().GetField("_historyFromSave", InstanceFieldFlags);
            IList history = historyField != null ? historyField.GetValue(tracker) as IList : null;
            if (history != null && history.Count > 0)
            {
                history.Clear();
                changed = true;
            }

            if (historyField != null && historyField.GetValue(tracker) != null)
            {
                historyField.SetValue(tracker, null);
                changed = true;
            }

            PropertyInfo hasSpawnHistoryProperty = tracker.GetType().GetProperty("HasSpawnHistory", InstanceFieldFlags);
            MethodInfo setter = hasSpawnHistoryProperty != null ? hasSpawnHistoryProperty.GetSetMethod(true) : null;
            if (setter != null)
            {
                object currentValue = hasSpawnHistoryProperty.GetValue(tracker, null);
                if (!(currentValue is bool) || (bool)currentValue)
                {
                    setter.Invoke(tracker, new object[] { false });
                    changed = true;
                }
            }
            else
            {
                FieldInfo backingField = tracker.GetType().GetField("<HasSpawnHistory>k__BackingField", InstanceFieldFlags);
                if (backingField != null && backingField.FieldType == typeof(bool) && (bool)backingField.GetValue(tracker))
                {
                    backingField.SetValue(tracker, false);
                    changed = true;
                }
            }

            return changed;
        }

        private static int CollectNearbySpawnedItemsForSpawner(Component spawner, List<Component> runtimeItems, HashSet<GameObject> candidates, Dictionary<GameObject, string> matchReasons)
        {
            if (spawner == null || runtimeItems == null || runtimeItems.Count == 0 || candidates == null)
            {
                return 0;
            }

            List<Vector3> origins = GetSpawnerOrigins(spawner);
            if (origins.Count == 0)
            {
                return 0;
            }

            string expectedName = GetExpectedRuntimeSpawnedItemName(spawner);
            float sqrRadius = GetRuntimeSpawnCleanupRadiusSqr(spawner, expectedName);
            float bestMatchedDistance = 0f;
            int matches = 0;
            for (int itemIndex = 0; itemIndex < runtimeItems.Count; itemIndex++)
            {
                Component item = runtimeItems[itemIndex];
                GameObject itemObject = item != null ? item.gameObject : null;
                if (!IsCandidateForSpatialSpawnCleanup(itemObject, expectedName))
                {
                    continue;
                }

                Vector3 itemPosition = item.transform.position;
                for (int originIndex = 0; originIndex < origins.Count; originIndex++)
                {
                    float sqrDistance = (itemPosition - origins[originIndex]).sqrMagnitude;
                    if (sqrDistance > sqrRadius)
                    {
                        continue;
                    }

                    if (candidates.Add(itemObject))
                    {
                        bestMatchedDistance = Mathf.Sqrt(sqrDistance);
                        AddRuntimeSpawnMatchReason(
                            matchReasons,
                            itemObject,
                            "spatial:" + GetRuntimeHierarchyPath(spawner.transform) +
                            ", expected=" + (expectedName ?? string.Empty) +
                            ", distance=" + bestMatchedDistance.ToString("F2", CultureInfo.InvariantCulture));
                        matches++;
                    }

                    break;
                }
            }

            return matches;
        }

        private static void AddRuntimeSpawnMatchReason(Dictionary<GameObject, string> matchReasons, GameObject itemObject, string reason)
        {
            if (matchReasons == null || itemObject == null || string.IsNullOrWhiteSpace(reason))
            {
                return;
            }

            if (matchReasons.ContainsKey(itemObject))
            {
                matchReasons[itemObject] += " | " + reason;
            }
            else
            {
                matchReasons[itemObject] = reason;
            }
        }

        private static void LogRuntimeSpawnCleanupDiagnostics(
            DaSegmentData segment,
            DaPropGrouperData grouper,
            string context,
            List<Component> runtimeItems,
            HashSet<GameObject> candidates,
            Dictionary<GameObject, string> matchReasons)
        {
            if (runtimeItems == null || runtimeItems.Count == 0)
            {
                return;
            }

            if (candidates != null && candidates.Count > 0)
            {
                int logged = 0;
                foreach (GameObject candidate in candidates)
                {
                    if (candidate == null || logged >= RuntimeSpawnDiagnosticMaxMatchedItems)
                    {
                        continue;
                    }

                    string reason = string.Empty;
                    if (matchReasons != null)
                    {
                        matchReasons.TryGetValue(candidate, out reason);
                    }

                    DaLog.Info("Generation trace: runtime item cleanup candidate." +
                        " context=" + (context ?? string.Empty) +
                        ", segment=" + (segment != null ? segment.SegmentName ?? string.Empty : string.Empty) +
                        ", grouper=" + (grouper != null ? grouper.GrouperName ?? string.Empty : string.Empty) +
                        ", item=" + DescribeRuntimeSpawnedItem(candidate, grouper) +
                        ", reason=" + (reason ?? string.Empty));
                    logged++;
                }

                if (candidates.Count > logged)
                {
                    DaLog.Info("Generation trace: runtime item cleanup candidates truncated. remaining=" +
                        (candidates.Count - logged).ToString(CultureInfo.InvariantCulture));
                }

                return;
            }

            LogNearbyRuntimeSpawnedItems(segment, grouper, context, runtimeItems);
        }

        private static void LogNearbyRuntimeSpawnedItems(DaSegmentData segment, DaPropGrouperData grouper, string context, List<Component> runtimeItems)
        {
            if (grouper == null || grouper.SourceObject == null || runtimeItems == null || runtimeItems.Count == 0)
            {
                return;
            }

            Component[] components = grouper.SourceObject.GetComponentsInChildren<Component>(true);
            List<Component> spawners = new List<Component>();
            for (int index = 0; components != null && index < components.Length; index++)
            {
                Component component = components[index];
                if (IsKnownRuntimeItemSpawner(component))
                {
                    spawners.Add(component);
                }
            }

            if (spawners.Count == 0)
            {
                return;
            }

            int logged = 0;
            for (int itemIndex = 0; itemIndex < runtimeItems.Count && logged < RuntimeSpawnDiagnosticMaxNearbyItems; itemIndex++)
            {
                Component item = runtimeItems[itemIndex];
                GameObject itemObject = item != null ? item.gameObject : null;
                if (itemObject == null)
                {
                    continue;
                }

                Component nearestSpawner = null;
                float nearestDistance = float.MaxValue;
                string nearestExpected = string.Empty;
                for (int spawnerIndex = 0; spawnerIndex < spawners.Count; spawnerIndex++)
                {
                    Component spawner = spawners[spawnerIndex];
                    List<Vector3> origins = GetSpawnerOrigins(spawner);
                    for (int originIndex = 0; originIndex < origins.Count; originIndex++)
                    {
                        float distance = Vector3.Distance(itemObject.transform.position, origins[originIndex]);
                        if (distance < nearestDistance)
                        {
                            nearestDistance = distance;
                            nearestSpawner = spawner;
                            nearestExpected = GetExpectedRuntimeSpawnedItemName(spawner);
                        }
                    }
                }

                if (nearestSpawner == null)
                {
                    continue;
                }

                DaLog.Info("Generation trace: nearby runtime item not matched for cleanup." +
                    " context=" + (context ?? string.Empty) +
                    ", segment=" + (segment != null ? segment.SegmentName ?? string.Empty : string.Empty) +
                    ", grouper=" + (grouper.GrouperName ?? string.Empty) +
                    ", item=" + DescribeRuntimeSpawnedItem(itemObject, grouper) +
                    ", nearestSpawner=" + GetRuntimeHierarchyPath(nearestSpawner.transform) +
                    ", expected=" + (nearestExpected ?? string.Empty) +
                    ", distance=" + nearestDistance.ToString("F2", CultureInfo.InvariantCulture));
                logged++;
            }
        }

        private static string DescribeRuntimeSpawnedItem(GameObject itemObject, DaPropGrouperData grouper)
        {
            if (itemObject == null)
            {
                return "<null>";
            }

            Rigidbody rigidbody = itemObject.GetComponent<Rigidbody>();
            Component photonView = FindComponentByTypeName(itemObject, "PhotonView");
            Component item = FindComponentByTypeName(itemObject, "Item");
            Component nearestSpawner = FindNearestKnownRuntimeItemSpawner(itemObject.transform, grouper, out float nearestDistance);
            return (itemObject.name ?? string.Empty) +
                ", path=" + GetRuntimeHierarchyPath(itemObject.transform) +
                ", pos=" + FormatVector3(itemObject.transform.position) +
                ", photonView=" + DescribePhotonView(photonView) +
                ", hasItem=" + (item != null ? "true" : "false") +
                ", rigidbody=" + DescribeRigidbody(rigidbody) +
                ", nearestSpawner=" + (nearestSpawner != null ? GetRuntimeHierarchyPath(nearestSpawner.transform) : string.Empty) +
                ", nearestDistance=" + (nearestSpawner != null ? nearestDistance.ToString("F2", CultureInfo.InvariantCulture) : string.Empty);
        }

        private static string DescribePhotonView(Component photonView)
        {
            if (photonView == null)
            {
                return "none";
            }

            PropertyInfo viewIdProperty = photonView.GetType().GetProperty("ViewID", InstanceFieldFlags);
            object viewId = viewIdProperty != null ? viewIdProperty.GetValue(photonView, null) : null;
            return viewId != null ? Convert.ToString(viewId, CultureInfo.InvariantCulture) : "present";
        }

        private static string DescribeRigidbody(Rigidbody rigidbody)
        {
            if (rigidbody == null)
            {
                return "none";
            }

            return "present,kinematic=" + (rigidbody.isKinematic ? "true" : "false");
        }

        private static Component FindNearestKnownRuntimeItemSpawner(Transform itemTransform, DaPropGrouperData grouper, out float nearestDistance)
        {
            nearestDistance = float.MaxValue;
            if (itemTransform == null || grouper == null || grouper.SourceObject == null)
            {
                return null;
            }

            Component nearest = null;
            Component[] components = grouper.SourceObject.GetComponentsInChildren<Component>(true);
            for (int index = 0; components != null && index < components.Length; index++)
            {
                Component component = components[index];
                if (!IsKnownRuntimeItemSpawner(component))
                {
                    continue;
                }

                List<Vector3> origins = GetSpawnerOrigins(component);
                for (int originIndex = 0; originIndex < origins.Count; originIndex++)
                {
                    float distance = Vector3.Distance(itemTransform.position, origins[originIndex]);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearest = component;
                    }
                }
            }

            return nearest;
        }

        private static float GetRuntimeSpawnCleanupRadiusSqr(Component spawner, string expectedName)
        {
            if (spawner != null && IsKnownCoconutSpawner(spawner))
            {
                return RuntimeCoconutSpawnCleanupRadius * RuntimeCoconutSpawnCleanupRadius;
            }

            if (spawner != null && IsTypeNamed(spawner.GetType(), "SingleItemSpawner"))
            {
                if (string.Equals(expectedName, "Item_Coconut", StringComparison.Ordinal))
                {
                    return RuntimeCoconutSpawnCleanupRadius * RuntimeCoconutSpawnCleanupRadius;
                }

                return RuntimeSingleItemSpawnCleanupRadius * RuntimeSingleItemSpawnCleanupRadius;
            }

            return RuntimeSpawnCleanupRadius * RuntimeSpawnCleanupRadius;
        }

        private static List<Vector3> GetSpawnerOrigins(Component spawner)
        {
            List<Vector3> result = new List<Vector3>();
            if (spawner == null)
            {
                return result;
            }

            if (IsTypeNamed(spawner.GetType(), "SingleItemSpawner"))
            {
                result.Add(spawner.transform.position + Vector3.up * 0.1f);
                return result;
            }

            TryAddSpawnSpotPositions(GetSpawnerSpotEntries(spawner), result);

            return result;
        }

        private static IEnumerable GetSpawnerSpotEntries(Component spawner)
        {
            if (spawner == null)
            {
                return null;
            }

            MethodInfo getSpawnSpotsMethod = spawner.GetType().GetMethod("GetSpawnSpots", InstanceFieldFlags);
            if (getSpawnSpotsMethod != null)
            {
                try
                {
                    IEnumerable spots = getSpawnSpotsMethod.Invoke(spawner, null) as IEnumerable;
                    if (spots != null)
                    {
                        return spots;
                    }
                }
                catch
                {
                }
            }

            FieldInfo spawnSpotsField = GetFieldInfo(spawner.GetType(), "spawnSpots");
            if (spawnSpotsField != null)
            {
                return spawnSpotsField.GetValue(spawner) as IEnumerable;
            }

            return null;
        }

        private static void TryAddSpawnSpotPositions(IEnumerable entries, List<Vector3> result)
        {
            if (entries == null || result == null)
            {
                return;
            }

            foreach (object entry in entries)
            {
                Transform transform = entry as Transform;
                if (transform == null)
                {
                    transform = GetObjectGameObject(entry)?.transform;
                }

                if (transform != null)
                {
                    result.Add(transform.position);
                }
            }
        }

        private static string GetExpectedRuntimeSpawnedItemName(Component spawner)
        {
            if (spawner == null)
            {
                return string.Empty;
            }

            if (IsTypeNamed(spawner.GetType(), "SingleItemSpawner"))
            {
                FieldInfo prefabField = GetFieldInfo(spawner.GetType(), "prefab");
                GameObject prefab = prefabField != null ? prefabField.GetValue(spawner) as GameObject : null;
                return prefab != null ? prefab.name ?? string.Empty : string.Empty;
            }

            if (!IsTypeNamed(spawner.GetType(), "Spawner"))
            {
                return string.Empty;
            }

            if (ContainsText(spawner.name, "Coconut") || ContainsText(GetRuntimeHierarchyPath(spawner.transform), "Coconut"))
            {
                return "Item_Coconut";
            }

            FieldInfo spawnPoolField = GetFieldInfo(spawner.GetType(), "spawnPool");
            object spawnPool = spawnPoolField != null ? spawnPoolField.GetValue(spawner) : null;
            string spawnPoolName = GetNamedObjectValue(spawnPool);
            if (ContainsText(spawnPoolName, "Coconut"))
            {
                return "Item_Coconut";
            }

            FieldInfo spawnsField = GetFieldInfo(spawner.GetType(), "spawns");
            object spawns = spawnsField != null ? spawnsField.GetValue(spawner) : null;
            string spawnsName = GetNamedObjectValue(spawns);
            if (ContainsText(spawnsName, "Coconut"))
            {
                return "Item_Coconut";
            }

            return string.Empty;
        }

        private static bool IsCandidateForSpatialSpawnCleanup(GameObject itemObject, string expectedName)
        {
            if (!IsRuntimeSpawnedItemCandidate(itemObject))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(expectedName))
            {
                string itemName = itemObject.name ?? string.Empty;
                if (!itemName.StartsWith(expectedName, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsRuntimeSpawnedItemCandidate(GameObject itemObject)
        {
            if (itemObject == null || !IsSceneObject(itemObject) || !itemObject.activeInHierarchy)
            {
                return false;
            }

            if (!HasComponentWithTypeName(itemObject, "PhotonView"))
            {
                return false;
            }

            Rigidbody rigidbody = itemObject.GetComponent<Rigidbody>();
            if (rigidbody == null || !rigidbody.isKinematic)
            {
                return false;
            }

            return true;
        }

        private static int DestroyRuntimeItems(HashSet<GameObject> candidates, Dictionary<GameObject, string> matchReasons)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return 0;
            }

            int destroyed = 0;
            foreach (GameObject candidate in candidates)
            {
                if (candidate == null)
                {
                    continue;
                }

                bool removed = TryDestroyViaPhoton(candidate);
                if (!removed)
                {
                    UnityEngine.Object.DestroyImmediate(candidate);
                }

                string reason = string.Empty;
                if (matchReasons != null)
                {
                    matchReasons.TryGetValue(candidate, out reason);
                }

                DaLog.Info("Generation trace: runtime item destroyed before regeneration." +
                    " item=" + (candidate.name ?? string.Empty) +
                    ", reason=" + (reason ?? string.Empty) +
                    ", viaPhoton=" + (removed ? "true" : "false"));
                destroyed++;
            }

            return destroyed;
        }

        private static void LogNearbyRuntimeItemDiagnostics(DaSegmentData segment, DaPropGrouperData grouper, List<Component> runtimeItems)
        {
            if (segment == null || grouper == null || grouper.SourceObject == null || runtimeItems == null || runtimeItems.Count == 0)
            {
                return;
            }

            string segmentName = segment.SegmentName ?? string.Empty;
            if (segmentName.IndexOf("Beach", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return;
            }

            List<Component> coconutSpawners = new List<Component>();
            Component[] components = grouper.SourceObject.GetComponentsInChildren<Component>(true);
            for (int index = 0; components != null && index < components.Length; index++)
            {
                Component component = components[index];
                if (!IsKnownCoconutSpawner(component))
                {
                    continue;
                }

                coconutSpawners.Add(component);
            }

            if (coconutSpawners.Count == 0)
            {
                return;
            }

            int logged = 0;
            for (int itemIndex = 0; itemIndex < runtimeItems.Count && logged < 8; itemIndex++)
            {
                Component item = runtimeItems[itemIndex];
                GameObject itemObject = item != null ? item.gameObject : null;
                if (itemObject == null ||
                    !IsSceneObject(itemObject) ||
                    !StartsWithObjectName(itemObject, "Item_Coconut"))
                {
                    continue;
                }

                float bestDistance = float.MaxValue;
                Component bestSpawner = null;
                for (int spawnerIndex = 0; spawnerIndex < coconutSpawners.Count; spawnerIndex++)
                {
                    Component spawner = coconutSpawners[spawnerIndex];
                    if (spawner == null)
                    {
                        continue;
                    }

                    float distance = Vector3.Distance(item.transform.position, spawner.transform.position);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestSpawner = spawner;
                    }
                }

                if (bestSpawner == null)
                {
                    continue;
                }

                DaLog.Info("Generation trace: coconut diagnostic." +
                    " segment=" + segmentName +
                    ", grouper=" + (grouper.GrouperName ?? string.Empty) +
                    ", item=" + itemObject.name +
                    ", itemPos=" + FormatVector3(item.transform.position) +
                    ", expected=" + GetExpectedRuntimeSpawnedItemName(bestSpawner) +
                    ", nearestSpawner=" + GetRuntimeHierarchyPath(bestSpawner.transform) +
                    ", spawnerPos=" + FormatVector3(bestSpawner.transform.position) +
                    ", distance=" + bestDistance.ToString("F2", CultureInfo.InvariantCulture));
                logged++;
            }
        }

        private static bool TryDestroyViaPhoton(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return false;
            }

            Type photonNetworkType = FindTypeByName("PhotonNetwork");
            if (photonNetworkType == null)
            {
                return false;
            }

            Component photonView = FindComponentByTypeName(gameObject, "PhotonView");
            return TryInvokePhotonDestroy(photonNetworkType, photonView) ||
                   TryInvokePhotonDestroy(photonNetworkType, gameObject);
        }

        private static bool TryInvokePhotonDestroy(Type photonNetworkType, object target)
        {
            if (photonNetworkType == null || target == null)
            {
                return false;
            }

            MethodInfo[] methods = photonNetworkType.GetMethods(BindingFlags.Public | BindingFlags.Static);
            for (int index = 0; methods != null && index < methods.Length; index++)
            {
                MethodInfo method = methods[index];
                if (method == null || !string.Equals(method.Name, "Destroy", StringComparison.Ordinal))
                {
                    continue;
                }

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length != 1 || !parameters[0].ParameterType.IsInstanceOfType(target))
                {
                    continue;
                }

                try
                {
                    method.Invoke(null, new[] { target });
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        private static Component FindComponentByTypeName(GameObject gameObject, string typeName)
        {
            Component[] components = gameObject != null ? gameObject.GetComponents<Component>() : null;
            for (int index = 0; components != null && index < components.Length; index++)
            {
                Component component = components[index];
                if (component != null && IsTypeNamed(component.GetType(), typeName))
                {
                    return component;
                }
            }

            return null;
        }

        private static bool HasComponentWithTypeName(GameObject gameObject, string typeName)
        {
            return FindComponentByTypeName(gameObject, typeName) != null;
        }

        private static GameObject GetObjectGameObject(object value)
        {
            if (value == null)
            {
                return null;
            }

            GameObject gameObject = value as GameObject;
            if (gameObject != null)
            {
                return gameObject;
            }

            Component component = value as Component;
            if (component != null)
            {
                return component.gameObject;
            }

            PropertyInfo gameObjectProperty = value.GetType().GetProperty("gameObject", InstanceFieldFlags);
            if (gameObjectProperty != null)
            {
                return gameObjectProperty.GetValue(value, null) as GameObject;
            }

            PropertyInfo transformProperty = value.GetType().GetProperty("transform", InstanceFieldFlags);
            Transform transform = transformProperty != null ? transformProperty.GetValue(value, null) as Transform : null;
            return transform != null ? transform.gameObject : null;
        }

        private static bool StartsWithObjectName(GameObject gameObject, string prefix)
        {
            string objectName = gameObject != null ? gameObject.name ?? string.Empty : string.Empty;
            return !string.IsNullOrWhiteSpace(prefix) &&
                   objectName.StartsWith(prefix, StringComparison.Ordinal);
        }

        private static string GetNamedObjectValue(object value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            if (value is GameObject gameObject)
            {
                return gameObject.name ?? string.Empty;
            }

            if (value is Component component)
            {
                return component.name ?? string.Empty;
            }

            if (value is UnityEngine.Object unityObject)
            {
                return unityObject.name ?? string.Empty;
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        private static string FormatVector3(Vector3 value)
        {
            return "(" +
                   value.x.ToString("F2", CultureInfo.InvariantCulture) + ", " +
                   value.y.ToString("F2", CultureInfo.InvariantCulture) + ", " +
                   value.z.ToString("F2", CultureInfo.InvariantCulture) + ")";
        }

        private static void ValidateGrouperAfterwardsIfEnabled(PropGrouper grouper)
        {
            if (grouper == null)
            {
                return;
            }

            try
            {
                FieldInfo validateAfterwardsField = typeof(PropGrouper).GetField("ValidateAfterwards", InstanceFieldFlags);
                if (validateAfterwardsField != null && validateAfterwardsField.FieldType == typeof(bool) && (bool)validateAfterwardsField.GetValue(grouper))
                {
                    grouper.Validate();
                }
            }
            catch (Exception ex)
            {
                DaLog.OnceWarn("grouper-validate-afterwards-failed:" + grouper.name, "Failed to validate grouper after runtime generation: " + ex.Message);
            }
        }

        private static bool ShouldSkipGrouperForOfficialSegmentRun(DaSegmentData segment, DaPropGrouperData grouper)
        {
            if (segment == null || grouper == null)
            {
                return false;
            }

            string segmentName = segment.SegmentName ?? string.Empty;
            string grouperName = grouper.GrouperName ?? string.Empty;

            if (string.Equals(segmentName, "Roots Segment", StringComparison.Ordinal) &&
                (string.Equals(grouperName, "PlateauRocks", StringComparison.Ordinal) ||
                 string.Equals(grouperName, "WallRocks", StringComparison.Ordinal)))
            {
                return true;
            }

            if (string.Equals(segmentName, "Jungle_Segment", StringComparison.Ordinal) &&
                (string.Equals(grouperName, "Rocks_Plat", StringComparison.Ordinal) ||
                 string.Equals(grouperName, "Rocks_Wall", StringComparison.Ordinal)))
            {
                return true;
            }

            return false;
        }

        private static int RunWhitelistedStepsForSkippedOfficialGrouper(DaSegmentData segment, DaPropGrouperData grouper)
        {
            int generated = 0;
            if (segment == null || grouper == null || grouper.Steps == null)
            {
                return generated;
            }

            for (int index = 0; index < grouper.Steps.Count; index++)
            {
                DaLevelGenStepData step = grouper.Steps[index];
                if (step == null || step.SourceObject == null || !ShouldRunStepInSkippedOfficialGrouper(segment, grouper, step))
                {
                    continue;
                }

                StepGenerationSnapshot before = BuildStepGenerationSnapshot(step, index);
                int removed = ClearDirectGeneratedChildren(step.SourceObject.transform, false);
                if (TryInvokeLevelGenGo(step.SourceObject))
                {
                    UnityEngine.Physics.SyncTransforms();
                    StepGenerationSnapshot after = BuildStepGenerationSnapshot(step, index);
                    LogStepGenerationDelta(segment, grouper, before, after, "whitelisted-step", removed);
                    generated++;
                    DaLog.Info("Generated whitelisted step from skipped official grouper. segment=" + segment.SegmentName +
                        ", grouper=" + grouper.GrouperName + ", step=" + step.StepName);
                }
            }

            if (generated > 0)
            {
                UnityEngine.Physics.SyncTransforms();
            }

            return generated;
        }

        private static bool ShouldRunStepInSkippedOfficialGrouper(DaSegmentData segment, DaPropGrouperData grouper, DaLevelGenStepData step)
        {
            string segmentName = segment != null ? segment.SegmentName ?? string.Empty : string.Empty;
            string grouperName = grouper != null ? grouper.GrouperName ?? string.Empty : string.Empty;
            string stepName = step != null ? step.StepName ?? string.Empty : string.Empty;

            HashSet<string> whitelistedSteps;
            return SkippedOfficialGrouperStepWhitelist.TryGetValue(BuildSkippedGrouperKey(segmentName, grouperName), out whitelistedSteps) &&
                   whitelistedSteps != null &&
                   whitelistedSteps.Contains(stepName);
        }

        private static string BuildSkippedGrouperKey(string segmentName, string grouperName)
        {
            return (segmentName ?? string.Empty) + "|" + (grouperName ?? string.Empty);
        }

        private static int ClearSegmentRuntimeChildren(DaSegmentData segment)
        {
            int removed = 0;
            if (segment == null)
            {
                return removed;
            }

            HashSet<Transform> clearedParents = new HashSet<Transform>();
            HashSet<Transform> generationParents = new HashSet<Transform>();
            for (int index = 0; segment.Groupers != null && index < segment.Groupers.Count; index++)
            {
                DaPropGrouperData grouper = segment.Groupers[index];
                if (grouper != null && grouper.SourceObject != null)
                {
                    generationParents.Add(grouper.SourceObject.transform);
                    if (ShouldPreserveGrouperForCustomBlank(segment, grouper))
                    {
                        DaLog.Info("Preserved structural grouper for custom blank. segment=" + segment.SegmentName + ", grouper=" + grouper.GrouperName);
                        removed += ClearNestedCustomBlankDecorations(segment, grouper.SourceObject.transform);
                        continue;
                    }

                    clearedParents.Add(grouper.SourceObject.transform);
                    removed += ClearRuntimeChildren(grouper.SourceObject.transform, step => ShouldPreserveStepForCustomBlank(segment, step));
                }
            }

            removed += ClearLooseLevelGenStepChildren(segment, generationParents, step => ShouldPreserveStepForCustomBlank(segment, step));
            removed += ClearKnownLooseDecorationContainers(segment);
            removed += ClearSceneCapybaraObjects(segment);
            removed += ClearSceneBeachAndLuggageObjects(segment);
            return removed;
        }

        private static bool ShouldPreserveGrouperForCustomBlank(DaSegmentData segment, DaPropGrouperData grouper)
        {
            string segmentName = segment != null ? segment.SegmentName : string.Empty;
            string grouperName = grouper != null ? grouper.GrouperName : string.Empty;

            return false;
        }

        private static int ClearNestedCustomBlankDecorations(DaSegmentData segment, Transform preservedRoot)
        {
            int removed = 0;
            if (segment == null || preservedRoot == null)
            {
                return removed;
            }

            if (string.Equals(segment.SegmentName, "Desert_Segment", StringComparison.Ordinal) &&
                string.Equals(preservedRoot.name, "Platteau", StringComparison.Ordinal))
            {
                removed += ClearNestedGrouperChildren(preservedRoot, "Canyon");
            }

            if (removed > 0)
            {
                DaLog.Info("Cleared nested custom blank decorations. segment=" + segment.SegmentName + ", root=" + preservedRoot.name + ", removed=" + removed);
            }

            return removed;
        }

        private static int ClearNestedGrouperChildren(Transform root, string grouperName)
        {
            int removed = 0;
            PropGrouper[] groupers = root.GetComponentsInChildren<PropGrouper>(true);
            for (int index = 0; index < groupers.Length; index++)
            {
                PropGrouper grouper = groupers[index];
                if (grouper == null ||
                    grouper.transform == root ||
                    !string.Equals(grouper.name, grouperName, StringComparison.Ordinal))
                {
                    continue;
                }

                removed += ClearRuntimeChildren(grouper.transform);
            }

            return removed;
        }

        private static int ClearLooseLevelGenStepChildren(DaSegmentData segment, HashSet<Transform> clearedParents, Func<LevelGenStep, bool> shouldPreserveStep)
        {
            int removed = 0;
            foreach (Transform root in segment.SourceRoots ?? new List<Transform>())
            {
                if (root == null)
                {
                    continue;
                }

                LevelGenStep[] steps = root.GetComponentsInChildren<LevelGenStep>(true);
                for (int index = 0; index < steps.Length; index++)
                {
                    Transform stepTransform = steps[index] != null ? steps[index].transform : null;
                    if (stepTransform == null || IsUnderAny(stepTransform, clearedParents))
                    {
                        continue;
                    }

                    if (shouldPreserveStep != null && shouldPreserveStep(steps[index]))
                    {
                        continue;
                    }

                    removed += ClearDirectGeneratedChildren(stepTransform, false);
                }
            }

            if (removed > 0)
            {
                DaLog.Info("Cleared loose level generation children in segment " + segment.SegmentName + ", removed=" + removed);
            }

            return removed;
        }

        private static int ClearKnownLooseDecorationContainers(DaSegmentData segment)
        {
            int removed = 0;
            foreach (Transform root in segment.SourceRoots ?? new List<Transform>())
            {
                if (root == null)
                {
                    continue;
                }

                Transform desertRocks = FindChildPath(root, "Misc", "DesertRocks");
                if (desertRocks != null)
                {
                    removed += ClearDirectGeneratedChildren(desertRocks, false);
                }

                Transform desertOasis = FindChildPath(root, "Platteau", "Rocks", "Oasis");
                if (desertOasis != null)
                {
                    removed += ClearDirectGeneratedChildren(desertOasis, false);
                    removed += ClearCapybaraObjects(desertOasis, "desertOasis");
                }

                removed += ClearCapybaraObjects(root, "segmentRoot");

                Transform calderaBoxes = FindChildPath(root, "Enterance", "Boxes");
                if (calderaBoxes != null)
                {
                    removed += ClearDirectGeneratedChildren(calderaBoxes, false);
                }

                Transform calderaRespawnChest = FindChildPath(root, "Enterance", "CampfireSpawner_Volcano", "RespawnChest");
                if (calderaRespawnChest != null)
                {
                    removed += ClearDirectGeneratedChildren(calderaRespawnChest, false);
                }

                Transform calderaLavaTemple = FindChildPath(root, "Enterance", "LavaTemple");
                if (calderaLavaTemple != null)
                {
                    removed += ClearDirectGeneratedChildren(calderaLavaTemple, false);
                }

                Transform calderaLavaAsh = FindChildPath(root, "Lava ash");
                if (calderaLavaAsh != null)
                {
                    removed += ClearDirectGeneratedChildren(calderaLavaAsh, false);
                }

                Transform calderaLavaBubbles = FindChildPath(root, "Lava Bubbles");
                if (calderaLavaBubbles != null)
                {
                    removed += ClearDirectGeneratedChildren(calderaLavaBubbles, false);
                }

                Transform volcanoSmoke = FindChildPath(root, "volcano Smoke");
                if (volcanoSmoke != null)
                {
                    removed += ClearDirectGeneratedChildren(volcanoSmoke, false);
                }

                Transform bridge = FindChildPath(root, "Bridge");
                if (bridge != null)
                {
                    removed += ClearDirectGeneratedChildren(bridge, false);
                }

                // Keep Volcano's moving lava mechanism intact. MovingLava drives a rock-door
                // animator reference, so deleting the loose mechanism rock breaks lava rising.
            }

            if (removed > 0)
            {
                DaLog.Info("Cleared loose decoration containers in segment " + segment.SegmentName + ", removed=" + removed);
            }

            return removed;
        }

        private static int ClearCapybaraObjects(Transform root, string source)
        {
            int removed = ClearMatchingDescendants(root, IsCapybaraObject);
            if (removed > 0)
            {
                DaLog.Info("Cleared capybara objects for custom blank. source=" + source + ", root=" + (root != null ? root.name : "<null>") + ", removed=" + removed);
            }

            return removed;
        }

        private static int ClearSceneCapybaraObjects(DaSegmentData segment)
        {
            if (!ShouldClearSceneCapybaras(segment))
            {
                return 0;
            }

            HashSet<GameObject> candidates = new HashSet<GameObject>();
            Type capybaraType = FindTypeByName("Capybara");
            if (capybaraType != null)
            {
                UnityEngine.Object[] components = Resources.FindObjectsOfTypeAll(capybaraType);
                for (int index = 0; components != null && index < components.Length; index++)
                {
                    Component component = components[index] as Component;
                    if (component != null && IsSceneObject(component.gameObject))
                    {
                        candidates.Add(GetCapybaraRemovalObject(component.transform));
                    }
                }
            }

            UnityEngine.Object[] transforms = Resources.FindObjectsOfTypeAll(typeof(Transform));
            for (int index = 0; transforms != null && index < transforms.Length; index++)
            {
                Transform transform = transforms[index] as Transform;
                if (transform != null && IsSceneObject(transform.gameObject) && IsCapybaraObject(transform))
                {
                    candidates.Add(GetCapybaraRemovalObject(transform));
                }
            }

            int removed = 0;
            foreach (GameObject candidate in candidates)
            {
                if (candidate == null)
                {
                    continue;
                }

                DaLog.Info("Cleared scene capybara for custom blank. segment=" + segment.SegmentName + ", path=" + GetRuntimeHierarchyPath(candidate.transform));
                UnityEngine.Object.DestroyImmediate(candidate);
                removed++;
            }

            if (removed > 0)
            {
                DaLog.Info("Cleared scene capybaras for custom blank. segment=" + segment.SegmentName + ", removed=" + removed);
            }

            return removed;
        }

        private static int ClearSceneBeachAndLuggageObjects(DaSegmentData segment)
        {
            if (segment == null)
            {
                return 0;
            }

            HashSet<GameObject> candidates = new HashSet<GameObject>();
            HashSet<Transform> roots = new HashSet<Transform>(segment.SourceRoots ?? new List<Transform>());

            Type beachSpawnerType = FindTypeByName("BeachSpawner");
            if (beachSpawnerType != null)
            {
                UnityEngine.Object[] components = Resources.FindObjectsOfTypeAll(beachSpawnerType);
                for (int index = 0; components != null && index < components.Length; index++)
                {
                    Component component = components[index] as Component;
                    if (component != null &&
                        IsSceneObject(component.gameObject) &&
                        IsUnderAny(component.transform, roots))
                    {
                        candidates.Add(component.gameObject);
                    }
                }
            }

            UnityEngine.Object[] transforms = Resources.FindObjectsOfTypeAll(typeof(Transform));
            for (int index = 0; transforms != null && index < transforms.Length; index++)
            {
                Transform transform = transforms[index] as Transform;
                if (transform == null ||
                    !IsSceneObject(transform.gameObject) ||
                    !IsUnderAny(transform, roots) ||
                    !IsCustomBlankObject(transform))
                {
                    continue;
                }

                GameObject candidate = GetCustomBlankRemovalObject(transform);
                if (candidate != null)
                {
                    candidates.Add(candidate);
                }
            }

            int removed = 0;
            foreach (GameObject candidate in candidates)
            {
                if (candidate == null)
                {
                    continue;
                }

                DaLog.Info("Cleared custom blank beach/luggage object. segment=" + segment.SegmentName + ", path=" + GetRuntimeHierarchyPath(candidate.transform));
                UnityEngine.Object.DestroyImmediate(candidate);
                removed++;
            }

            if (removed > 0)
            {
                DaLog.Info("Cleared custom blank beach/luggage objects. segment=" + segment.SegmentName + ", removed=" + removed);
            }

            return removed;
        }

        private static bool ShouldClearSceneCapybaras(DaSegmentData segment)
        {
            string segmentName = segment != null ? segment.SegmentName ?? string.Empty : string.Empty;
            return segmentName.IndexOf("Desert", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   segmentName.IndexOf("Snow", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static GameObject GetCapybaraRemovalObject(Transform transform)
        {
            if (transform == null)
            {
                return null;
            }

            Transform current = transform;
            Transform match = transform;
            while (current != null)
            {
                if (IsCapybaraText(current.name))
                {
                    match = current;
                }

                current = current.parent;
            }

            return match != null ? match.gameObject : transform.gameObject;
        }

        private static GameObject GetCustomBlankRemovalObject(Transform transform)
        {
            if (transform == null)
            {
                return null;
            }

            Transform current = transform;
            Transform match = transform;
            while (current != null)
            {
                if (IsCustomBlankObject(current))
                {
                    match = current;
                }

                current = current.parent;
            }

            return match != null ? match.gameObject : transform.gameObject;
        }

        private static bool IsSceneObject(GameObject gameObject)
        {
            return gameObject != null && gameObject.scene.IsValid();
        }

        private static bool IsCustomBlankObject(Transform transform)
        {
            if (transform == null)
            {
                return false;
            }

            string path = GetRuntimeHierarchyPath(transform);
            return ContainsCustomBlankBeachText(transform.name) ||
                   ContainsCustomBlankBeachText(path) ||
                   ContainsCustomBlankLuggageText(transform.name) ||
                   ContainsCustomBlankLuggageText(path);
        }

        private static bool ContainsCustomBlankBeachText(string value)
        {
            return ContainsText(value, "Coconut") ||
                   ContainsText(value, "Palm") ||
                   ContainsText(value, "BeachSpawner");
        }

        private static bool ContainsCustomBlankLuggageText(string value)
        {
            return ContainsText(value, "LuggageAncient") ||
                   ContainsText(value, "MirageLuggageAncient") ||
                   ContainsText(value, "LuggageSpawner") ||
                   ContainsText(value, "Luggage");
        }

        private static bool ContainsText(string value, string pattern)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   !string.IsNullOrWhiteSpace(pattern) &&
                   value.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Type FindTypeByName(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return null;
            }

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int assemblyIndex = 0; assemblyIndex < assemblies.Length; assemblyIndex++)
            {
                Type[] types;
                try
                {
                    types = assemblies[assemblyIndex].GetTypes();
                }
                catch
                {
                    continue;
                }

                for (int typeIndex = 0; typeIndex < types.Length; typeIndex++)
                {
                    Type type = types[typeIndex];
                    if (type == null)
                    {
                        continue;
                    }

                    if (string.Equals(type.Name, typeName, StringComparison.Ordinal) ||
                        string.Equals(type.FullName, typeName, StringComparison.Ordinal) ||
                        type.FullName != null && type.FullName.EndsWith("." + typeName, StringComparison.Ordinal))
                    {
                        return type;
                    }
                }
            }

            return null;
        }

        private static int ClearMatchingDescendants(Transform root, Func<Transform, bool> shouldRemove)
        {
            if (root == null || shouldRemove == null)
            {
                return 0;
            }

            int removed = 0;
            for (int index = root.childCount - 1; index >= 0; index--)
            {
                Transform child = root.GetChild(index);
                if (child == null)
                {
                    continue;
                }

                if (shouldRemove(child))
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                    removed++;
                    continue;
                }

                removed += ClearMatchingDescendants(child, shouldRemove);
            }

            return removed;
        }

        private static bool IsCapybaraObject(Transform transform)
        {
            if (transform == null)
            {
                return false;
            }

            string objectName = transform.name ?? string.Empty;
            if (IsCapybaraText(objectName) || IsCapybaraText(GetRuntimeHierarchyPath(transform)))
            {
                return true;
            }

            Renderer[] renderers = transform.GetComponents<Renderer>();
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                if (renderer == null || renderer.sharedMaterials == null)
                {
                    continue;
                }

                Material[] materials = renderer.sharedMaterials;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material material = materials[materialIndex];
                    if (material != null && string.Equals(material.name, "M_Capybara", StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsCapybaraText(string value)
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

        private static string GetRuntimeHierarchyPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            Stack<string> names = new Stack<string>();
            Transform current = transform;
            while (current != null)
            {
                names.Push(current.name ?? string.Empty);
                current = current.parent;
            }

            return string.Join("/", names.ToArray());
        }

        private static bool IsUnderAny(Transform transform, HashSet<Transform> parents)
        {
            if (transform == null || parents == null || parents.Count == 0)
            {
                return false;
            }

            Transform current = transform;
            while (current != null)
            {
                if (parents.Contains(current))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static Transform FindChildPath(Transform root, params string[] names)
        {
            Transform current = root;
            for (int index = 0; index < names.Length; index++)
            {
                if (current == null)
                {
                    return null;
                }

                current = current.Find(names[index]);
            }

            return current;
        }

        private static int ClearRuntimeChildren(Transform parent, Func<LevelGenStep, bool> shouldPreserveStep = null)
        {
            if (parent == null)
            {
                return 0;
            }

            int removed = 0;
            LevelGenStep[] steps = parent.GetComponentsInChildren<LevelGenStep>(true);
            for (int stepIndex = 0; stepIndex < steps.Length; stepIndex++)
            {
                Transform stepTransform = steps[stepIndex] != null ? steps[stepIndex].transform : null;
                if (stepTransform == null)
                {
                    continue;
                }

                if (shouldPreserveStep != null && shouldPreserveStep(steps[stepIndex]))
                {
                    continue;
                }

                removed += ClearDirectGeneratedChildren(stepTransform, false);
            }

            removed += ClearDirectGeneratedChildren(parent, true);

            if (removed > 0)
            {
                DaLog.Info("Cleared runtime generated children before regeneration. parent=" + parent.name + ", removed=" + removed);
            }

            return removed;
        }

        private static int ClearDirectGeneratedChildren(Transform parent, bool preserveGenerationNodes)
        {
            int removed = 0;
            for (int index = parent.childCount - 1; index >= 0; index--)
            {
                Transform child = parent.GetChild(index);
                if (child == null ||
                    ShouldPreserveChildForCustomBlank(child) ||
                    (preserveGenerationNodes &&
                     (child.GetComponent<LevelGenStep>() != null ||
                      child.GetComponent<PropGrouper>() != null)))
                {
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(child.gameObject);
                removed++;
            }

            return removed;
        }

        private static bool ShouldPreserveChildForCustomBlank(Transform child)
        {
            if (child == null)
            {
                return false;
            }

            Transform parent = child.parent;
            return parent != null &&
                   string.Equals(parent.name, "Mechanics", StringComparison.Ordinal) &&
                   string.Equals(child.name, "RisingLava", StringComparison.Ordinal);
        }

        private static bool ShouldPreserveStepForCustomBlank(DaSegmentData segment, LevelGenStep step)
        {
            if (step == null)
            {
                return false;
            }

            if (segment != null && string.Equals(segment.SegmentName, "Volcano_Segment", StringComparison.Ordinal))
            {
                return false;
            }

            if (CustomBlankPreservedStepNames.Contains(step.name ?? string.Empty))
            {
                return true;
            }

            return false;
        }

        private static bool TryInvokeLevelGenGo(LevelGenStep step)
        {
            try
            {
                MethodInfo method = step.GetType().GetMethod("Go", InstanceFieldFlags);
                if (method == null)
                {
                    method = typeof(LevelGenStep).GetMethod("Go", InstanceFieldFlags);
                }

                if (method == null)
                {
                    DaLog.OnceWarn("levelgen-go-missing:" + step.GetType().FullName, "LevelGenStep.Go method was not found for " + step.GetType().FullName);
                    return false;
                }

                method.Invoke(step, null);
                return true;
            }
            catch (Exception ex)
            {
                DaLog.OnceWarn("levelgen-go-failed:" + step.GetType().FullName, "Failed to invoke LevelGenStep.Go for " + step.GetType().FullName + ": " + ex.Message);
                return false;
            }
        }

        public static int ApplyImportedData(DaTerrainData current, DaTerrainData imported)
        {
            if (current == null || current.Map == null || imported == null || imported.Map == null)
            {
                DaLog.Warn("Cannot import terrain data because current or imported map data is missing.");
                return 0;
            }

            int changed = 0;
            int modeChanges = ApplyImportedSegmentEditModes(current, imported);
            int placementConfigChanges = ApplyImportedPlacementConfig(current, imported);
            Dictionary<string, DaPropertyData> currentProperties = BuildPropertyIndex(current);
            Dictionary<string, DaPropertyData> importedProperties = BuildPropertyIndex(imported);

            foreach (KeyValuePair<string, DaPropertyData> pair in importedProperties)
            {
                if (!currentProperties.TryGetValue(pair.Key, out DaPropertyData currentProperty))
                {
                    continue;
                }

                DaRuntimePropertyOwner owner = FindOwner(current, pair.Key);
                if (owner == null)
                {
                    continue;
                }

                string rawValue = FormatForEdit(pair.Value.Value);
                bool ok = owner.Constraint != null
                    ? TrySetConstraintProperty(owner.Constraint, currentProperty, rawValue)
                    : TrySetStepProperty(owner.Step, currentProperty, rawValue);

                if (ok)
                {
                    changed++;
                }
            }

            DaLog.Info(string.Format(
                "Imported terrain values. sourceMap={0}, currentMap={1}, applied={2}, segmentModes={3}, placementConfigs={4}",
                imported.Map.MapKey,
                current.Map.MapKey,
                changed,
                modeChanges,
                placementConfigChanges));
            return changed + modeChanges + placementConfigChanges;
        }

        private static int ApplyImportedSegmentEditModes(DaTerrainData current, DaTerrainData imported)
        {
            if (current == null || current.Map == null || current.Map.Segments == null ||
                imported == null || imported.Map == null || imported.Map.Segments == null)
            {
                return 0;
            }

            Dictionary<string, DaSegmentEditMode> importedModes = new Dictionary<string, DaSegmentEditMode>(StringComparer.Ordinal);
            for (int index = 0; index < imported.Map.Segments.Count; index++)
            {
                DaSegmentData segment = imported.Map.Segments[index];
                if (segment != null && !string.IsNullOrWhiteSpace(segment.SegmentName))
                {
                    importedModes[segment.SegmentName] = segment.EditMode;
                }
            }

            int changed = 0;
            for (int index = 0; index < current.Map.Segments.Count; index++)
            {
                DaSegmentData segment = current.Map.Segments[index];
                if (segment == null || string.IsNullOrWhiteSpace(segment.SegmentName))
                {
                    continue;
                }

                if (!importedModes.TryGetValue(segment.SegmentName, out DaSegmentEditMode importedMode) ||
                    segment.EditMode == importedMode)
                {
                    continue;
                }

                DaSegmentEditMode oldMode = segment.EditMode;
                segment.EditMode = importedMode;
                changed++;
                DaLog.Info("Imported segment edit mode. segment=" + segment.SegmentName + ", old=" + oldMode + ", new=" + importedMode);
            }

            return changed;
        }

        public static int ApplyImportedPlacementConfig(DaTerrainData current, DaTerrainData imported)
        {
            if (current == null || current.Map == null || current.Map.Segments == null ||
                imported == null || imported.Map == null || imported.Map.Segments == null)
            {
                return 0;
            }

            Dictionary<string, DaSegmentData> importedSegments = new Dictionary<string, DaSegmentData>(StringComparer.Ordinal);
            for (int index = 0; index < imported.Map.Segments.Count; index++)
            {
                DaSegmentData segment = imported.Map.Segments[index];
                if (segment != null && !string.IsNullOrWhiteSpace(segment.SegmentName))
                {
                    importedSegments[segment.SegmentName] = segment;
                }
            }

            int changed = 0;
            for (int index = 0; index < current.Map.Segments.Count; index++)
            {
                DaSegmentData currentSegment = current.Map.Segments[index];
                if (currentSegment == null ||
                    string.IsNullOrWhiteSpace(currentSegment.SegmentName) ||
                    !importedSegments.TryGetValue(currentSegment.SegmentName, out DaSegmentData importedSegment))
                {
                    continue;
                }

                if (!HasPlacementConfig(importedSegment))
                {
                    continue;
                }

                List<DaSubAreaData> importedSubAreas = importedSegment.SubAreas ?? new List<DaSubAreaData>();
                List<DaPlacementRuleData> importedRules = importedSegment.PlacementRules ?? new List<DaPlacementRuleData>();
                currentSegment.SubAreas = CloneSubAreas(importedSubAreas);
                currentSegment.PlacementRules = ClonePlacementRules(importedRules, currentSegment.SubAreas);
                changed++;
                DaLog.Info("Imported placement config. segment=" + currentSegment.SegmentName + ", subAreas=" + currentSegment.SubAreas.Count + ", rules=" + currentSegment.PlacementRules.Count);
            }

            return changed;
        }

        private static bool HasPlacementConfig(DaSegmentData segment)
        {
            return segment != null &&
                   (segment.HasPlacementConfigSpecified ||
                    segment.SubAreas != null && segment.SubAreas.Count > 0 ||
                    segment.PlacementRules != null && segment.PlacementRules.Count > 0);
        }

        public static List<DaSubAreaData> CloneSubAreas(List<DaSubAreaData> source)
        {
            List<DaSubAreaData> result = new List<DaSubAreaData>();
            if (source == null)
            {
                return result;
            }

            for (int index = 0; index < source.Count; index++)
            {
                DaSubAreaData area = source[index];
                if (area == null)
                {
                    continue;
                }

                result.Add(new DaSubAreaData
                {
                    Id = area.Id,
                    DisplayName = area.DisplayName,
                    Shape = area.Shape,
                    CenterOffset = CloneVector(area.CenterOffset),
                    Size = CloneVector(area.Size),
                    Enabled = area.Enabled
                });
            }

            return result;
        }

        public static List<DaPlacementRuleData> ClonePlacementRules(List<DaPlacementRuleData> source, List<DaSubAreaData> subAreas)
        {
            List<DaPlacementRuleData> result = new List<DaPlacementRuleData>();
            if (source == null)
            {
                return result;
            }

            for (int index = 0; index < source.Count; index++)
            {
                DaPlacementRuleData rule = source[index];
                if (rule == null)
                {
                    continue;
                }

                result.Add(new DaPlacementRuleData
                {
                    Id = rule.Id,
                    DisplayName = rule.DisplayName,
                    Enabled = rule.Enabled,
                    RegistryId = rule.RegistryId,
                    RegistryDisplayName = rule.RegistryDisplayName,
                    TargetSubAreaId = ResolveSubAreaId(rule.TargetSubAreaId, subAreas),
                    Count = rule.Count,
                    MinScale = rule.MinScale,
                    MaxScale = rule.MaxScale,
                    PlacementMode = rule.PlacementMode,
                    RotationMode = rule.RotationMode,
                    OwnershipMode = rule.OwnershipMode,
                    LocalOffset = CloneVector(rule.LocalOffset)
                });
            }

            return result;
        }

        private static string ResolveSubAreaId(string value, List<DaSubAreaData> subAreas)
        {
            if (subAreas == null || subAreas.Count == 0)
            {
                return value;
            }

            string requested = value ?? string.Empty;
            for (int index = 0; index < subAreas.Count; index++)
            {
                DaSubAreaData area = subAreas[index];
                if (area != null && string.Equals(area.Id, requested, StringComparison.Ordinal))
                {
                    return area.Id;
                }
            }

            for (int index = 0; index < subAreas.Count; index++)
            {
                DaSubAreaData area = subAreas[index];
                if (area != null && string.Equals(area.DisplayName, requested, StringComparison.Ordinal))
                {
                    return area.Id;
                }
            }

            return subAreas[0] != null ? subAreas[0].Id : requested;
        }

        private static DaVector3Data CloneVector(DaVector3Data source)
        {
            return source == null
                ? DaVector3Data.Zero()
                : new DaVector3Data
                {
                    X = source.X,
                    Y = source.Y,
                    Z = source.Z
                };
        }

        public static bool IsChanged(DaPropertyData property)
        {
            if (property == null)
            {
                return false;
            }

            return !string.Equals(
                FormatForEdit(property.Value),
                FormatForEdit(property.InitialValue),
                StringComparison.Ordinal);
        }

        public static string FormatForEdit(object value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            if (value is JValue jValue)
            {
                return FormatForEdit(jValue.Value);
            }

            if (value is JObject jObject)
            {
                return FormatJsonObjectForEdit(jObject);
            }

            if (value is float f)
            {
                return f.ToString("0.###", CultureInfo.InvariantCulture);
            }

            if (value is double d)
            {
                return d.ToString("0.###", CultureInfo.InvariantCulture);
            }

            if (value is Vector2 v2)
            {
                return string.Format(CultureInfo.InvariantCulture, "{0:0.###},{1:0.###}", v2.x, v2.y);
            }

            if (value is Vector3 v3)
            {
                return string.Format(CultureInfo.InvariantCulture, "{0:0.###},{1:0.###},{2:0.###}", v3.x, v3.y, v3.z);
            }

            if (value is Vector2Int v2i)
            {
                return string.Format(CultureInfo.InvariantCulture, "{0},{1}", v2i.x, v2i.y);
            }

            if (value is Vector3Int v3i)
            {
                return string.Format(CultureInfo.InvariantCulture, "{0},{1},{2}", v3i.x, v3i.y, v3i.z);
            }

            if (value is bool b)
            {
                return b ? "true" : "false";
            }

            if (value is IConvertible convertible)
            {
                return convertible.ToString(CultureInfo.InvariantCulture);
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        public static bool TryGetVectorParts(object value, out string[] parts)
        {
            parts = null;
            if (value == null)
            {
                return false;
            }

            if (value is Vector2 v2)
            {
                parts = new[] { FormatForEdit(v2.x), FormatForEdit(v2.y) };
                return true;
            }

            if (value is Vector3 v3)
            {
                parts = new[] { FormatForEdit(v3.x), FormatForEdit(v3.y), FormatForEdit(v3.z) };
                return true;
            }

            if (value is Vector2Int v2i)
            {
                parts = new[] { FormatForEdit(v2i.x), FormatForEdit(v2i.y) };
                return true;
            }

            if (value is Vector3Int v3i)
            {
                parts = new[] { FormatForEdit(v3i.x), FormatForEdit(v3i.y), FormatForEdit(v3i.z) };
                return true;
            }

            if (value is JObject jObject)
            {
                JToken x = jObject["x"];
                JToken y = jObject["y"];
                JToken z = jObject["z"];
                if (x != null && y != null && z != null)
                {
                    parts = new[] { Convert.ToString(x, CultureInfo.InvariantCulture), Convert.ToString(y, CultureInfo.InvariantCulture), Convert.ToString(z, CultureInfo.InvariantCulture) };
                    return true;
                }

                if (x != null && y != null)
                {
                    parts = new[] { Convert.ToString(x, CultureInfo.InvariantCulture), Convert.ToString(y, CultureInfo.InvariantCulture) };
                    return true;
                }
            }

            return false;
        }

        private static string FormatJsonObjectForEdit(JObject value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            JToken x = value["x"];
            JToken y = value["y"];
            JToken z = value["z"];
            JToken w = value["w"];

            if (x != null && y != null && z != null && w != null)
            {
                return string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3}", x, y, z, w);
            }

            if (x != null && y != null && z != null)
            {
                return string.Format(CultureInfo.InvariantCulture, "{0},{1},{2}", x, y, z);
            }

            if (x != null && y != null)
            {
                return string.Format(CultureInfo.InvariantCulture, "{0},{1}", x, y);
            }

            return value.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static bool TrySetProperty(object source, string ownerName, DaPropertyData property, string rawValue)
        {
            if (source == null || property == null || string.IsNullOrWhiteSpace(property.Name))
            {
                return false;
            }

            FieldInfo field = GetFieldInfo(source.GetType(), property.Name);
            if (field == null)
            {
                DaLog.Warn("Field not found during edit: " + source.GetType().FullName + "." + property.Name);
                return false;
            }

            try
            {
                if (!TryConvert(rawValue, field.FieldType, out object converted))
                {
                    DaLog.Warn("Invalid value for " + ownerName + "." + property.Name + ": " + rawValue);
                    return false;
                }

                object oldValue = field.GetValue(source);
                field.SetValue(source, converted);
                property.Value = converted;
                property.EditText = FormatForEdit(converted);

                DaLog.Info(string.Format(
                    "Edited property: owner={0}, field={1}, old={2}, new={3}",
                    ownerName,
                    property.Name,
                    FormatForEdit(oldValue),
                    FormatForEdit(converted)));
                return true;
            }
            catch (Exception ex)
            {
                DaLog.Error("Failed to edit " + ownerName + "." + property.Name + ": " + ex);
                return false;
            }
        }

        private static bool TryConvert(string rawValue, Type targetType, out object converted)
        {
            converted = null;
            string value = rawValue ?? string.Empty;
            string[] parts = value.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (targetType == typeof(string))
            {
                converted = value;
                return true;
            }

            if (targetType == typeof(bool))
            {
                if (bool.TryParse(value, out bool boolValue))
                {
                    converted = boolValue;
                    return true;
                }

                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int boolInt))
                {
                    converted = boolInt != 0;
                    return true;
                }

                return false;
            }

            if (targetType.IsEnum)
            {
                try
                {
                    converted = Enum.Parse(targetType, value, true);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            if (targetType == typeof(int))
            {
                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue))
                {
                    converted = intValue;
                    return true;
                }

                return false;
            }

            if (targetType == typeof(float))
            {
                if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatValue))
                {
                    converted = floatValue;
                    return true;
                }

                return false;
            }

            if (targetType == typeof(double))
            {
                if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double doubleValue))
                {
                    converted = doubleValue;
                    return true;
                }

                return false;
            }

            if (targetType == typeof(LayerMask))
            {
                if (TryParseLayerMask(value, out LayerMask layerMask))
                {
                    converted = layerMask;
                    return true;
                }

                return false;
            }

            if (targetType == typeof(Vector2) && parts.Length >= 2)
            {
                if (TryFloat(parts[0], out float x) && TryFloat(parts[1], out float y))
                {
                    converted = new Vector2(x, y);
                    return true;
                }
            }

            if (targetType == typeof(Vector3) && parts.Length >= 3)
            {
                if (TryFloat(parts[0], out float x) && TryFloat(parts[1], out float y) && TryFloat(parts[2], out float z))
                {
                    converted = new Vector3(x, y, z);
                    return true;
                }
            }

            if (targetType == typeof(Vector2Int) && parts.Length >= 2)
            {
                if (TryInt(parts[0], out int x) && TryInt(parts[1], out int y))
                {
                    converted = new Vector2Int(x, y);
                    return true;
                }
            }

            if (targetType == typeof(Vector3Int) && parts.Length >= 3)
            {
                if (TryInt(parts[0], out int x) && TryInt(parts[1], out int y) && TryInt(parts[2], out int z))
                {
                    converted = new Vector3Int(x, y, z);
                    return true;
                }
            }

            return false;
        }

        private static bool TryFloat(string value, out float result)
        {
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }

        private static bool TryInt(string value, out int result)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
        }

        private static bool TryParseLayerMask(string rawValue, out LayerMask layerMask)
        {
            layerMask = default;

            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return false;
            }

            if (int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int rawInt))
            {
                layerMask.value = rawInt;
                return true;
            }

            string trimmed = rawValue.Trim();
            if (!trimmed.StartsWith("{", StringComparison.Ordinal))
            {
                return false;
            }

            try
            {
                JObject jObject = JObject.Parse(trimmed);
                JToken valueToken = jObject["value"];
                if (valueToken == null)
                {
                    return false;
                }

                if (valueToken.Type == JTokenType.Integer)
                {
                    layerMask.value = valueToken.Value<int>();
                    return true;
                }

                string valueText = Convert.ToString(valueToken, CultureInfo.InvariantCulture);
                if (int.TryParse(valueText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
                {
                    layerMask.value = parsed;
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private static FieldInfo GetFieldInfo(Type type, string fieldName)
        {
            if (type == null || string.IsNullOrWhiteSpace(fieldName))
            {
                return null;
            }

            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo field = current.GetField(fieldName, InstanceFieldFlags | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    return field;
                }
            }

            return null;
        }

        private static Dictionary<string, DaPropertyData> BuildPropertyIndex(DaTerrainData data)
        {
            Dictionary<string, DaPropertyData> index = new Dictionary<string, DaPropertyData>(StringComparer.Ordinal);
            if (data == null || data.Map == null || data.Map.Segments == null)
            {
                return index;
            }

            for (int segmentIndex = 0; segmentIndex < data.Map.Segments.Count; segmentIndex++)
            {
                DaSegmentData segment = data.Map.Segments[segmentIndex];
                if (segment == null || segment.Groupers == null)
                {
                    continue;
                }

                for (int grouperIndex = 0; grouperIndex < segment.Groupers.Count; grouperIndex++)
                {
                    DaPropGrouperData grouper = segment.Groupers[grouperIndex];
                    if (grouper == null || grouper.Steps == null)
                    {
                        continue;
                    }

                    for (int stepIndex = 0; stepIndex < grouper.Steps.Count; stepIndex++)
                    {
                        DaLevelGenStepData step = grouper.Steps[stepIndex];
                        AddProperties(index, BuildStepKey(segment, grouper, step), step != null ? step.Properties : null);
                        AddConstraintProperties(index, BuildStepKey(segment, grouper, step), "mod", step != null ? step.Modifiers : null);
                        AddConstraintProperties(index, BuildStepKey(segment, grouper, step), "constraint", step != null ? step.Constraints : null);
                        AddConstraintProperties(index, BuildStepKey(segment, grouper, step), "post", step != null ? step.PostConstraints : null);
                    }
                }
            }

            return index;
        }

        private static void AddProperties(Dictionary<string, DaPropertyData> index, string prefix, List<DaPropertyData> properties)
        {
            if (properties == null)
            {
                return;
            }

            for (int propertyIndex = 0; propertyIndex < properties.Count; propertyIndex++)
            {
                DaPropertyData property = properties[propertyIndex];
                if (property != null)
                {
                    index[prefix + "/prop/" + property.Name] = property;
                }
            }
        }

        private static void AddConstraintProperties(Dictionary<string, DaPropertyData> index, string prefix, string listName, List<DaConstraintData> constraints)
        {
            if (constraints == null)
            {
                return;
            }

            for (int constraintIndex = 0; constraintIndex < constraints.Count; constraintIndex++)
            {
                DaConstraintData constraint = constraints[constraintIndex];
                if (constraint == null || constraint.Properties == null)
                {
                    continue;
                }

                for (int propertyIndex = 0; propertyIndex < constraint.Properties.Count; propertyIndex++)
                {
                    DaPropertyData property = constraint.Properties[propertyIndex];
                    if (property != null)
                    {
                        index[prefix + "/" + listName + "/" + constraintIndex + "/" + constraint.Type + "/" + property.Name] = property;
                    }
                }
            }
        }

        private static DaRuntimePropertyOwner FindOwner(DaTerrainData data, string propertyKey)
        {
            if (data == null || data.Map == null || data.Map.Segments == null)
            {
                return null;
            }

            for (int segmentIndex = 0; segmentIndex < data.Map.Segments.Count; segmentIndex++)
            {
                DaSegmentData segment = data.Map.Segments[segmentIndex];
                if (segment == null || segment.Groupers == null)
                {
                    continue;
                }

                for (int grouperIndex = 0; grouperIndex < segment.Groupers.Count; grouperIndex++)
                {
                    DaPropGrouperData grouper = segment.Groupers[grouperIndex];
                    if (grouper == null || grouper.Steps == null)
                    {
                        continue;
                    }

                    for (int stepIndex = 0; stepIndex < grouper.Steps.Count; stepIndex++)
                    {
                        DaLevelGenStepData step = grouper.Steps[stepIndex];
                        string prefix = BuildStepKey(segment, grouper, step);
                        if (!propertyKey.StartsWith(prefix + "/", StringComparison.Ordinal))
                        {
                            continue;
                        }

                        if (propertyKey.Contains("/mod/"))
                        {
                            return new DaRuntimePropertyOwner { Step = step, Constraint = FindConstraint(step.Modifiers, propertyKey) };
                        }

                        if (propertyKey.Contains("/constraint/"))
                        {
                            return new DaRuntimePropertyOwner { Step = step, Constraint = FindConstraint(step.Constraints, propertyKey) };
                        }

                        if (propertyKey.Contains("/post/"))
                        {
                            return new DaRuntimePropertyOwner { Step = step, Constraint = FindConstraint(step.PostConstraints, propertyKey) };
                        }

                        return new DaRuntimePropertyOwner { Step = step };
                    }
                }
            }

            return null;
        }

        private static DaConstraintData FindConstraint(List<DaConstraintData> constraints, string propertyKey)
        {
            if (constraints == null)
            {
                return null;
            }

            string propertyName = ExtractPropertyName(propertyKey);
            string constraintType = ExtractConstraintType(propertyKey);

            for (int index = 0; index < constraints.Count; index++)
            {
                DaConstraintData constraint = constraints[index];
                if (constraint == null)
                {
                    continue;
                }

                if (propertyKey.Contains("/" + index + "/" + constraint.Type + "/"))
                {
                    return constraint;
                }
            }

            if (!string.IsNullOrWhiteSpace(constraintType))
            {
                for (int index = 0; index < constraints.Count; index++)
                {
                    DaConstraintData constraint = constraints[index];
                    if (constraint == null || !string.Equals(constraint.Type, constraintType, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (constraint.Properties == null || constraint.Properties.Count == 0)
                    {
                        return constraint;
                    }

                    if (string.IsNullOrWhiteSpace(propertyName))
                    {
                        return constraint;
                    }

                    for (int propertyIndex = 0; propertyIndex < constraint.Properties.Count; propertyIndex++)
                    {
                        DaPropertyData property = constraint.Properties[propertyIndex];
                        if (property != null && string.Equals(property.Name, propertyName, StringComparison.Ordinal))
                        {
                            return constraint;
                        }
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(propertyName))
            {
                for (int index = 0; index < constraints.Count; index++)
                {
                    DaConstraintData constraint = constraints[index];
                    if (constraint == null || constraint.Properties == null)
                    {
                        continue;
                    }

                    for (int propertyIndex = 0; propertyIndex < constraint.Properties.Count; propertyIndex++)
                    {
                        DaPropertyData property = constraint.Properties[propertyIndex];
                        if (property != null && string.Equals(property.Name, propertyName, StringComparison.Ordinal))
                        {
                            return constraint;
                        }
                    }
                }
            }

            return null;
        }

        private static string ExtractPropertyName(string propertyKey)
        {
            if (string.IsNullOrWhiteSpace(propertyKey))
            {
                return string.Empty;
            }

            int slashIndex = propertyKey.LastIndexOf('/');
            return slashIndex >= 0 && slashIndex < propertyKey.Length - 1
                ? propertyKey.Substring(slashIndex + 1)
                : propertyKey;
        }

        private static string ExtractConstraintType(string propertyKey)
        {
            if (string.IsNullOrWhiteSpace(propertyKey))
            {
                return string.Empty;
            }

            string[] parts = propertyKey.Split('/');
            if (parts.Length < 7)
            {
                return string.Empty;
            }

            return parts[6];
        }

        private static void LogSegmentGenerationTraceEnter(DaSegmentData segment)
        {
            if (!GenerationTraceEnabled || segment == null)
            {
                return;
            }

            int grouperCount = segment.Groupers != null ? segment.Groupers.Count : 0;
            DaLog.Info("Generation trace: RunSegment enter. segment=" + (segment.SegmentName ?? string.Empty) +
                ", mode=" + segment.EditMode +
                ", variant=" + (segment.NormalizedVariantName ?? string.Empty) +
                ", groupers=" + grouperCount.ToString(CultureInfo.InvariantCulture) +
                ", missingGroupers=" + CountMissingRuntimeGroupers(segment).ToString(CultureInfo.InvariantCulture) +
                ", missingSteps=" + CountMissingRuntimeSteps(segment).ToString(CultureInfo.InvariantCulture) +
                ", stack=" + FormatGenerationStackTrace());
        }

        private static SegmentGenerationSnapshot BuildSegmentGenerationSnapshot(DaSegmentData segment)
        {
            SegmentGenerationSnapshot snapshot = new SegmentGenerationSnapshot();
            if (segment == null || segment.Groupers == null)
            {
                return snapshot;
            }

            for (int index = 0; index < segment.Groupers.Count; index++)
            {
                DaPropGrouperData grouper = segment.Groupers[index];
                GrouperGenerationSnapshot grouperSnapshot = BuildGrouperGenerationSnapshot(grouper);
                snapshot.Groupers[BuildGrouperGenerationKey(index, grouper)] = grouperSnapshot;
                snapshot.GrouperCount++;
                snapshot.TotalDirectChildren += grouperSnapshot.TotalDirectChildren;
                snapshot.TotalDescendants += grouperSnapshot.TotalDescendants;
                snapshot.MissingRuntimeGroupers += grouperSnapshot.RuntimeReferenceMissing ? 1 : 0;
                snapshot.MissingRuntimeSteps += grouperSnapshot.MissingRuntimeSteps;
            }

            return snapshot;
        }

        private static GrouperGenerationSnapshot BuildGrouperGenerationSnapshot(DaPropGrouperData grouper)
        {
            GrouperGenerationSnapshot snapshot = new GrouperGenerationSnapshot
            {
                GrouperName = grouper != null ? grouper.GrouperName : string.Empty,
                GrouperPath = grouper != null ? grouper.HierarchyPath : string.Empty,
                RuntimeReferenceMissing = grouper == null || grouper.SourceObject == null
            };

            if (grouper == null)
            {
                return snapshot;
            }

            if (grouper.SourceObject != null)
            {
                Transform grouperTransform = grouper.SourceObject.transform;
                snapshot.GrouperDirectChildren = CountDirectChildren(grouperTransform);
                snapshot.GrouperDescendants = CountDescendantObjects(grouperTransform);
                if (string.IsNullOrWhiteSpace(snapshot.GrouperPath))
                {
                    snapshot.GrouperPath = GetRuntimeHierarchyPath(grouperTransform);
                }
            }

            for (int index = 0; grouper.Steps != null && index < grouper.Steps.Count; index++)
            {
                DaLevelGenStepData step = grouper.Steps[index];
                StepGenerationSnapshot stepSnapshot = BuildStepGenerationSnapshot(step, index);
                snapshot.Steps[BuildStepGenerationKey(index, step)] = stepSnapshot;
                snapshot.StepCount++;
                snapshot.TotalDirectChildren += stepSnapshot.DirectChildCount;
                snapshot.TotalDescendants += stepSnapshot.DescendantCount;
                snapshot.MissingRuntimeSteps += stepSnapshot.RuntimeReferenceMissing ? 1 : 0;
            }

            return snapshot;
        }

        private static StepGenerationSnapshot BuildStepGenerationSnapshot(DaLevelGenStepData step, int index)
        {
            StepGenerationSnapshot snapshot = new StepGenerationSnapshot
            {
                StepIndex = index,
                StepName = step != null ? step.StepName : string.Empty,
                StepType = step != null ? step.StepType : string.Empty,
                StepPath = step != null ? step.HierarchyPath : string.Empty,
                RuntimeReferenceMissing = step == null || step.SourceObject == null
            };

            if (step == null || step.SourceObject == null)
            {
                return snapshot;
            }

            Transform stepTransform = step.SourceObject.transform;
            snapshot.DirectChildCount = CountDirectChildren(stepTransform);
            snapshot.DescendantCount = CountDescendantObjects(stepTransform);
            if (string.IsNullOrWhiteSpace(snapshot.StepPath))
            {
                snapshot.StepPath = GetRuntimeHierarchyPath(stepTransform);
            }

            return snapshot;
        }

        private static void LogGrouperGenerationSnapshot(string phase, DaSegmentData segment, DaPropGrouperData grouper, GrouperGenerationSnapshot snapshot, string context)
        {
            if (!GenerationTraceEnabled || snapshot == null)
            {
                return;
            }

            DaLog.Info("Generation trace: grouper " + phase +
                ". context=" + (context ?? string.Empty) +
                ", segment=" + (segment != null ? segment.SegmentName ?? string.Empty : string.Empty) +
                ", grouper=" + (grouper != null ? grouper.GrouperName ?? string.Empty : snapshot.GrouperName ?? string.Empty) +
                ", timing=" + (grouper != null && grouper.SourceObject != null ? grouper.SourceObject.timing.ToString() : string.Empty) +
                ", path=" + (snapshot.GrouperPath ?? string.Empty) +
                ", steps=" + snapshot.StepCount.ToString(CultureInfo.InvariantCulture) +
                ", directChildren=" + snapshot.TotalDirectChildren.ToString(CultureInfo.InvariantCulture) +
                ", descendants=" + snapshot.TotalDescendants.ToString(CultureInfo.InvariantCulture) +
                ", grouperDirectChildren=" + snapshot.GrouperDirectChildren.ToString(CultureInfo.InvariantCulture) +
                ", missingSteps=" + snapshot.MissingRuntimeSteps.ToString(CultureInfo.InvariantCulture));
        }

        private static void LogSegmentGenerationDelta(DaSegmentData segment, SegmentGenerationSnapshot before, SegmentGenerationSnapshot after, string context)
        {
            if (!GenerationTraceEnabled || before == null || after == null)
            {
                return;
            }

            if (before.TotalDirectChildren == after.TotalDirectChildren &&
                before.TotalDescendants == after.TotalDescendants &&
                before.MissingRuntimeGroupers == after.MissingRuntimeGroupers &&
                before.MissingRuntimeSteps == after.MissingRuntimeSteps)
            {
                return;
            }

            List<string> changedGroupers = new List<string>();
            foreach (KeyValuePair<string, GrouperGenerationSnapshot> pair in before.Groupers)
            {
                GrouperGenerationSnapshot beforeGrouper = pair.Value;
                GrouperGenerationSnapshot afterGrouper;
                if (beforeGrouper == null || !after.Groupers.TryGetValue(pair.Key, out afterGrouper) || afterGrouper == null)
                {
                    continue;
                }

                if (beforeGrouper.TotalDirectChildren != afterGrouper.TotalDirectChildren ||
                    beforeGrouper.TotalDescendants != afterGrouper.TotalDescendants ||
                    beforeGrouper.MissingRuntimeSteps != afterGrouper.MissingRuntimeSteps)
                {
                    changedGroupers.Add((beforeGrouper.GrouperName ?? string.Empty) +
                        ":" + beforeGrouper.TotalDirectChildren.ToString(CultureInfo.InvariantCulture) +
                        "->" + afterGrouper.TotalDirectChildren.ToString(CultureInfo.InvariantCulture) +
                        "/desc " + beforeGrouper.TotalDescendants.ToString(CultureInfo.InvariantCulture) +
                        "->" + afterGrouper.TotalDescendants.ToString(CultureInfo.InvariantCulture));
                }
            }

            DaLog.Warn("Generation trace: segment delta. context=" + (context ?? string.Empty) +
                ", segment=" + (segment != null ? segment.SegmentName ?? string.Empty : string.Empty) +
                ", mode=" + (segment != null ? segment.EditMode.ToString() : string.Empty) +
                ", directChildren=" + before.TotalDirectChildren.ToString(CultureInfo.InvariantCulture) +
                "->" + after.TotalDirectChildren.ToString(CultureInfo.InvariantCulture) +
                ", descendants=" + before.TotalDescendants.ToString(CultureInfo.InvariantCulture) +
                "->" + after.TotalDescendants.ToString(CultureInfo.InvariantCulture) +
                ", missingGroupers=" + before.MissingRuntimeGroupers.ToString(CultureInfo.InvariantCulture) +
                "->" + after.MissingRuntimeGroupers.ToString(CultureInfo.InvariantCulture) +
                ", missingSteps=" + before.MissingRuntimeSteps.ToString(CultureInfo.InvariantCulture) +
                "->" + after.MissingRuntimeSteps.ToString(CultureInfo.InvariantCulture) +
                ", changedGroupers=" + FormatLimitedList(changedGroupers, GenerationTraceMaxStepChanges));
        }

        private static void LogGrouperGenerationDelta(DaSegmentData segment, DaPropGrouperData grouper, GrouperGenerationSnapshot before, GrouperGenerationSnapshot after, string context)
        {
            if (!GenerationTraceEnabled || before == null || after == null)
            {
                return;
            }

            List<string> changedSteps = new List<string>();
            List<string> zeroedSteps = new List<string>();
            foreach (KeyValuePair<string, StepGenerationSnapshot> pair in before.Steps)
            {
                StepGenerationSnapshot beforeStep = pair.Value;
                StepGenerationSnapshot afterStep;
                if (beforeStep == null || !after.Steps.TryGetValue(pair.Key, out afterStep) || afterStep == null)
                {
                    continue;
                }

                bool changed = beforeStep.DirectChildCount != afterStep.DirectChildCount ||
                    beforeStep.DescendantCount != afterStep.DescendantCount ||
                    beforeStep.RuntimeReferenceMissing != afterStep.RuntimeReferenceMissing;
                if (!changed)
                {
                    continue;
                }

                string delta = FormatStepGenerationDelta(beforeStep, afterStep);
                changedSteps.Add(delta);
                if (beforeStep.DirectChildCount > 0 && afterStep.DirectChildCount == 0)
                {
                    zeroedSteps.Add(delta);
                }
            }

            if (before.TotalDirectChildren == after.TotalDirectChildren &&
                before.TotalDescendants == after.TotalDescendants &&
                before.MissingRuntimeSteps == after.MissingRuntimeSteps &&
                changedSteps.Count == 0)
            {
                return;
            }

            DaLog.Warn("Generation trace: grouper delta. context=" + (context ?? string.Empty) +
                ", segment=" + (segment != null ? segment.SegmentName ?? string.Empty : string.Empty) +
                ", grouper=" + (grouper != null ? grouper.GrouperName ?? string.Empty : before.GrouperName ?? string.Empty) +
                ", directChildren=" + before.TotalDirectChildren.ToString(CultureInfo.InvariantCulture) +
                "->" + after.TotalDirectChildren.ToString(CultureInfo.InvariantCulture) +
                ", descendants=" + before.TotalDescendants.ToString(CultureInfo.InvariantCulture) +
                "->" + after.TotalDescendants.ToString(CultureInfo.InvariantCulture) +
                ", missingSteps=" + before.MissingRuntimeSteps.ToString(CultureInfo.InvariantCulture) +
                "->" + after.MissingRuntimeSteps.ToString(CultureInfo.InvariantCulture) +
                ", zeroedSteps=" + FormatLimitedList(zeroedSteps, GenerationTraceMaxStepChanges) +
                ", changedSteps=" + FormatLimitedList(changedSteps, GenerationTraceMaxStepChanges));
        }

        private static void LogStepGenerationDelta(DaSegmentData segment, DaPropGrouperData grouper, StepGenerationSnapshot before, StepGenerationSnapshot after, string context, int removedBeforeRun)
        {
            if (!GenerationTraceEnabled || before == null || after == null)
            {
                return;
            }

            if (before.DirectChildCount == after.DirectChildCount &&
                before.DescendantCount == after.DescendantCount &&
                before.RuntimeReferenceMissing == after.RuntimeReferenceMissing)
            {
                return;
            }

            DaLog.Warn("Generation trace: step delta. context=" + (context ?? string.Empty) +
                ", segment=" + (segment != null ? segment.SegmentName ?? string.Empty : string.Empty) +
                ", grouper=" + (grouper != null ? grouper.GrouperName ?? string.Empty : string.Empty) +
                ", removedBeforeRun=" + removedBeforeRun.ToString(CultureInfo.InvariantCulture) +
                ", " + FormatStepGenerationDelta(before, after));
        }

        private static string FormatStepGenerationDelta(StepGenerationSnapshot before, StepGenerationSnapshot after)
        {
            string name = !string.IsNullOrWhiteSpace(before.StepName) ? before.StepName : after.StepName;
            string type = !string.IsNullOrWhiteSpace(before.StepType) ? before.StepType : after.StepType;
            string path = !string.IsNullOrWhiteSpace(before.StepPath) ? before.StepPath : after.StepPath;
            return name + "(" + type + ")" +
                ":children " + before.DirectChildCount.ToString(CultureInfo.InvariantCulture) +
                "->" + after.DirectChildCount.ToString(CultureInfo.InvariantCulture) +
                ",desc " + before.DescendantCount.ToString(CultureInfo.InvariantCulture) +
                "->" + after.DescendantCount.ToString(CultureInfo.InvariantCulture) +
                ",missing " + before.RuntimeReferenceMissing +
                "->" + after.RuntimeReferenceMissing +
                ",path=" + (path ?? string.Empty);
        }

        private static int CountMissingRuntimeGroupers(DaSegmentData segment)
        {
            int count = 0;
            for (int index = 0; segment != null && segment.Groupers != null && index < segment.Groupers.Count; index++)
            {
                DaPropGrouperData grouper = segment.Groupers[index];
                if (grouper != null && grouper.SourceObject == null)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountMissingRuntimeSteps(DaSegmentData segment)
        {
            int count = 0;
            for (int grouperIndex = 0; segment != null && segment.Groupers != null && grouperIndex < segment.Groupers.Count; grouperIndex++)
            {
                DaPropGrouperData grouper = segment.Groupers[grouperIndex];
                for (int stepIndex = 0; grouper != null && grouper.Steps != null && stepIndex < grouper.Steps.Count; stepIndex++)
                {
                    DaLevelGenStepData step = grouper.Steps[stepIndex];
                    if (step != null && step.SourceObject == null)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static int CountDirectChildren(Transform transform)
        {
            return transform != null ? transform.childCount : 0;
        }

        private static int CountDescendantObjects(Transform transform)
        {
            if (transform == null)
            {
                return 0;
            }

            int count = 0;
            for (int index = 0; index < transform.childCount; index++)
            {
                Transform child = transform.GetChild(index);
                if (child == null)
                {
                    continue;
                }

                count++;
                count += CountDescendantObjects(child);
            }

            return count;
        }

        private static string BuildGrouperGenerationKey(int index, DaPropGrouperData grouper)
        {
            return index.ToString(CultureInfo.InvariantCulture) + "|" +
                (grouper != null ? grouper.HierarchyPath ?? string.Empty : string.Empty) + "|" +
                (grouper != null ? grouper.GrouperName ?? string.Empty : string.Empty);
        }

        private static string BuildStepGenerationKey(int index, DaLevelGenStepData step)
        {
            return index.ToString(CultureInfo.InvariantCulture) + "|" +
                (step != null ? step.HierarchyPath ?? string.Empty : string.Empty) + "|" +
                (step != null ? step.StepName ?? string.Empty : string.Empty) + "|" +
                (step != null ? step.StepType ?? string.Empty : string.Empty);
        }

        private static string FormatLimitedList(List<string> values, int limit)
        {
            if (values == null || values.Count == 0)
            {
                return "<none>";
            }

            int safeLimit = Math.Max(1, limit);
            StringBuilder builder = new StringBuilder();
            for (int index = 0; index < values.Count && index < safeLimit; index++)
            {
                if (builder.Length > 0)
                {
                    builder.Append("; ");
                }

                builder.Append(values[index]);
            }

            if (values.Count > safeLimit)
            {
                builder.Append("; ... +");
                builder.Append((values.Count - safeLimit).ToString(CultureInfo.InvariantCulture));
                builder.Append(" more");
            }

            return builder.ToString();
        }

        private static string FormatGenerationStackTrace()
        {
            try
            {
                StackTrace trace = new StackTrace(2, false);
                StackFrame[] frames = trace.GetFrames();
                if (frames == null || frames.Length == 0)
                {
                    return "<empty>";
                }

                StringBuilder builder = new StringBuilder();
                int written = 0;
                for (int index = 0; index < frames.Length && written < GenerationTraceMaxStackFrames; index++)
                {
                    MethodBase method = frames[index] != null ? frames[index].GetMethod() : null;
                    if (method == null)
                    {
                        continue;
                    }

                    Type declaringType = method.DeclaringType;
                    string typeName = declaringType != null ? declaringType.FullName ?? declaringType.Name : "<unknown>";
                    if (builder.Length > 0)
                    {
                        builder.Append(" <- ");
                    }

                    builder.Append(typeName);
                    builder.Append(".");
                    builder.Append(method.Name);
                    written++;
                }

                if (frames.Length > written)
                {
                    builder.Append(" <- ...");
                }

                return builder.ToString();
            }
            catch (Exception ex)
            {
                return "<stack unavailable: " + ex.Message + ">";
            }
        }

        private static string BuildStepKey(DaSegmentData segment, DaPropGrouperData grouper, DaLevelGenStepData step)
        {
            return (segment != null ? segment.SegmentName : string.Empty) + "/" +
                   (grouper != null ? grouper.GrouperName : string.Empty) + "/" +
                   (step != null ? step.StepName : string.Empty) + "/" +
                   (step != null ? step.StepType : string.Empty);
        }

        private sealed class DaRuntimePropertyOwner
        {
            public DaLevelGenStepData Step { get; set; }

            public DaConstraintData Constraint { get; set; }
        }

        private sealed class SegmentGenerationSnapshot
        {
            public int GrouperCount;

            public int TotalDirectChildren;

            public int TotalDescendants;

            public int MissingRuntimeGroupers;

            public int MissingRuntimeSteps;

            public Dictionary<string, GrouperGenerationSnapshot> Groupers { get; } = new Dictionary<string, GrouperGenerationSnapshot>(StringComparer.Ordinal);
        }

        private sealed class GrouperGenerationSnapshot
        {
            public string GrouperName;

            public string GrouperPath;

            public bool RuntimeReferenceMissing;

            public int StepCount;

            public int GrouperDirectChildren;

            public int GrouperDescendants;

            public int TotalDirectChildren;

            public int TotalDescendants;

            public int MissingRuntimeSteps;

            public Dictionary<string, StepGenerationSnapshot> Steps { get; } = new Dictionary<string, StepGenerationSnapshot>(StringComparer.Ordinal);
        }

        private sealed class StepGenerationSnapshot
        {
            public int StepIndex;

            public string StepName;

            public string StepType;

            public string StepPath;

            public bool RuntimeReferenceMissing;

            public int DirectChildCount;

            public int DescendantCount;
        }
    }
}


