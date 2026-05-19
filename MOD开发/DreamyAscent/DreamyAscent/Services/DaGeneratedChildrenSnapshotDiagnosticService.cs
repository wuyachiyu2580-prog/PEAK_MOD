using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using DreamyAscent.Data;
using DreamyAscent.Helpers;
using UnityEngine;

namespace DreamyAscent.Services
{
    internal static class DaGeneratedChildrenSnapshotDiagnosticService
    {
        private const int MaxDirectComponents = 80;
        private const int MaxDescendantComponentTypes = 160;
        private const int MaxRendererMaterials = 160;
        private const int MaxObjectChildSummaries = 120;
        private const int MaxObjectChildSummaryDepth = 3;
        private const int MaxRelationshipCandidatesPerSegment = 1200;
        private const int MaxComponentFieldSnapshots = 40;
        private const int MaxFieldsPerComponent = 60;
        private const int MaxEnumerableFieldValues = 24;
        private const int MaxFieldStringLength = 220;
        private const BindingFlags ExternalFieldFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags ComponentFieldFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly JsonSerializerSettings s_jsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

        public static void WriteGeneratedChildrenSnapshot(DaTerrainData data, string mapDirectory)
        {
            if (string.IsNullOrWhiteSpace(mapDirectory))
            {
                return;
            }

            try
            {
                DaGeneratedChildrenSnapshot snapshot = BuildSnapshot(data);
                string path = Path.Combine(mapDirectory, "GeneratedChildrenSnapshot.json");
                using (StreamWriter writer = new StreamWriter(path, false, Encoding.UTF8))
                using (JsonTextWriter jsonWriter = new JsonTextWriter(writer))
                {
                    jsonWriter.Formatting = Formatting.Indented;
                    JsonSerializer.Create(s_jsonSettings).Serialize(jsonWriter, snapshot);
                }

                DaLog.Info(string.Format(
                    CultureInfo.InvariantCulture,
                    "Generated children snapshot written: {0}, segments={1}, steps={2}, generatedChildren={3}, looseObjects={4}, specialObjects={5}, relationshipCandidates={6}, dirty={7}",
                    path,
                    snapshot.SegmentCount,
                    snapshot.StepCount,
                    snapshot.GeneratedChildCount,
                    snapshot.LooseObjectCount,
                    snapshot.SpecialObjectCount,
                    snapshot.RelationshipCandidateCount,
                    snapshot.PotentiallyDirty));
            }
            catch (Exception ex)
            {
                DaLog.Warn("Failed to write generated children snapshot: " + ex.Message);
            }
        }

        private static DaGeneratedChildrenSnapshot BuildSnapshot(DaTerrainData data)
        {
            DaGeneratedChildrenSnapshot snapshot = new DaGeneratedChildrenSnapshot
            {
                SchemaVersion = 3,
                GeneratedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                MapKey = data != null && data.Map != null ? data.Map.MapKey : null,
                SourceClassification = "runtime-current-map-unknown",
                Freshness = "not-verified",
                ReconstructionEligibility = "review-required"
            };
            AddExternalMapModifierInfo(snapshot);

            if (data == null || data.Map == null || data.Map.Segments == null)
            {
                snapshot.PotentiallyDirty = true;
                snapshot.Warnings.Add("No terrain export data was available.");
                return snapshot;
            }

            Dictionary<DaSegmentData, List<DaSpecialSceneObjectData>> specialBySegment = DaSpecialSceneObjectService.ScanAll(data);
            HashSet<GameObject> specialObjects = new HashSet<GameObject>();
            foreach (KeyValuePair<DaSegmentData, List<DaSpecialSceneObjectData>> pair in specialBySegment)
            {
                foreach (DaSpecialSceneObjectData item in pair.Value ?? Enumerable.Empty<DaSpecialSceneObjectData>())
                {
                    if (item != null && item.SourceObject != null)
                    {
                        specialObjects.Add(item.SourceObject);
                    }
                }
            }

            int nonOfficialModeCount = 0;
            int dreamyRuntimeObjectCount = CountDreamyAscentRuntimeObjects();
            if (dreamyRuntimeObjectCount > 0)
            {
                snapshot.PotentiallyDirty = true;
                snapshot.DreamyAscentRuntimeObjectCount = dreamyRuntimeObjectCount;
                snapshot.DirtyReasons.Add("dreamy-ascent-runtime-objects-present");
                snapshot.Warnings.Add("DreamyAscent runtime-created objects are present in the scene; use a fresh map before treating this as an official reconstruction sample.");
            }

            for (int segmentIndex = 0; segmentIndex < data.Map.Segments.Count; segmentIndex++)
            {
                DaSegmentData segment = data.Map.Segments[segmentIndex];
                if (segment == null)
                {
                    continue;
                }

                if (segment.EditMode != DaSegmentEditMode.OfficialTemplate)
                {
                    nonOfficialModeCount++;
                }

                DaGeneratedSegmentSnapshot segmentSnapshot = BuildSegmentSnapshot(segment, specialBySegment, specialObjects);
                snapshot.Segments.Add(segmentSnapshot);
                snapshot.StepCount += segmentSnapshot.StepCount;
                snapshot.GeneratedChildCount += segmentSnapshot.GeneratedChildCount;
                snapshot.LooseObjectCount += segmentSnapshot.LooseObjectCount;
                snapshot.SpecialObjectCount += segmentSnapshot.SpecialObjectCount;
                snapshot.RelationshipCandidateCount += segmentSnapshot.RelationshipCandidateCount;
            }

            snapshot.SegmentCount = snapshot.Segments.Count;
            snapshot.NonOfficialEditModeSegmentCount = nonOfficialModeCount;
            if (snapshot.StepCount > 0 && snapshot.GeneratedChildCount == 0 && snapshot.RelationshipCandidateCount == 0)
            {
                snapshot.PotentiallyDirty = true;
                snapshot.DirtyReasons.Add("no-generated-children-or-relationships-captured");
                snapshot.Warnings.Add("No generated children or relationship candidates were captured; the map may have been hidden, unloaded, or exported after preview isolation.");
            }

            if (nonOfficialModeCount > 0)
            {
                snapshot.PotentiallyDirty = true;
                snapshot.DirtyReasons.Add("non-official-edit-mode-present");
                snapshot.Warnings.Add("One or more segments are not in OfficialTemplate mode; do not mix this sample into official baseline data.");
            }

            if (!snapshot.PotentiallyDirty)
            {
                snapshot.ReconstructionEligibility = "eligible-after-manual-source-review";
            }

            return snapshot;
        }

        private static DaGeneratedSegmentSnapshot BuildSegmentSnapshot(
            DaSegmentData segment,
            Dictionary<DaSegmentData, List<DaSpecialSceneObjectData>> specialBySegment,
            HashSet<GameObject> specialObjects)
        {
            DaGeneratedSegmentSnapshot segmentSnapshot = new DaGeneratedSegmentSnapshot
            {
                SegmentName = segment.SegmentName,
                LevelSlot = segment.LevelSlot,
                SegmentPath = segment.SegmentPath,
                VariantSelectionType = segment.VariantSelectionType,
                NormalizedVariantName = segment.NormalizedVariantName,
                EditMode = segment.EditMode.ToString()
            };

            if (segment.ActiveVariantNames != null)
            {
                segmentSnapshot.ActiveVariantNames.AddRange(segment.ActiveVariantNames);
            }

            if (segment.ActiveVariantPaths != null)
            {
                segmentSnapshot.ActiveVariantPaths.AddRange(segment.ActiveVariantPaths);
            }

            if (segment.RootPaths != null)
            {
                segmentSnapshot.RootPaths.AddRange(segment.RootPaths);
            }

            HashSet<Transform> stepTransforms = new HashSet<Transform>();
            HashSet<Transform> grouperTransforms = new HashSet<Transform>();
            HashSet<Transform> generatedChildRoots = new HashSet<Transform>();

            foreach (DaPropGrouperData grouper in segment.Groupers ?? Enumerable.Empty<DaPropGrouperData>())
            {
                if (grouper == null)
                {
                    continue;
                }

                DaGeneratedGrouperSnapshot grouperSnapshot = BuildGrouperSnapshot(grouper, stepTransforms, grouperTransforms, generatedChildRoots);
                if (grouperSnapshot.StepCount > 0)
                {
                    segmentSnapshot.Groupers.Add(grouperSnapshot);
                    segmentSnapshot.StepCount += grouperSnapshot.StepCount;
                    segmentSnapshot.GeneratedChildCount += grouperSnapshot.GeneratedChildCount;
                }
            }

            AddLooseObjects(segmentSnapshot, segment, stepTransforms, grouperTransforms, generatedChildRoots, specialObjects);
            AddSpecialObjects(segmentSnapshot, segment, specialBySegment);
            AddRelationshipCandidates(segmentSnapshot, segment, stepTransforms, grouperTransforms, generatedChildRoots, specialObjects);
            return segmentSnapshot;
        }

