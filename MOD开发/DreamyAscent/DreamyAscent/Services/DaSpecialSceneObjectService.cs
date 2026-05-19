using System;
using System.Collections.Generic;
using System.Reflection;
using DreamyAscent.Data;
using UnityEngine;

namespace DreamyAscent.Services
{
    internal static class DaSpecialSceneObjectService
    {
        public static Dictionary<DaSegmentData, List<DaSpecialSceneObjectData>> ScanAll(DaTerrainData data)
        {
            Dictionary<DaSegmentData, List<DaSpecialSceneObjectData>> result = new Dictionary<DaSegmentData, List<DaSpecialSceneObjectData>>();
            if (data == null || data.Map == null || data.Map.Segments == null)
            {
                return result;
            }

            Dictionary<GameObject, string> candidates = BuildSceneCandidates(data);
            for (int segmentIndex = 0; segmentIndex < data.Map.Segments.Count; segmentIndex++)
            {
                DaSegmentData segment = data.Map.Segments[segmentIndex];
                if (segment == null)
                {
                    continue;
                }

                List<DaSpecialSceneObjectData> items = new List<DaSpecialSceneObjectData>();
                foreach (KeyValuePair<GameObject, string> pair in candidates)
                {
                    GameObject gameObject = pair.Key;
                    if (gameObject == null || !gameObject.scene.IsValid() || !ShouldIncludeForSegment(segment, gameObject))
                    {
                        continue;
                    }

                    DaSpecialSceneObjectData item = BuildItem(segment, gameObject, pair.Value);
                    if (item != null)
                    {
                        items.Add(item);
                    }
                }

                items.Sort((left, right) => string.Compare(left.Path, right.Path, StringComparison.OrdinalIgnoreCase));
                result[segment] = items;
            }

            return result;
        }

        public static List<DaSpecialSceneObjectData> Scan(DaSegmentData segment)
        {
            List<DaSpecialSceneObjectData> result = new List<DaSpecialSceneObjectData>();
            if (segment == null)
            {
                return result;
            }

            Dictionary<GameObject, string> candidates = BuildSceneCandidates();
            foreach (KeyValuePair<GameObject, string> pair in candidates)
            {
                GameObject gameObject = pair.Key;
                if (gameObject == null || !gameObject.scene.IsValid())
                {
                    continue;
                }

                if (!ShouldIncludeForSegment(segment, gameObject))
                {
                    continue;
                }

                DaSpecialSceneObjectData item = BuildItem(segment, gameObject, pair.Value);
                if (item != null)
                {
                    result.Add(item);
                }
            }

            result.Sort((left, right) => string.Compare(left.Path, right.Path, StringComparison.OrdinalIgnoreCase));
            return result;
        }

        private static Dictionary<GameObject, string> BuildSceneCandidates()
        {
            return BuildSceneCandidates(null);
        }

        private static Dictionary<GameObject, string> BuildSceneCandidates(DaTerrainData data)
        {
            Dictionary<GameObject, string> candidates = new Dictionary<GameObject, string>();
            AddComponentTypeCandidates(candidates, "Capybara", "Capybara component");
            AddComponentTypeCandidates(candidates, "BreakableBridge", "Bridge component");
            AddComponentTypeCandidates(candidates, "SpawnConnectingBridge", "Bridge spawn condition");
            AddComponentTypeCandidates(candidates, "Campfire", "Campfire component");
            AddComponentTypeCandidates(candidates, "RespawnChest", "Campfire attachment");
            AddComponentTypeCandidates(candidates, "CampfireSectionGroundStealer", "Campfire attachment");
            AddComponentTypeCandidates(candidates, "MovingLava", "Rising lava mechanism");
            AddComponentTypeCandidates(candidates, "LavaRising", "Rising lava mechanism");
            AddComponentTypeCandidates(candidates, "LavaTides", "Lava mechanism");
            AddComponentTypeCandidates(candidates, "TempleConfig", "Independent mechanism");
            AddComponentTypeCandidates(candidates, "TempleEntranceRope", "Independent mechanism");
            AddRendererMaterialCandidates(candidates, "M_Capybara", "Capybara material");
            AddRendererMaterialCandidates(candidates, "M_RopeBridge", "Bridge material");
            AddRendererMaterialCandidates(candidates, "M_RopeBridgeWood", "Bridge material");
            AddRendererMaterialCandidates(candidates, "M_RopeBridgeCold", "Bridge material");
            AddNamePathCandidates(candidates, data);
            return candidates;
        }

