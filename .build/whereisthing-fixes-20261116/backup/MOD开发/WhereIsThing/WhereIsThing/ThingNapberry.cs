using System;
using System.Collections.Generic;
using System.Linq;

namespace WhereIsThing
{
    internal static class ThingNapberry
    {
        internal static bool IsNapberry(Item item)
        {
            Item identity = ThingPickupIdentity.Resolve(item);
            return identity != null && identity.UIData != null &&
                string.Equals(identity.UIData.itemName, "Napberry", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsAvailable(FakeItem fruit)
        {
            return fruit != null && fruit.gameObject.activeInHierarchy && !fruit.pickedUp &&
                fruit.gameObject.scene.IsValid() && IsNapberry(fruit.realItemPrefab);
        }

        // Expand only the effective selection. Keep saved/shared legacy tokens readable.
        internal static void ExpandSelection(IEnumerable<ThingTargetDefinition> catalog,
            HashSet<ushort> items, HashSet<ThingSceneTargetType> scenes)
        {
            foreach (ThingTargetDefinition definition in catalog)
            {
                if (!definition.IsSceneTarget || definition.SceneTargetType != ThingSceneTargetType.NapberryHypnoOrb) continue;
                if (!scenes.Contains(ThingSceneTargetType.NapberryHypnoOrb) && !definition.ItemIds.Any(items.Contains)) continue;
                items.UnionWith(definition.ItemIds);
                scenes.Add(ThingSceneTargetType.NapberryHypnoOrb);
            }
        }
    }
}