        private static void AddExternalMapModifierInfo(DaGeneratedChildrenSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            DaExternalMapModifierSnapshot terrainRandomiser = BuildTerrainRandomiserInfo();
            if (terrainRandomiser != null)
            {
                snapshot.ExternalMapModifiers.Add(terrainRandomiser);
                if (terrainRandomiser.Enabled)
                {
                    snapshot.SourceClassification = "terrain-randomiser-forced-map";
                    snapshot.Warnings.Add("TerrainRandomiser appears enabled; keep this sample separate from official natural map samples.");
                }
            }

            snapshot.ExternalMapModifierCount = snapshot.ExternalMapModifiers.Count;
            if (snapshot.ExternalMapModifierCount == 0)
            {
                snapshot.SourceClassification = "official-natural-map-or-unknown";
            }
        }

        private static DaExternalMapModifierSnapshot BuildTerrainRandomiserInfo()
        {
            Type pluginType = FindTypeByFullName("TerrainRandomiser.Plugin");
            if (pluginType == null)
            {
                return null;
            }

            object instance = GetFieldValue(pluginType, null, "Instance");
            object roomMapSettings = instance != null ? GetFieldValue(instance.GetType(), instance, "roomMapSettings") : null;
            object mapSettings = instance != null ? GetFieldValue(instance.GetType(), instance, "mapSettings") : null;
            object settings = roomMapSettings ?? mapSettings;

            DaExternalMapModifierSnapshot info = new DaExternalMapModifierSnapshot
            {
                Name = "TerrainRandomiser",
                Detected = true,
                Enabled = GetBoolField(settings, "enableRandomiser"),
                Source = roomMapSettings != null ? "roomMapSettings" : (mapSettings != null ? "mapSettings" : "plugin-loaded-no-settings"),
                Seed = GetNullableIntField(settings, "seed")
            };

            IEnumerable biomes = GetFieldValue(settings != null ? settings.GetType() : null, settings, "biomes") as IEnumerable;
            if (biomes != null)
            {
                foreach (object biome in biomes)
                {
                    if (biome == null)
                    {
                        continue;
                    }

                    object selectedBiome = GetFieldValue(biome.GetType(), biome, "selectedBiome");
                    info.Biomes.Add(new DaExternalBiomeSelectionSnapshot
                    {
                        SectionName = GetStringField(biome, "sectionName"),
                        SelectedBiomeName = GetStringField(selectedBiome, "biomeName"),
                        SelectedBiomeType = GetFieldString(selectedBiome, "biomeType"),
                        SelectedVariant = GetStringField(biome, "selectedVariant"),
                        VariantSelectionType = GetFieldString(selectedBiome, "variantSelectionType"),
                        OverrideEnabled = GetBoolField(biome, "overrideEnabled"),
                        RandomBiome = GetBoolField(biome, "randomBiome"),
                        RandomVariant = GetBoolField(biome, "randomVariant")
                    });
                }
            }

            return info;
        }

        private static DaGeneratedGrouperSnapshot BuildGrouperSnapshot(
            DaPropGrouperData grouper,
            HashSet<Transform> stepTransforms,
            HashSet<Transform> grouperTransforms,
            HashSet<Transform> generatedChildRoots)
        {
            DaGeneratedGrouperSnapshot grouperSnapshot = new DaGeneratedGrouperSnapshot
            {
                GrouperName = grouper.GrouperName,
                GrouperPath = grouper.HierarchyPath,
                RuntimeType = grouper.SourceObject != null ? grouper.SourceObject.GetType().FullName : null
            };

            if (grouper.SourceObject != null)
            {
                grouperTransforms.Add(grouper.SourceObject.transform);
            }

            foreach (DaLevelGenStepData step in grouper.Steps ?? Enumerable.Empty<DaLevelGenStepData>())
            {
                if (step == null)
                {
                    continue;
                }

                DaGeneratedStepSnapshot stepSnapshot = BuildStepSnapshot(step, grouper, stepTransforms, generatedChildRoots);
                grouperSnapshot.Steps.Add(stepSnapshot);
                grouperSnapshot.StepCount++;
                grouperSnapshot.GeneratedChildCount += stepSnapshot.DirectChildCount;
            }

            return grouperSnapshot;
        }

        private static DaGeneratedStepSnapshot BuildStepSnapshot(
            DaLevelGenStepData step,
            DaPropGrouperData grouper,
            HashSet<Transform> stepTransforms,
            HashSet<Transform> generatedChildRoots)
        {
            DaGeneratedStepSnapshot stepSnapshot = new DaGeneratedStepSnapshot
            {
                StepName = step.StepName,
                StepType = step.StepType,
                StepPath = step.HierarchyPath,
                GrouperName = grouper != null ? grouper.GrouperName : null,
                GrouperPath = grouper != null ? grouper.HierarchyPath : null,
                RuntimeType = step.SourceObject != null ? step.SourceObject.GetType().FullName : null
            };

            if (step.SourceObject == null)
            {
                stepSnapshot.RuntimeReferenceMissing = true;
                return stepSnapshot;
            }

            Transform stepTransform = step.SourceObject.transform;
            stepTransforms.Add(stepTransform);
            stepSnapshot.Transform = DescribeTransform(stepTransform);
            stepSnapshot.Bounds = BuildBoundsInfo(stepTransform.gameObject);
            FillStepTypeLists(stepSnapshot, step);
            FillComponentFieldSnapshots(stepSnapshot.InterestingComponentFields, stepTransform.gameObject, false);

            for (int childIndex = 0; childIndex < stepTransform.childCount; childIndex++)
            {
                Transform child = stepTransform.GetChild(childIndex);
                if (child == null)
                {
                    continue;
                }

                generatedChildRoots.Add(child);
                DaGeneratedObjectSnapshot childSnapshot = DescribeGeneratedObject(child.gameObject, childIndex, stepTransform);
                stepSnapshot.Children.Add(childSnapshot);
            }

            stepSnapshot.DirectChildCount = stepSnapshot.Children.Count;
            stepSnapshot.DescendantObjectCount = CountDescendants(stepTransform);
            stepSnapshot.RendererCount = stepTransform.GetComponentsInChildren<Renderer>(true).Length;
            stepSnapshot.ColliderCount = stepTransform.GetComponentsInChildren<Collider>(true).Length;
            return stepSnapshot;
        }

        private static void FillStepTypeLists(DaGeneratedStepSnapshot stepSnapshot, DaLevelGenStepData step)
        {
            foreach (DaConstraintData modifier in step.Modifiers ?? Enumerable.Empty<DaConstraintData>())
            {
                AddUnique(stepSnapshot.ModifierTypes, modifier != null ? modifier.Type : null);
            }

            foreach (DaConstraintData constraint in step.Constraints ?? Enumerable.Empty<DaConstraintData>())
            {
                AddUnique(stepSnapshot.ConstraintTypes, constraint != null ? constraint.Type : null);
            }

            foreach (DaConstraintData postConstraint in step.PostConstraints ?? Enumerable.Empty<DaConstraintData>())
            {
                AddUnique(stepSnapshot.PostConstraintTypes, postConstraint != null ? postConstraint.Type : null);
            }
        }

        private static void AddLooseObjects(
            DaGeneratedSegmentSnapshot segmentSnapshot,
            DaSegmentData segment,
            HashSet<Transform> stepTransforms,
            HashSet<Transform> grouperTransforms,
            HashSet<Transform> generatedChildRoots,
            HashSet<GameObject> specialObjects)
        {
            HashSet<Transform> looseRoots = new HashSet<Transform>();
            foreach (Transform root in segment.SourceRoots ?? Enumerable.Empty<Transform>())
            {
                Transform[] transforms = root != null ? root.GetComponentsInChildren<Transform>(true) : null;
                for (int index = 0; transforms != null && index < transforms.Length; index++)
                {
                    Transform transform = transforms[index];
                    if (transform == null ||
                        transform == root ||
                        IsSelfOrDescendantOfAny(transform, generatedChildRoots) ||
                        IsSelfOrDescendantOfAny(transform, stepTransforms) ||
                        IsSelfOrDescendantOfAny(transform, grouperTransforms) ||
                        HasAncestorInSet(transform, looseRoots))
                    {
                        continue;
                    }

                    if (!HasLooseSnapshotSignal(transform.gameObject, specialObjects))
                    {
                        continue;
                    }

                    looseRoots.Add(transform);
                    segmentSnapshot.LooseObjects.Add(DescribeLooseObject(transform.gameObject, segmentSnapshot.LooseObjects.Count, root));
                }
            }

            segmentSnapshot.LooseObjectCount = segmentSnapshot.LooseObjects.Count;
        }