        private static bool ShouldIncludeForSegment(DaSegmentData segment, GameObject gameObject)
        {
            if (segment == null || gameObject == null)
            {
                return false;
            }

            if (IsUnderSegmentRoot(segment, gameObject.transform))
            {
                return true;
            }

            string segmentName = segment.SegmentName ?? string.Empty;
            if (IsCapybaraCandidate(gameObject))
            {
                return segmentName.IndexOf("Desert", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       segmentName.IndexOf("Snow", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            return IsCampfireCandidate(gameObject) && IsCampfireForSegment(segment, gameObject);
        }

        private static bool IsUnderSegmentRoot(DaSegmentData segment, Transform transform)
        {
            Transform current = transform;
            while (current != null)
            {
                for (int index = 0; segment != null && segment.SourceRoots != null && index < segment.SourceRoots.Count; index++)
                {
                    if (segment.SourceRoots[index] == current)
                    {
                        return true;
                    }
                }

                current = current.parent;
            }

            return false;
        }

        private static bool IsCapybaraCandidate(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return false;
            }

            return ContainsCapybaraText(gameObject.name) ||
                   ContainsCapybaraText(GetHierarchyPath(gameObject.transform)) ||
                   CountComponentsByTypeName(gameObject, "Capybara") > 0 ||
                   HasMaterial(gameObject, "M_Capybara");
        }

        private static bool HasMaterial(GameObject gameObject, string materialName)
        {
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
                    if (material != null && string.Equals(material.name, materialName, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool TrySetActive(DaSpecialSceneObjectData item, bool active)
        {
            if (item == null || item.SourceObject == null || !item.CanToggleActive)
            {
                return false;
            }

            item.SourceObject.SetActive(active);
            return true;
        }

        public static bool TryDelete(DaSpecialSceneObjectData item, out string failure)
        {
            failure = string.Empty;
            if (item == null || item.SourceObject == null)
            {
                failure = "ObjectMissing";
                return false;
            }

            if (!item.CanDelete)
            {
                failure = item.ProtectionReason ?? "Protected";
                return false;
            }

            UnityEngine.Object.DestroyImmediate(item.SourceObject);
            return true;
        }

        private static void AddComponentTypeCandidates(Dictionary<GameObject, string> candidates, string typeName, string reason)
        {
            Type type = FindTypeByName(typeName);
            if (type == null)
            {
                return;
            }

            UnityEngine.Object[] objects = Resources.FindObjectsOfTypeAll(type);
            for (int index = 0; objects != null && index < objects.Length; index++)
            {
                Component component = objects[index] as Component;
                if (component == null || component.gameObject == null || !component.gameObject.scene.IsValid())
                {
                    continue;
                }

                AddCandidate(candidates, GetNamedAncestorOrSelf(component.transform, typeName), reason);
            }
        }

        private static void AddRendererMaterialCandidates(Dictionary<GameObject, string> candidates, string materialName, string reason)
        {
            UnityEngine.Object[] objects = Resources.FindObjectsOfTypeAll(typeof(Renderer));
            for (int index = 0; objects != null && index < objects.Length; index++)
            {
                Renderer renderer = objects[index] as Renderer;
                if (renderer == null || renderer.gameObject == null || !renderer.gameObject.scene.IsValid() || renderer.sharedMaterials == null)
                {
                    continue;
                }

                Material[] materials = renderer.sharedMaterials;
                bool matched = false;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material material = materials[materialIndex];
                    if (material != null && string.Equals(material.name, materialName, StringComparison.Ordinal))
                    {
                        matched = true;
                        break;
                    }
                }

                if (matched)
                {
                    AddCandidate(candidates, GetSpecialRemovalRoot(renderer.transform), reason);
                }
            }
        }

        private static void AddNamePathCandidates(Dictionary<GameObject, string> candidates, DaTerrainData data)
        {
            if (data != null && data.Map != null && data.Map.Segments != null)
            {
                HashSet<Transform> roots = new HashSet<Transform>();
                for (int segmentIndex = 0; segmentIndex < data.Map.Segments.Count; segmentIndex++)
                {
                    DaSegmentData segment = data.Map.Segments[segmentIndex];
                    for (int rootIndex = 0; segment != null && segment.SourceRoots != null && rootIndex < segment.SourceRoots.Count; rootIndex++)
                    {
                        if (segment.SourceRoots[rootIndex] != null)
                        {
                            roots.Add(segment.SourceRoots[rootIndex]);
                        }
                    }

                    if (segment != null && segment.SourceSegment != null && segment.SourceSegment.segmentCampfire != null)
                    {
                        roots.Add(segment.SourceSegment.segmentCampfire.transform);
                    }
                }

                foreach (Transform root in roots)
                {
                    AddNamePathCandidatesUnderRoot(candidates, root);
                }

                return;
            }

            UnityEngine.Object[] objects = Resources.FindObjectsOfTypeAll(typeof(Transform));
            for (int index = 0; objects != null && index < objects.Length; index++)
            {
                Transform transform = objects[index] as Transform;
                if (transform == null || transform.gameObject == null || !transform.gameObject.scene.IsValid())
                {
                    continue;
                }

                string reason;
                if (!TryGetSpecialReason(transform, out reason))
                {
                    continue;
                }

                AddCandidate(candidates, GetSpecialRoot(transform), reason);
            }
        }

        private static void AddNamePathCandidatesUnderRoot(Dictionary<GameObject, string> candidates, Transform root)
        {
            Transform[] transforms = root != null ? root.GetComponentsInChildren<Transform>(true) : null;
            for (int index = 0; transforms != null && index < transforms.Length; index++)
            {
                Transform transform = transforms[index];
                string reason;
                if (transform != null && TryGetSpecialReason(transform, out reason))
                {
                    AddCandidate(candidates, GetSpecialRoot(transform), reason);
                }
            }
        }

        private static void AddCandidate(Dictionary<GameObject, string> candidates, Transform transform, string reason)
        {
            if (candidates == null || transform == null || transform.gameObject == null)
            {
                return;
            }

            GameObject gameObject = transform.gameObject;
            if (!candidates.ContainsKey(gameObject))
            {
                candidates[gameObject] = reason;
            }
        }

        private static DaSpecialSceneObjectData BuildItem(DaSegmentData segment, GameObject gameObject, string reason)
        {
            Transform transform = gameObject != null ? gameObject.transform : null;
            if (transform == null)
            {
                return null;
            }

            DaSpecialSceneObjectData item = new DaSpecialSceneObjectData
            {
                Id = gameObject.GetInstanceID().ToString(System.Globalization.CultureInfo.InvariantCulture),
                DisplayName = gameObject.name,
                Category = GetCategory(gameObject),
                Reason = reason,
                Path = GetHierarchyPath(transform),
                ParentPath = transform.parent != null ? GetHierarchyPath(transform.parent) : string.Empty,
                RootPath = GetNearestSegmentRootPath(segment, transform),
                ActiveSelf = gameObject.activeSelf,
                ActiveInHierarchy = gameObject.activeInHierarchy,
                Layer = gameObject.layer,
                Tag = SafeTag(gameObject),
                LocalPosition = transform.localPosition,
                WorldPosition = transform.position,
                RendererCount = gameObject.GetComponentsInChildren<Renderer>(true).Length,
                ColliderCount = gameObject.GetComponentsInChildren<Collider>(true).Length,
                SourceObject = gameObject
            };
            ApplyManagementPolicy(item, gameObject);

            Component[] components = gameObject.GetComponents<Component>();
            for (int index = 0; components != null && index < components.Length; index++)
            {
                Component component = components[index];
                item.Components.Add(component != null ? component.GetType().Name : "<missing>");
            }

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
                    string materialName = material != null ? material.name : string.Empty;
                    if (!string.IsNullOrWhiteSpace(materialName) && !item.Materials.Contains(materialName))
                    {
                        item.Materials.Add(materialName);
                    }
                }
            }

            return item;
        }

        private static bool TryGetSpecialReason(Transform transform, out string reason)
        {
            reason = string.Empty;
            if (transform == null)
            {
                return false;
            }

            string path = GetHierarchyPath(transform);
            if (ContainsCapybaraText(transform.name) || ContainsCapybaraText(path))
            {
                reason = "Capybara name/path";
                return true;
            }

            if (ContainsBridgeText(transform.name))
            {
                reason = "Bridge name/path";
                return true;
            }

            if (ContainsRisingLavaText(transform.name) || ContainsRisingLavaText(path))
            {
                reason = "Rising lava name/path";
                return true;
            }

            if (ContainsCampfireText(transform.name) || ContainsCampfireText(path))
            {
                reason = "Campfire name/path";
                return true;
            }

            if (ContainsMechanismText(transform.name) || ContainsMechanismText(path))
            {
                reason = "Independent mechanism name/path";
                return true;
            }

            Component[] components = transform.GetComponents<Component>();
            for (int index = 0; components != null && index < components.Length; index++)
            {
                Component component = components[index];
                if (component != null && ContainsCapybaraText(component.GetType().Name))
                {
                    reason = "Capybara component";
                    return true;
                }

                if (component != null && IsSpecialComponent(component.GetType().Name, out reason))
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
                if (material != null && ContainsCapybaraText(material.name))
                {
                    reason = "Capybara material";
                    return true;
                }

                if (material != null && ContainsBridgeText(material.name))
                {
                    reason = "Bridge material";
                    return true;
                }
            }

            return false;
        }

        private static Transform GetSpecialRemovalRoot(Transform transform)
        {
            Transform current = transform;
            Transform match = transform;
            while (current != null)
            {
                if (ContainsCapybaraText(current.name) || ContainsBridgeText(current.name))
                {
                    match = current;
                }

                current = current.parent;
            }

            return match;
        }

        private static Transform GetSpecialRoot(Transform transform)
        {
            Transform current = transform;
            Transform match = transform;
            while (current != null)
            {
                if (ContainsCapybaraText(current.name) ||
                    ContainsBridgeText(current.name) ||
                    ContainsCampfireText(current.name) ||
                    ContainsRisingLavaText(current.name) ||
                    ContainsMechanismText(current.name))
                {
                    match = current;
                }

                current = current.parent;
            }

            return match;
        }

        private static Transform GetNamedAncestorOrSelf(Transform transform, string typeName)
        {
            Transform current = transform;
            Transform match = transform;
            while (current != null)
            {
                if (ContainsText(current.name, typeName))
                {
                    match = current;
                }

                current = current.parent;
            }

            return match;
        }

        private static string GetCategory(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return string.Empty;
            }

            if (CountComponentsByTypeName(gameObject, "Capybara") > 0 || ContainsCapybaraText(gameObject.name))
            {
                return "Capybara";
            }

            if (IsBridgeCandidate(gameObject))
            {
                return "Bridge";
            }

            if (IsCampfireCandidate(gameObject))
            {
                return "Campfire";
            }

            if (IsRisingLavaCandidate(gameObject))
            {
                return "RisingLava";
            }

            if (IsMechanismCandidate(gameObject))
            {
                return "Mechanism";
            }

            return "Special";
        }

        private static void ApplyManagementPolicy(DaSpecialSceneObjectData item, GameObject gameObject)
        {
            item.CanToggleActive = true;
            item.CanDelete = true;

            if (IsCampfireCandidate(gameObject))
            {
                item.IsProtected = true;
                item.CanToggleActive = false;
                item.CanDelete = false;
                item.ProtectionReason = "Campfire roots are used by map progression and respawn logic.";
                return;
            }

            if (IsRisingLavaCandidate(gameObject))
            {
                item.IsProtected = true;
                item.CanToggleActive = false;
                item.CanDelete = false;
                item.ProtectionReason = "Rising lava drives the Volcano mechanism and should not be deleted.";
                return;
            }

            if (IsMechanismCandidate(gameObject))
            {
                item.IsProtected = true;
                item.CanToggleActive = false;
                item.CanDelete = false;
                item.ProtectionReason = "Independent mechanisms are protected until explicit rules exist.";
            }
        }

        private static int CountComponentsByTypeName(GameObject gameObject, string typeName)
        {
            int count = 0;
            Component[] components = gameObject != null ? gameObject.GetComponentsInChildren<Component>(true) : null;
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

        private static string GetNearestSegmentRootPath(DaSegmentData segment, Transform transform)
        {
            Transform current = transform;
            while (current != null)
            {
                for (int index = 0; segment != null && segment.SourceRoots != null && index < segment.SourceRoots.Count; index++)
                {
                    if (segment.SourceRoots[index] == current)
                    {
                        return GetHierarchyPath(current);
                    }
                }

                current = current.parent;
            }

            return string.Empty;
        }

        private static bool IsCampfireForSegment(DaSegmentData segment, GameObject gameObject)
        {
            if (segment == null || gameObject == null || segment.SourceSegment == null || segment.SourceSegment.segmentCampfire == null)
            {
                return false;
            }

            return gameObject == segment.SourceSegment.segmentCampfire || IsUnderRoot(gameObject.transform, segment.SourceSegment.segmentCampfire.transform);
        }

        private static bool IsUnderRoot(Transform transform, Transform root)
        {
            Transform current = transform;
            while (current != null)
            {
                if (current == root)
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static string SafeTag(GameObject gameObject)
        {
            try
            {
                return gameObject != null ? gameObject.tag : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool ContainsCapybaraText(string value)
        {
            return ContainsText(value, "Capy") ||
                   ContainsText(value, "Capybara") ||
                   ContainsText(value, "M_Capybara") ||
                   ContainsText(value, "卡皮") ||
                   ContainsText(value, "水豚");
        }

        private static bool IsSpecialComponent(string componentName, out string reason)
        {
            reason = string.Empty;
            if (ContainsBridgeText(componentName))
            {
                reason = "Bridge component";
                return true;
            }

            if (ContainsCampfireText(componentName) || ContainsText(componentName, "RespawnChest"))
            {
                reason = "Campfire component";
                return true;
            }

            if (ContainsRisingLavaText(componentName))
            {
                reason = "Rising lava component";
                return true;
            }

            if (ContainsMechanismText(componentName))
            {
                reason = "Independent mechanism component";
                return true;
            }

            return false;
        }

        private static bool IsBridgeCandidate(GameObject gameObject)
        {
            return gameObject != null &&
                   (ContainsBridgeText(gameObject.name) ||
                    CountComponentsByTypeName(gameObject, "BreakableBridge") > 0 ||
                    CountComponentsByTypeName(gameObject, "SpawnConnectingBridge") > 0 ||
                    HasMaterial(gameObject, "M_RopeBridge") ||
                    HasMaterial(gameObject, "M_RopeBridgeWood") ||
                    HasMaterial(gameObject, "M_RopeBridgeCold"));
        }

        private static bool IsCampfireCandidate(GameObject gameObject)
        {
            return gameObject != null &&
                   (ContainsCampfireText(gameObject.name) ||
                    ContainsCampfireText(GetHierarchyPath(gameObject.transform)) ||
                    CountComponentsByTypeName(gameObject, "Campfire") > 0 ||
                    CountComponentsByTypeName(gameObject, "RespawnChest") > 0 ||
                    CountComponentsByTypeName(gameObject, "CampfireSectionGroundStealer") > 0);
        }

        private static bool IsRisingLavaCandidate(GameObject gameObject)
        {
            return gameObject != null &&
                   (ContainsRisingLavaText(gameObject.name) ||
                    ContainsRisingLavaText(GetHierarchyPath(gameObject.transform)) ||
                    CountComponentsByTypeName(gameObject, "MovingLava") > 0 ||
                    CountComponentsByTypeName(gameObject, "LavaRising") > 0 ||
                    CountComponentsByTypeName(gameObject, "LavaTides") > 0);
        }

        private static bool IsMechanismCandidate(GameObject gameObject)
        {
            return gameObject != null &&
                   (ContainsMechanismText(gameObject.name) ||
                    ContainsMechanismText(GetHierarchyPath(gameObject.transform)) ||
                    CountComponentsByTypeName(gameObject, "TempleConfig") > 0 ||
                    CountComponentsByTypeName(gameObject, "TempleEntranceRope") > 0);
        }

        private static bool ContainsBridgeText(string value)
        {
            if (string.Equals(value, "Bridges", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "TreePlatformBridges", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return ContainsText(value, "Bridge") ||
                   ContainsText(value, "HangBridge") ||
                   ContainsText(value, "LavaBridge") ||
                   ContainsText(value, "RopeBridge");
        }

        private static bool ContainsCampfireText(string value)
        {
            return ContainsText(value, "Campfire") ||
                   ContainsText(value, "RespawnChest");
        }

        private static bool ContainsRisingLavaText(string value)
        {
            return ContainsText(value, "RisingLava") ||
                   ContainsText(value, "MovingLava") ||
                   ContainsText(value, "LavaTides");
        }

        private static bool ContainsMechanismText(string value)
        {
            return ContainsText(value, "LavaTemple") ||
                   ContainsText(value, "TempleEntrance") ||
                   ContainsText(value, "TempleConfig") ||
                   ContainsText(value, "TempleEntranceRope");
        }

        private static bool ContainsText(string value, string pattern)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   !string.IsNullOrWhiteSpace(pattern) &&
                   value.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Type FindTypeByName(string typeName)
        {
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
                    if (type != null && (string.Equals(type.Name, typeName, StringComparison.Ordinal) || string.Equals(type.FullName, typeName, StringComparison.Ordinal)))
                    {
                        return type;
                    }
                }
            }

            return null;
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
                names.Push(current.name ?? string.Empty);
                current = current.parent;
            }

            return string.Join("/", names.ToArray());
        }
    }
}