        private static void AddSpecialObjects(
            DaGeneratedSegmentSnapshot segmentSnapshot,
            DaSegmentData segment,
            Dictionary<DaSegmentData, List<DaSpecialSceneObjectData>> specialBySegment)
        {
            if (!specialBySegment.TryGetValue(segment, out List<DaSpecialSceneObjectData> specialItems) || specialItems == null)
            {
                return;
            }

            foreach (DaSpecialSceneObjectData item in specialItems)
            {
                if (item == null)
                {
                    continue;
                }

                DaSpecialObjectSnapshot special = new DaSpecialObjectSnapshot
                {
                    Id = item.Id,
                    DisplayName = item.DisplayName,
                    Category = item.Category,
                    Reason = item.Reason,
                    Path = item.Path,
                    ParentPath = item.ParentPath,
                    RootPath = item.RootPath,
                    ActiveSelf = item.ActiveSelf,
                    ActiveInHierarchy = item.ActiveInHierarchy,
                    Layer = item.Layer,
                    Tag = item.Tag,
                    LocalPosition = ToVector(item.LocalPosition),
                    WorldPosition = ToVector(item.WorldPosition),
                    RendererCount = item.RendererCount,
                    ColliderCount = item.ColliderCount,
                    CanToggleActive = item.CanToggleActive,
                    CanDelete = item.CanDelete,
                    IsProtected = item.IsProtected,
                    ProtectionReason = item.ProtectionReason
                };

                special.Components.AddRange(item.Components);
                special.Materials.AddRange(item.Materials);
                segmentSnapshot.SpecialObjects.Add(special);
            }

            segmentSnapshot.SpecialObjectCount = segmentSnapshot.SpecialObjects.Count;
        }

        private static void AddRelationshipCandidates(
            DaGeneratedSegmentSnapshot segmentSnapshot,
            DaSegmentData segment,
            HashSet<Transform> stepTransforms,
            HashSet<Transform> grouperTransforms,
            HashSet<Transform> generatedChildRoots,
            HashSet<GameObject> specialObjects)
        {
            if (segmentSnapshot == null || segment == null)
            {
                return;
            }

            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (DaPropGrouperData grouper in segment.Groupers ?? Enumerable.Empty<DaPropGrouperData>())
            {
                foreach (DaLevelGenStepData step in grouper != null ? grouper.Steps ?? Enumerable.Empty<DaLevelGenStepData>() : Enumerable.Empty<DaLevelGenStepData>())
                {
                    if (step == null || step.SourceObject == null)
                    {
                        continue;
                    }

                    Transform stepTransform = step.SourceObject.transform;
                    AddRelationshipCandidate(
                        segmentSnapshot,
                        seen,
                        "generation-step",
                        "LevelGenStep can generate or modify child objects.",
                        stepTransform,
                        stepTransform.parent,
                        step.HierarchyPath,
                        grouper != null ? grouper.HierarchyPath : null,
                        stepTransform);

                    if (segmentSnapshot.RelationshipCandidates.Count >= MaxRelationshipCandidatesPerSegment)
                    {
                        segmentSnapshot.RelationshipCandidateCount = segmentSnapshot.RelationshipCandidates.Count;
                        return;
                    }
                }
            }

            foreach (Transform root in segment.SourceRoots ?? Enumerable.Empty<Transform>())
            {
                Transform[] transforms = root != null ? root.GetComponentsInChildren<Transform>(true) : null;
                for (int index = 0; transforms != null && index < transforms.Length; index++)
                {
                    Transform transform = transforms[index];
                    if (transform == null || transform == root)
                    {
                        continue;
                    }

                    string reason;
                    if (!TryGetRelationshipReason(transform.gameObject, specialObjects, out reason))
                    {
                        continue;
                    }

                    Transform nearestStep = FindNearestAncestorInSet(transform, stepTransforms);
                    Transform nearestGrouper = FindNearestAncestorInSet(transform, grouperTransforms);
                    Transform nearestGeneratedRoot = FindNearestAncestorInSet(transform, generatedChildRoots);
                    AddRelationshipCandidate(
                        segmentSnapshot,
                        seen,
                        "object-candidate",
                        reason,
                        transform,
                        transform.parent,
                        nearestStep != null ? GetHierarchyPath(nearestStep) : null,
                        nearestGrouper != null ? GetHierarchyPath(nearestGrouper) : null,
                        nearestGeneratedRoot);

                    if (segmentSnapshot.RelationshipCandidates.Count >= MaxRelationshipCandidatesPerSegment)
                    {
                        segmentSnapshot.RelationshipCandidateCount = segmentSnapshot.RelationshipCandidates.Count;
                        return;
                    }
                }
            }

            segmentSnapshot.RelationshipCandidateCount = segmentSnapshot.RelationshipCandidates.Count;
        }

        private static void AddRelationshipCandidate(
            DaGeneratedSegmentSnapshot segmentSnapshot,
            HashSet<string> seen,
            string sourceKind,
            string reason,
            Transform subject,
            Transform parent,
            string sourceStepPath,
            string sourceGrouperPath,
            Transform generatedRoot)
        {
            if (segmentSnapshot == null || subject == null)
            {
                return;
            }

            string path = GetHierarchyPath(subject);
            string key = sourceKind + "|" + path;
            if (seen != null && !seen.Add(key))
            {
                return;
            }

            DaRelationshipCandidateSnapshot candidate = new DaRelationshipCandidateSnapshot
            {
                SourceKind = sourceKind,
                Reason = reason,
                Name = subject.name,
                Path = path,
                ParentPath = parent != null ? GetHierarchyPath(parent) : null,
                SourceStepPath = sourceStepPath,
                SourceGrouperPath = sourceGrouperPath,
                GeneratedRootPath = generatedRoot != null ? GetHierarchyPath(generatedRoot) : null,
                PathHash = StableHash(path),
                StableSignature = BuildObjectSignature(subject.gameObject, NormalizeCloneName(subject.name)),
                Transform = DescribeTransform(subject),
                Bounds = BuildBoundsInfo(subject.gameObject)
            };

            Component[] directComponents = subject.gameObject.GetComponents<Component>();
            for (int index = 0; directComponents != null && index < directComponents.Length && index < MaxDirectComponents; index++)
            {
                Component component = directComponents[index];
                candidate.Components.Add(component != null ? component.GetType().FullName : "<missing>");
            }

            FillObjectRiskInfo(candidate, subject.gameObject);
            FillComponentFieldSnapshots(candidate.InterestingComponentFields, subject.gameObject, true);
            FillChildSummaries(candidate.Children, subject, 1);
            candidate.ChildCount = subject.childCount;
            candidate.DescendantObjectCount = CountDescendants(subject);
            segmentSnapshot.RelationshipCandidates.Add(candidate);
        }

        private static DaGeneratedObjectSnapshot DescribeGeneratedObject(GameObject gameObject, int childIndex, Transform stepTransform)
        {
            DaGeneratedObjectSnapshot snapshot = DescribeObjectBase(gameObject);
            snapshot.ChildIndex = childIndex;
            snapshot.ParentPath = gameObject != null && gameObject.transform.parent != null ? GetHierarchyPath(gameObject.transform.parent) : null;
            snapshot.SourceKind = "step-direct-child";
            snapshot.SourceStepPath = stepTransform != null ? GetHierarchyPath(stepTransform) : null;
            FillObjectTreeInfo(snapshot, gameObject);
            return snapshot;
        }

        private static DaGeneratedObjectSnapshot DescribeLooseObject(GameObject gameObject, int index, Transform root)
        {
            DaGeneratedObjectSnapshot snapshot = DescribeObjectBase(gameObject);
            snapshot.ChildIndex = index;
            snapshot.ParentPath = gameObject != null && gameObject.transform.parent != null ? GetHierarchyPath(gameObject.transform.parent) : null;
            snapshot.RootPath = root != null ? GetHierarchyPath(root) : null;
            snapshot.SourceKind = "loose-segment-object";
            FillObjectTreeInfo(snapshot, gameObject);
            return snapshot;
        }

        private static DaGeneratedObjectSnapshot DescribeObjectBase(GameObject gameObject)
        {
            DaGeneratedObjectSnapshot snapshot = new DaGeneratedObjectSnapshot();
            if (gameObject == null)
            {
                return snapshot;
            }

            Transform transform = gameObject.transform;
            string prefabNameGuess = NormalizeCloneName(gameObject.name);

            snapshot.Name = gameObject.name;
            snapshot.PrefabNameGuess = prefabNameGuess;
            snapshot.Path = GetHierarchyPath(transform);
            snapshot.PathHash = StableHash(snapshot.Path);
            snapshot.StableSignature = BuildObjectSignature(gameObject, prefabNameGuess);
            snapshot.ActiveSelf = gameObject.activeSelf;
            snapshot.ActiveInHierarchy = gameObject.activeInHierarchy;
            snapshot.Layer = gameObject.layer;
            snapshot.Tag = SafeTag(gameObject);
            snapshot.Transform = DescribeTransform(transform);
            snapshot.Bounds = BuildBoundsInfo(gameObject);
            snapshot.Scene = gameObject.scene.IsValid() ? gameObject.scene.name : string.Empty;
            return snapshot;
        }

        private static void FillObjectTreeInfo(DaGeneratedObjectSnapshot snapshot, GameObject gameObject)
        {
            if (snapshot == null || gameObject == null)
            {
                return;
            }

            Component[] directComponents = gameObject.GetComponents<Component>();
            for (int index = 0; directComponents != null && index < directComponents.Length && index < MaxDirectComponents; index++)
            {
                Component component = directComponents[index];
                snapshot.Components.Add(component != null ? component.GetType().FullName : "<missing>");
            }

            HashSet<string> descendantComponentTypes = new HashSet<string>(StringComparer.Ordinal);
            Component[] allComponents = gameObject.GetComponentsInChildren<Component>(true);
            for (int index = 0; allComponents != null && index < allComponents.Length; index++)
            {
                Component component = allComponents[index];
                if (component != null && descendantComponentTypes.Add(component.GetType().FullName) && snapshot.DescendantComponentTypes.Count < MaxDescendantComponentTypes)
                {
                    snapshot.DescendantComponentTypes.Add(component.GetType().FullName);
                }
            }

            HashSet<string> materialNames = new HashSet<string>(StringComparer.Ordinal);
            Renderer[] renderers = gameObject.GetComponentsInChildren<Renderer>(true);
            for (int rendererIndex = 0; renderers != null && rendererIndex < renderers.Length; rendererIndex++)
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
                    if (material != null && materialNames.Add(material.name) && snapshot.RendererMaterials.Count < MaxRendererMaterials)
                    {
                        snapshot.RendererMaterials.Add(material.name);
                    }
                }
            }

            snapshot.ChildCount = gameObject.transform.childCount;
            snapshot.DescendantObjectCount = CountDescendants(gameObject.transform);
            snapshot.RendererCount = renderers != null ? renderers.Length : 0;
            snapshot.ColliderCount = gameObject.GetComponentsInChildren<Collider>(true).Length;
            snapshot.LevelGenStepCount = gameObject.GetComponentsInChildren<LevelGenStep>(true).Length;
            snapshot.PropGrouperCount = gameObject.GetComponentsInChildren<PropGrouper>(true).Length;
            snapshot.SingleItemSpawnerCount = CountComponentsByTypeName(gameObject, "SingleItemSpawner");
            snapshot.PhotonViewCount = CountComponentsByTypeName(gameObject, "PhotonView");
            FillObjectRiskInfo(snapshot, gameObject);
            FillComponentFieldSnapshots(snapshot.InterestingComponentFields, gameObject, true);
            FillChildSummaries(snapshot.ChildSummaries, gameObject.transform, 1);
        }

        private static DaTransformSnapshot DescribeTransform(Transform transform)
        {
            if (transform == null)
            {
                return null;
            }

            return new DaTransformSnapshot
            {
                LocalPosition = ToVector(transform.localPosition),
                WorldPosition = ToVector(transform.position),
                LocalEulerAngles = ToVector(transform.localEulerAngles),
                WorldEulerAngles = ToVector(transform.eulerAngles),
                LocalRotation = ToQuaternion(transform.localRotation),
                WorldRotation = ToQuaternion(transform.rotation),
                LocalScale = ToVector(transform.localScale),
                LossyScale = ToVector(transform.lossyScale)
            };
        }

        private static DaBoundsSnapshot BuildBoundsInfo(GameObject gameObject)
        {
            DaBoundsSnapshot snapshot = new DaBoundsSnapshot();
            if (gameObject == null)
            {
                return snapshot;
            }

            bool hasBounds = false;
            Bounds combined = new Bounds(gameObject.transform.position, Vector3.zero);

            Renderer[] renderers = gameObject.GetComponentsInChildren<Renderer>(true);
            for (int index = 0; renderers != null && index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null)
                {
                    continue;
                }

                AddBounds(ref combined, ref hasBounds, renderer.bounds);
            }

            Collider[] colliders = gameObject.GetComponentsInChildren<Collider>(true);
            for (int index = 0; colliders != null && index < colliders.Length; index++)
            {
                Collider collider = colliders[index];
                if (collider == null)
                {
                    continue;
                }

                AddBounds(ref combined, ref hasBounds, collider.bounds);
            }

            snapshot.HasBounds = hasBounds;
            if (hasBounds)
            {
                snapshot.Center = ToVector(combined.center);
                snapshot.Size = ToVector(combined.size);
                snapshot.Min = ToVector(combined.min);
                snapshot.Max = ToVector(combined.max);
            }

            return snapshot;
        }

        private static void AddBounds(ref Bounds combined, ref bool hasBounds, Bounds bounds)
        {
            if (!hasBounds)
            {
                combined = bounds;
                hasBounds = true;
            }
            else
            {
                combined.Encapsulate(bounds);
            }
        }

        private static bool HasLooseSnapshotSignal(GameObject gameObject, HashSet<GameObject> specialObjects)
        {
            if (gameObject == null)
            {
                return false;
            }

            return gameObject.GetComponentsInChildren<Renderer>(true).Length > 0 ||
                   gameObject.GetComponentsInChildren<Collider>(true).Length > 0 ||
                   gameObject.GetComponent<LevelGenStep>() != null ||
                   gameObject.GetComponent<PropGrouper>() != null ||
                   specialObjects.Contains(gameObject);
        }

        private static bool TryGetRelationshipReason(GameObject gameObject, HashSet<GameObject> specialObjects, out string reason)
        {
            reason = string.Empty;
            if (gameObject == null)
            {
                return false;
            }

            string name = gameObject.name ?? string.Empty;
            string path = GetHierarchyPath(gameObject.transform);
            if (ContainsText(name, "Coconut") || ContainsText(path, "Coconut"))
            {
                reason = "coconut-name-or-path";
                return true;
            }

            if (ContainsText(name, "Palm") || ContainsText(path, "Palm"))
            {
                reason = "palm-tree-name-or-path";
                return true;
            }

            if (ContainsText(name, "Tree") && (ContainsText(path, "Beach") || ContainsText(path, "Shore") || ContainsText(path, "Jungle")))
            {
                reason = "tree-name-in-map-path";
                return true;
            }

            if (gameObject.GetComponentsInChildren<LevelGenStep>(true).Length > 0 && gameObject.GetComponent<LevelGenStep>() == null)
            {
                reason = "nested-level-gen-step";
                return true;
            }

            if (gameObject.GetComponentsInChildren<PropGrouper>(true).Length > 0 && gameObject.GetComponent<PropGrouper>() == null)
            {
                reason = "nested-prop-grouper";
                return true;
            }

            if (CountComponentsByTypeName(gameObject, "SingleItemSpawner") > 0)
            {
                reason = "single-item-spawner";
                return true;
            }

            if (CountComponentsByTypeName(gameObject, "BeachSpawner") > 0)
            {
                reason = "beach-spawner";
                return true;
            }

            if (CountComponentsByTypeName(gameObject, "PSM_ChildSpawners") > 0)
            {
                reason = "child-spawner-mod";
                return true;
            }

            if (CountComponentsByTypeName(gameObject, "PSM_SingleItemSpawner") > 0)
            {
                reason = "single-item-spawner-mod";
                return true;
            }

            if (CountComponentsByTypeName(gameObject, "Spawner") > 0 ||
                CountComponentsByTypeName(gameObject, "BerryBush") > 0 ||
                CountComponentsByTypeName(gameObject, "BerryVine") > 0 ||
                CountComponentsByTypeName(gameObject, "GroundPlaceSpawner") > 0 ||
                CountComponentsByTypeName(gameObject, "Luggage") > 0)
            {
                reason = "runtime-spawner-component";
                return true;
            }

            if (specialObjects != null && specialObjects.Contains(gameObject))
            {
                reason = "special-scene-object";
                return true;
            }

            return false;
        }

        private static void FillObjectRiskInfo(DaObjectRiskInfoSnapshot target, GameObject gameObject)
        {
            if (target == null || gameObject == null)
            {
                return;
            }

            target.HasNestedLevelGenStep = gameObject.GetComponentsInChildren<LevelGenStep>(true).Length > (gameObject.GetComponent<LevelGenStep>() != null ? 1 : 0);
            target.HasNestedPropGrouper = gameObject.GetComponentsInChildren<PropGrouper>(true).Length > (gameObject.GetComponent<PropGrouper>() != null ? 1 : 0);
            target.HasSingleItemSpawner = CountComponentsByTypeName(gameObject, "SingleItemSpawner") > 0;
            target.HasPhotonView = CountComponentsByTypeName(gameObject, "PhotonView") > 0;
            target.HasKnownSpawner = HasComponentWithTypeName(gameObject, "Spawner") ||
                                      HasComponentWithTypeName(gameObject, "BerryBush") ||
                                      HasComponentWithTypeName(gameObject, "BerryVine") ||
                                      HasComponentWithTypeName(gameObject, "GroundPlaceSpawner") ||
                                      HasComponentWithTypeName(gameObject, "Luggage") ||
                                      HasComponentWithTypeName(gameObject, "BeachSpawner");
            target.HasChildSpawnerMod = CountComponentsByTypeName(gameObject, "PSM_ChildSpawners") > 0;
            target.HasSingleItemSpawnerMod = CountComponentsByTypeName(gameObject, "PSM_SingleItemSpawner") > 0;
            target.NeedsParentChildHandling = target.HasNestedLevelGenStep ||
                                             target.HasNestedPropGrouper ||
                                             target.HasChildSpawnerMod ||
                                             ContainsText(gameObject.name, "Coconut") ||
                                             ContainsText(gameObject.name, "Palm");
            target.NeedsNetworkHandling = target.HasPhotonView || target.HasSingleItemSpawner || target.HasSingleItemSpawnerMod || HasComponentWithTypeName(gameObject, "Luggage");
            target.IsHighRiskDirectPlacement = target.NeedsParentChildHandling || target.NeedsNetworkHandling || target.HasKnownSpawner;
        }

        private static bool HasComponentWithTypeName(GameObject gameObject, string typeName)
        {
            Component[] components = gameObject != null ? gameObject.GetComponentsInChildren<Component>(true) : null;
            for (int index = 0; components != null && index < components.Length; index++)
            {
                Component component = components[index];
                if (component == null)
                {
                    continue;
                }

                Type type = component.GetType();
                while (type != null)
                {
                    if (string.Equals(type.Name, typeName, StringComparison.Ordinal))
                    {
                        return true;
                    }

                    type = type.BaseType;
                }
            }

            return false;
        }

        private static Transform FindNearestAncestorInSet(Transform transform, HashSet<Transform> candidates)
        {
            Transform current = transform;
            while (current != null)
            {
                if (candidates != null && candidates.Contains(current))
                {
                    return current;
                }

                current = current.parent;
            }

            return null;
        }

        private static bool IsSelfOrDescendantOfAny(Transform transform, HashSet<Transform> roots)
        {
            if (transform == null || roots == null || roots.Count == 0)
            {
                return false;
            }

            Transform current = transform;
            while (current != null)
            {
                if (roots.Contains(current))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static bool HasAncestorInSet(Transform transform, HashSet<Transform> candidates)
        {
            Transform current = transform != null ? transform.parent : null;
            while (current != null)
            {
                if (candidates.Contains(current))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static int CountDescendants(Transform transform)
        {
            if (transform == null)
            {
                return 0;
            }

            int count = 0;
            Transform[] descendants = transform.GetComponentsInChildren<Transform>(true);
            for (int index = 0; descendants != null && index < descendants.Length; index++)
            {
                if (descendants[index] != transform)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountComponentsByTypeName(GameObject root, string typeName)
        {
            int count = 0;
            if (root == null || string.IsNullOrWhiteSpace(typeName))
            {
                return count;
            }

            Component[] components = root.GetComponentsInChildren<Component>(true);
            for (int index = 0; components != null && index < components.Length; index++)
            {
                Component component = components[index];
                if (component != null && string.Equals(component.GetType().Name, typeName, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        private static void FillChildSummaries(List<DaChildObjectSummarySnapshot> target, Transform root, int depth)
        {
            if (target == null || root == null || depth > MaxObjectChildSummaryDepth)
            {
                return;
            }

            for (int childIndex = 0; childIndex < root.childCount && target.Count < MaxObjectChildSummaries; childIndex++)
            {
                Transform child = root.GetChild(childIndex);
                if (child == null)
                {
                    continue;
                }

                DaChildObjectSummarySnapshot summary = new DaChildObjectSummarySnapshot
                {
                    Depth = depth,
                    ChildIndex = childIndex,
                    Name = child.name,
                    Path = GetHierarchyPath(child),
                    PathHash = StableHash(GetHierarchyPath(child)),
                    ActiveSelf = child.gameObject.activeSelf,
                    ActiveInHierarchy = child.gameObject.activeInHierarchy,
                    RendererCount = child.GetComponentsInChildren<Renderer>(true).Length,
                    ColliderCount = child.GetComponentsInChildren<Collider>(true).Length,
                    LevelGenStepCount = child.GetComponentsInChildren<LevelGenStep>(true).Length,
                    PropGrouperCount = child.GetComponentsInChildren<PropGrouper>(true).Length,
                    SingleItemSpawnerCount = CountComponentsByTypeName(child.gameObject, "SingleItemSpawner"),
                    PhotonViewCount = CountComponentsByTypeName(child.gameObject, "PhotonView"),
                    Transform = DescribeTransform(child)
                };

                Component[] components = child.GetComponents<Component>();
                for (int index = 0; components != null && index < components.Length && index < MaxDirectComponents; index++)
                {
                    Component component = components[index];
                    summary.Components.Add(component != null ? component.GetType().FullName : "<missing>");
                }

                target.Add(summary);
                FillChildSummaries(target, child, depth + 1);
            }
        }

        private static void FillComponentFieldSnapshots(List<DaComponentFieldSnapshot> target, GameObject gameObject, bool includeChildren)
        {
            if (target == null || gameObject == null)
            {
                return;
            }

            Component[] components = includeChildren ? gameObject.GetComponentsInChildren<Component>(true) : gameObject.GetComponents<Component>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; components != null && index < components.Length && target.Count < MaxComponentFieldSnapshots; index++)
            {
                Component component = components[index];
                if (component == null)
                {
                    continue;
                }

                Type type = component.GetType();
                if (!IsInterestingComponentType(type))
                {
                    continue;
                }

                string path = GetHierarchyPath(component.transform);
                string key = type.FullName + "|" + path;
                if (!seen.Add(key))
                {
                    continue;
                }

                DaComponentFieldSnapshot snapshot = new DaComponentFieldSnapshot
                {
                    ComponentType = type.FullName,
                    ComponentName = type.Name,
                    ObjectPath = path
                };

                FieldInfo[] fields = type.GetFields(ComponentFieldFlags);
                for (int fieldIndex = 0; fields != null && fieldIndex < fields.Length && snapshot.Fields.Count < MaxFieldsPerComponent; fieldIndex++)
                {
                    FieldInfo field = fields[fieldIndex];
                    if (field == null || field.IsStatic && !field.IsLiteral)
                    {
                        continue;
                    }

                    object value;
                    try
                    {
                        value = field.GetValue(component);
                    }
                    catch
                    {
                        continue;
                    }

                    DaFieldValueSnapshot fieldSnapshot = DescribeFieldValue(field.Name, field.FieldType, value);
                    if (fieldSnapshot != null)
                    {
                        snapshot.Fields.Add(fieldSnapshot);
                    }
                }

                target.Add(snapshot);
            }
        }

        private static bool IsInterestingComponentType(Type type)
        {
            if (type == null)
            {
                return false;
            }

            for (Type current = type; current != null; current = current.BaseType)
            {
                string name = current.Name;
                if (string.Equals(name, "PropSpawner", StringComparison.Ordinal) ||
                    string.Equals(name, "LevelGenStep", StringComparison.Ordinal) ||
                    string.Equals(name, "BeachSpawner", StringComparison.Ordinal) ||
                    string.Equals(name, "SingleItemSpawner", StringComparison.Ordinal) ||
                    string.Equals(name, "PSM_ChildSpawners", StringComparison.Ordinal) ||
                    string.Equals(name, "PSM_SingleItemSpawner", StringComparison.Ordinal) ||
                    string.Equals(name, "Spawner", StringComparison.Ordinal) ||
                    string.Equals(name, "BerryBush", StringComparison.Ordinal) ||
                    string.Equals(name, "BerryVine", StringComparison.Ordinal) ||
                    string.Equals(name, "GroundPlaceSpawner", StringComparison.Ordinal) ||
                    string.Equals(name, "Luggage", StringComparison.Ordinal) ||
                    string.Equals(name, "Capybara", StringComparison.Ordinal) ||
                    string.Equals(name, "BreakableBridge", StringComparison.Ordinal) ||
                    string.Equals(name, "SpawnConnectingBridge", StringComparison.Ordinal) ||
                    string.Equals(name, "Campfire", StringComparison.Ordinal) ||
                    string.Equals(name, "RespawnChest", StringComparison.Ordinal) ||
                    string.Equals(name, "MovingLava", StringComparison.Ordinal) ||
                    string.Equals(name, "LavaRising", StringComparison.Ordinal) ||
                    string.Equals(name, "LavaTides", StringComparison.Ordinal) ||
                    string.Equals(name, "TempleConfig", StringComparison.Ordinal) ||
                    string.Equals(name, "TempleEntranceRope", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static DaFieldValueSnapshot DescribeFieldValue(string name, Type fieldType, object value)
        {
            DaFieldValueSnapshot snapshot = new DaFieldValueSnapshot
            {
                Name = name,
                Type = fieldType != null ? fieldType.FullName : null
            };

            if (value == null)
            {
                snapshot.ValueKind = "null";
                return snapshot;
            }

            if (value is string stringValue)
            {
                snapshot.ValueKind = "string";
                snapshot.Value = TruncateString(stringValue);
                return snapshot;
            }

            if (value is bool boolValue)
            {
                snapshot.ValueKind = "bool";
                snapshot.Value = boolValue.ToString(CultureInfo.InvariantCulture);
                return snapshot;
            }

            if (value is int || value is float || value is double || value is long || value is short || value is byte)
            {
                snapshot.ValueKind = "number";
                snapshot.Value = Convert.ToString(value, CultureInfo.InvariantCulture);
                return snapshot;
            }

            if (value is Vector2 vector2)
            {
                snapshot.ValueKind = "vector2";
                snapshot.Value = string.Format(CultureInfo.InvariantCulture, "{0:0.#####},{1:0.#####}", vector2.x, vector2.y);
                return snapshot;
            }

            if (value is Vector2Int vector2Int)
            {
                snapshot.ValueKind = "vector2Int";
                snapshot.Value = string.Format(CultureInfo.InvariantCulture, "{0},{1}", vector2Int.x, vector2Int.y);
                return snapshot;
            }

            if (value is Vector3 vector3)
            {
                snapshot.ValueKind = "vector3";
                snapshot.Value = string.Format(CultureInfo.InvariantCulture, "{0:0.#####},{1:0.#####},{2:0.#####}", vector3.x, vector3.y, vector3.z);
                return snapshot;
            }

            if (value is LayerMask layerMask)
            {
                snapshot.ValueKind = "layerMask";
                snapshot.Value = layerMask.value.ToString(CultureInfo.InvariantCulture);
                return snapshot;
            }

            if (value is GameObject gameObject)
            {
                snapshot.ValueKind = "gameObject";
                snapshot.Value = gameObject.name;
                snapshot.ObjectPath = gameObject.transform != null ? GetHierarchyPath(gameObject.transform) : null;
                snapshot.ObjectInstanceId = gameObject.GetInstanceID();
                return snapshot;
            }

            if (value is Transform transform)
            {
                snapshot.ValueKind = "transform";
                snapshot.Value = transform.name;
                snapshot.ObjectPath = GetHierarchyPath(transform);
                snapshot.ObjectInstanceId = transform.gameObject != null ? transform.gameObject.GetInstanceID() : 0;
                return snapshot;
            }

            if (value is Component component)
            {
                snapshot.ValueKind = "component";
                snapshot.Value = component.GetType().FullName;
                snapshot.ObjectPath = component.transform != null ? GetHierarchyPath(component.transform) : null;
                snapshot.ObjectInstanceId = component.gameObject != null ? component.gameObject.GetInstanceID() : 0;
                return snapshot;
            }

            if (value is UnityEngine.Object unityObject)
            {
                snapshot.ValueKind = "unityObject";
                snapshot.Value = unityObject.name;
                snapshot.ObjectInstanceId = unityObject.GetInstanceID();
                return snapshot;
            }

            if (value is IEnumerable enumerable && !(value is string))
            {
                snapshot.ValueKind = "enumerable";
                int count = 0;
                foreach (object item in enumerable)
                {
                    if (count >= MaxEnumerableFieldValues)
                    {
                        snapshot.Truncated = true;
                        break;
                    }

                    snapshot.Items.Add(DescribeFieldItem(item));
                    count++;
                }

                snapshot.Count = count;
                return snapshot;
            }

            snapshot.ValueKind = "object";
            snapshot.Value = TruncateString(value.ToString());
            return snapshot;
        }

        private static DaFieldItemSnapshot DescribeFieldItem(object value)
        {
            DaFieldItemSnapshot item = new DaFieldItemSnapshot();
            if (value == null)
            {
                item.ValueKind = "null";
                return item;
            }

            if (value is GameObject gameObject)
            {
                item.ValueKind = "gameObject";
                item.Value = gameObject.name;
                item.ObjectPath = gameObject.transform != null ? GetHierarchyPath(gameObject.transform) : null;
                item.ObjectInstanceId = gameObject.GetInstanceID();
                return item;
            }

            if (value is Transform transform)
            {
                item.ValueKind = "transform";
                item.Value = transform.name;
                item.ObjectPath = GetHierarchyPath(transform);
                item.ObjectInstanceId = transform.gameObject != null ? transform.gameObject.GetInstanceID() : 0;
                return item;
            }

            if (value is Component component)
            {
                item.ValueKind = "component";
                item.Value = component.GetType().FullName;
                item.ObjectPath = component.transform != null ? GetHierarchyPath(component.transform) : null;
                item.ObjectInstanceId = component.gameObject != null ? component.gameObject.GetInstanceID() : 0;
                return item;
            }

            if (value is UnityEngine.Object unityObject)
            {
                item.ValueKind = "unityObject";
                item.Value = unityObject.name;
                item.ObjectInstanceId = unityObject.GetInstanceID();
                return item;
            }

            item.ValueKind = "value";
            item.Value = TruncateString(Convert.ToString(value, CultureInfo.InvariantCulture));
            return item;
        }

        private static int CountDreamyAscentRuntimeObjects()
        {
            int count = 0;
            UnityEngine.Object[] objects = Resources.FindObjectsOfTypeAll(typeof(Transform));
            for (int index = 0; objects != null && index < objects.Length; index++)
            {
                Transform transform = objects[index] as Transform;
                if (transform == null || transform.gameObject == null || !transform.gameObject.scene.IsValid())
                {
                    continue;
                }

                if (IsDreamyAscentRuntimePlacementObject(transform))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool IsDreamyAscentRuntimePlacementObject(Transform transform)
        {
            Transform current = transform;
            while (current != null)
            {
                if (string.Equals(current.name, "DreamyAscent Runtime", StringComparison.Ordinal))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static Type FindTypeByFullName(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
            {
                return null;
            }

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int assemblyIndex = 0; assemblies != null && assemblyIndex < assemblies.Length; assemblyIndex++)
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

                for (int typeIndex = 0; types != null && typeIndex < types.Length; typeIndex++)
                {
                    Type type = types[typeIndex];
                    if (type != null && string.Equals(type.FullName, fullName, StringComparison.Ordinal))
                    {
                        return type;
                    }
                }
            }

            return null;
        }

        private static object GetFieldValue(Type type, object source, string fieldName)
        {
            if (type == null || string.IsNullOrWhiteSpace(fieldName))
            {
                return null;
            }

            try
            {
                FieldInfo field = type.GetField(fieldName, ExternalFieldFlags);
                return field != null ? field.GetValue(source) : null;
            }
            catch
            {
                return null;
            }
        }

        private static string GetStringField(object source, string fieldName)
        {
            object value = source != null ? GetFieldValue(source.GetType(), source, fieldName) : null;
            return value != null ? Convert.ToString(value, CultureInfo.InvariantCulture) : null;
        }

        private static string GetFieldString(object source, string fieldName)
        {
            object value = source != null ? GetFieldValue(source.GetType(), source, fieldName) : null;
            return value != null ? value.ToString() : null;
        }

        private static bool GetBoolField(object source, string fieldName)
        {
            object value = source != null ? GetFieldValue(source.GetType(), source, fieldName) : null;
            return value is bool boolValue && boolValue;
        }

        private static int? GetNullableIntField(object source, string fieldName)
        {
            object value = source != null ? GetFieldValue(source.GetType(), source, fieldName) : null;
            if (value is int intValue)
            {
                return intValue;
            }

            return null;
        }

        private static string BuildObjectSignature(GameObject gameObject, string prefabNameGuess)
        {
            List<string> parts = new List<string>();
            parts.Add(prefabNameGuess ?? string.Empty);

            Component[] components = gameObject != null ? gameObject.GetComponents<Component>() : null;
            for (int index = 0; components != null && index < components.Length; index++)
            {
                Component component = components[index];
                parts.Add(component != null ? component.GetType().FullName : "<missing>");
            }

            Renderer[] renderers = gameObject != null ? gameObject.GetComponentsInChildren<Renderer>(true) : null;
            for (int rendererIndex = 0; renderers != null && rendererIndex < renderers.Length; rendererIndex++)
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
                    parts.Add(material != null ? material.name : string.Empty);
                }
            }

            return StableHash(string.Join("|", parts.ToArray()), 16);
        }

        private static string NormalizeCloneName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            string result = name.Trim();
            const string cloneSuffix = "(Clone)";
            if (result.EndsWith(cloneSuffix, StringComparison.Ordinal))
            {
                result = result.Substring(0, result.Length - cloneSuffix.Length).TrimEnd();
            }

            return result;
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

        private static string SafeTag(GameObject gameObject)
        {
            try
            {
                return gameObject != null ? gameObject.tag : null;
            }
            catch
            {
                return null;
            }
        }

        private static DaVector3Snapshot ToVector(Vector3 value)
        {
            return new DaVector3Snapshot
            {
                X = Round(value.x),
                Y = Round(value.y),
                Z = Round(value.z)
            };
        }

        private static DaQuaternionSnapshot ToQuaternion(Quaternion value)
        {
            return new DaQuaternionSnapshot
            {
                X = Round(value.x),
                Y = Round(value.y),
                Z = Round(value.z),
                W = Round(value.w)
            };
        }

        private static float Round(float value)
        {
            return (float)Math.Round(value, 5);
        }

        private static void AddUnique(List<string> target, string value)
        {
            if (!string.IsNullOrWhiteSpace(value) && !target.Contains(value))
            {
                target.Add(value);
            }
        }

        private static bool ContainsText(string value, string pattern)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   !string.IsNullOrWhiteSpace(pattern) &&
                   value.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string TruncateString(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= MaxFieldStringLength)
            {
                return value;
            }

            return value.Substring(0, MaxFieldStringLength);
        }

        private static string StableHash(string value, int length = 12)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            using (SHA1 sha1 = SHA1.Create())
            {
                byte[] hash = sha1.ComputeHash(bytes);
                StringBuilder builder = new StringBuilder(hash.Length * 2);
                for (int index = 0; index < hash.Length; index++)
                {
                    builder.Append(hash[index].ToString("x2", CultureInfo.InvariantCulture));
                }

                string result = builder.ToString();
                return result.Length <= length ? result : result.Substring(0, length);
            }
        }

        private sealed class DaGeneratedChildrenSnapshot
        {
            public int SchemaVersion { get; set; }
            public string GeneratedAtUtc { get; set; }
            public string MapKey { get; set; }
            public string SourceClassification { get; set; }
            public string Freshness { get; set; }
            public string ReconstructionEligibility { get; set; }
            public bool PotentiallyDirty { get; set; }
            public int SegmentCount { get; set; }
            public int StepCount { get; set; }
            public int GeneratedChildCount { get; set; }
            public int LooseObjectCount { get; set; }
            public int SpecialObjectCount { get; set; }
            public int RelationshipCandidateCount { get; set; }
            public int NonOfficialEditModeSegmentCount { get; set; }
            public int DreamyAscentRuntimeObjectCount { get; set; }
            public int ExternalMapModifierCount { get; set; }
            public List<string> DirtyReasons { get; } = new List<string>();
            public List<string> Warnings { get; } = new List<string>();
            public List<DaExternalMapModifierSnapshot> ExternalMapModifiers { get; } = new List<DaExternalMapModifierSnapshot>();
            public List<DaGeneratedSegmentSnapshot> Segments { get; } = new List<DaGeneratedSegmentSnapshot>();
        }

        private sealed class DaExternalMapModifierSnapshot
        {
            public string Name { get; set; }
            public bool Detected { get; set; }
            public bool Enabled { get; set; }
            public string Source { get; set; }
            public int? Seed { get; set; }
            public List<DaExternalBiomeSelectionSnapshot> Biomes { get; } = new List<DaExternalBiomeSelectionSnapshot>();
        }

        private sealed class DaExternalBiomeSelectionSnapshot
        {
            public string SectionName { get; set; }
            public string SelectedBiomeName { get; set; }
            public string SelectedBiomeType { get; set; }
            public string SelectedVariant { get; set; }
            public string VariantSelectionType { get; set; }
            public bool OverrideEnabled { get; set; }
            public bool RandomBiome { get; set; }
            public bool RandomVariant { get; set; }
        }

        private sealed class DaGeneratedSegmentSnapshot
        {
            public string SegmentName { get; set; }
            public int LevelSlot { get; set; }
            public string SegmentPath { get; set; }
            public string VariantSelectionType { get; set; }
            public List<string> ActiveVariantNames { get; } = new List<string>();
            public List<string> ActiveVariantPaths { get; } = new List<string>();
            public string NormalizedVariantName { get; set; }
            public List<string> RootPaths { get; } = new List<string>();
            public string EditMode { get; set; }
            public int StepCount { get; set; }
            public int GeneratedChildCount { get; set; }
            public int LooseObjectCount { get; set; }
            public int SpecialObjectCount { get; set; }
            public int RelationshipCandidateCount { get; set; }
            public List<DaGeneratedGrouperSnapshot> Groupers { get; } = new List<DaGeneratedGrouperSnapshot>();
            public List<DaGeneratedObjectSnapshot> LooseObjects { get; } = new List<DaGeneratedObjectSnapshot>();
            public List<DaSpecialObjectSnapshot> SpecialObjects { get; } = new List<DaSpecialObjectSnapshot>();
            public List<DaRelationshipCandidateSnapshot> RelationshipCandidates { get; } = new List<DaRelationshipCandidateSnapshot>();
        }

        private sealed class DaGeneratedGrouperSnapshot
        {
            public string GrouperName { get; set; }
            public string GrouperPath { get; set; }
            public string RuntimeType { get; set; }
            public int StepCount { get; set; }
            public int GeneratedChildCount { get; set; }
            public List<DaGeneratedStepSnapshot> Steps { get; } = new List<DaGeneratedStepSnapshot>();
        }

        private sealed class DaGeneratedStepSnapshot
        {
            public string StepName { get; set; }
            public string StepType { get; set; }
            public string StepPath { get; set; }
            public string GrouperName { get; set; }
            public string GrouperPath { get; set; }
            public string RuntimeType { get; set; }
            public bool RuntimeReferenceMissing { get; set; }
            public DaTransformSnapshot Transform { get; set; }
            public DaBoundsSnapshot Bounds { get; set; }
            public int DirectChildCount { get; set; }
            public int DescendantObjectCount { get; set; }
            public int RendererCount { get; set; }
            public int ColliderCount { get; set; }
            public List<DaComponentFieldSnapshot> InterestingComponentFields { get; } = new List<DaComponentFieldSnapshot>();
            public List<string> ModifierTypes { get; } = new List<string>();
            public List<string> ConstraintTypes { get; } = new List<string>();
            public List<string> PostConstraintTypes { get; } = new List<string>();
            public List<DaGeneratedObjectSnapshot> Children { get; } = new List<DaGeneratedObjectSnapshot>();
        }

        private interface DaObjectRiskInfoSnapshot
        {
            bool HasNestedLevelGenStep { get; set; }
            bool HasNestedPropGrouper { get; set; }
            bool HasSingleItemSpawner { get; set; }
            bool HasPhotonView { get; set; }
            bool HasKnownSpawner { get; set; }
            bool HasChildSpawnerMod { get; set; }
            bool HasSingleItemSpawnerMod { get; set; }
            bool NeedsParentChildHandling { get; set; }
            bool NeedsNetworkHandling { get; set; }
            bool IsHighRiskDirectPlacement { get; set; }
        }

        private sealed class DaGeneratedObjectSnapshot : DaObjectRiskInfoSnapshot
        {
            public string SourceKind { get; set; }
            public string SourceStepPath { get; set; }
            public int ChildIndex { get; set; }
            public string Name { get; set; }
            public string PrefabNameGuess { get; set; }
            public string Path { get; set; }
            public string ParentPath { get; set; }
            public string RootPath { get; set; }
            public string PathHash { get; set; }
            public string StableSignature { get; set; }
            public string Scene { get; set; }
            public bool ActiveSelf { get; set; }
            public bool ActiveInHierarchy { get; set; }
            public int Layer { get; set; }
            public string Tag { get; set; }
            public DaTransformSnapshot Transform { get; set; }
            public DaBoundsSnapshot Bounds { get; set; }
            public int ChildCount { get; set; }
            public int DescendantObjectCount { get; set; }
            public int RendererCount { get; set; }
            public int ColliderCount { get; set; }
            public int LevelGenStepCount { get; set; }
            public int PropGrouperCount { get; set; }
            public int SingleItemSpawnerCount { get; set; }
            public int PhotonViewCount { get; set; }
            public bool HasNestedLevelGenStep { get; set; }
            public bool HasNestedPropGrouper { get; set; }
            public bool HasSingleItemSpawner { get; set; }
            public bool HasPhotonView { get; set; }
            public bool HasKnownSpawner { get; set; }
            public bool HasChildSpawnerMod { get; set; }
            public bool HasSingleItemSpawnerMod { get; set; }
            public bool NeedsParentChildHandling { get; set; }
            public bool NeedsNetworkHandling { get; set; }
            public bool IsHighRiskDirectPlacement { get; set; }
            public List<string> Components { get; } = new List<string>();
            public List<string> DescendantComponentTypes { get; } = new List<string>();
            public List<string> RendererMaterials { get; } = new List<string>();
            public List<DaChildObjectSummarySnapshot> ChildSummaries { get; } = new List<DaChildObjectSummarySnapshot>();
            public List<DaComponentFieldSnapshot> InterestingComponentFields { get; } = new List<DaComponentFieldSnapshot>();
        }

        private sealed class DaRelationshipCandidateSnapshot : DaObjectRiskInfoSnapshot
        {
            public string SourceKind { get; set; }
            public string Reason { get; set; }
            public string Name { get; set; }
            public string Path { get; set; }
            public string ParentPath { get; set; }
            public string SourceStepPath { get; set; }
            public string SourceGrouperPath { get; set; }
            public string GeneratedRootPath { get; set; }
            public string PathHash { get; set; }
            public string StableSignature { get; set; }
            public DaTransformSnapshot Transform { get; set; }
            public DaBoundsSnapshot Bounds { get; set; }
            public int ChildCount { get; set; }
            public int DescendantObjectCount { get; set; }
            public bool HasNestedLevelGenStep { get; set; }
            public bool HasNestedPropGrouper { get; set; }
            public bool HasSingleItemSpawner { get; set; }
            public bool HasPhotonView { get; set; }
            public bool HasKnownSpawner { get; set; }
            public bool HasChildSpawnerMod { get; set; }
            public bool HasSingleItemSpawnerMod { get; set; }
            public bool NeedsParentChildHandling { get; set; }
            public bool NeedsNetworkHandling { get; set; }
            public bool IsHighRiskDirectPlacement { get; set; }
            public List<string> Components { get; } = new List<string>();
            public List<DaChildObjectSummarySnapshot> Children { get; } = new List<DaChildObjectSummarySnapshot>();
            public List<DaComponentFieldSnapshot> InterestingComponentFields { get; } = new List<DaComponentFieldSnapshot>();
        }

        private sealed class DaChildObjectSummarySnapshot
        {
            public int Depth { get; set; }
            public int ChildIndex { get; set; }
            public string Name { get; set; }
            public string Path { get; set; }
            public string PathHash { get; set; }
            public bool ActiveSelf { get; set; }
            public bool ActiveInHierarchy { get; set; }
            public int RendererCount { get; set; }
            public int ColliderCount { get; set; }
            public int LevelGenStepCount { get; set; }
            public int PropGrouperCount { get; set; }
            public int SingleItemSpawnerCount { get; set; }
            public int PhotonViewCount { get; set; }
            public DaTransformSnapshot Transform { get; set; }
            public List<string> Components { get; } = new List<string>();
        }

        private sealed class DaComponentFieldSnapshot
        {
            public string ComponentType { get; set; }
            public string ComponentName { get; set; }
            public string ObjectPath { get; set; }
            public List<DaFieldValueSnapshot> Fields { get; } = new List<DaFieldValueSnapshot>();
        }

        private sealed class DaFieldValueSnapshot
        {
            public string Name { get; set; }
            public string Type { get; set; }
            public string ValueKind { get; set; }
            public string Value { get; set; }
            public string ObjectPath { get; set; }
            public int ObjectInstanceId { get; set; }
            public int Count { get; set; }
            public bool Truncated { get; set; }
            public List<DaFieldItemSnapshot> Items { get; } = new List<DaFieldItemSnapshot>();
        }

        private sealed class DaFieldItemSnapshot
        {
            public string ValueKind { get; set; }
            public string Value { get; set; }
            public string ObjectPath { get; set; }
            public int ObjectInstanceId { get; set; }
        }

        private sealed class DaSpecialObjectSnapshot
        {
            public string Id { get; set; }
            public string DisplayName { get; set; }
            public string Category { get; set; }
            public string Reason { get; set; }
            public string Path { get; set; }
            public string ParentPath { get; set; }
            public string RootPath { get; set; }
            public bool ActiveSelf { get; set; }
            public bool ActiveInHierarchy { get; set; }
            public int Layer { get; set; }
            public string Tag { get; set; }
            public DaVector3Snapshot LocalPosition { get; set; }
            public DaVector3Snapshot WorldPosition { get; set; }
            public int RendererCount { get; set; }
            public int ColliderCount { get; set; }
            public bool CanToggleActive { get; set; }
            public bool CanDelete { get; set; }
            public bool IsProtected { get; set; }
            public string ProtectionReason { get; set; }
            public List<string> Components { get; } = new List<string>();
            public List<string> Materials { get; } = new List<string>();
        }

        private sealed class DaTransformSnapshot
        {
            public DaVector3Snapshot LocalPosition { get; set; }
            public DaVector3Snapshot WorldPosition { get; set; }
            public DaVector3Snapshot LocalEulerAngles { get; set; }
            public DaVector3Snapshot WorldEulerAngles { get; set; }
            public DaQuaternionSnapshot LocalRotation { get; set; }
            public DaQuaternionSnapshot WorldRotation { get; set; }
            public DaVector3Snapshot LocalScale { get; set; }
            public DaVector3Snapshot LossyScale { get; set; }
        }

        private sealed class DaBoundsSnapshot
        {
            public bool HasBounds { get; set; }
            public DaVector3Snapshot Center { get; set; }
            public DaVector3Snapshot Size { get; set; }
            public DaVector3Snapshot Min { get; set; }
            public DaVector3Snapshot Max { get; set; }
        }

        private sealed class DaVector3Snapshot
        {
            public float X { get; set; }
            public float Y { get; set; }
            public float Z { get; set; }
        }

        private sealed class DaQuaternionSnapshot
        {
            public float X { get; set; }
            public float Y { get; set; }
            public float Z { get; set; }
            public float W { get; set; }
        }
    }
}
