using System;
using System.Collections.Generic;
using System.Linq;

namespace WhereIsThing
{
    internal static class ThingPresetSelection
    {
        internal static bool IsClientRestricted(bool enabled, bool inRoom, bool offline, bool host)
        {
            return enabled && inRoom && !offline && !host;
        }

        internal static List<string> Load(string serialized, string legacy, IEnumerable<string> available)
        {
            var valid = new HashSet<string>(available, StringComparer.Ordinal);
            var result = (serialized ?? string.Empty).Split(',').Select(id => id.Trim())
                .Where(valid.Contains).Distinct(StringComparer.Ordinal).ToList();
            if (result.Count == 0 && valid.Contains(legacy ?? string.Empty)) result.Add(legacy);
            if (result.Count == 0 && available.Any()) result.Add(available.First());
            return result;
        }

        internal static bool Toggle(List<string> active, string id)
        {
            if (active.Contains(id))
            {
                if (active.Count <= 1) return false;
                active.Remove(id);
            }
            else active.Add(id);
            return true;
        }

        internal static bool Combine(IEnumerable<ThingPresetDefinition> presets, HashSet<ushort> items,
            HashSet<ThingLuggageType> luggage, HashSet<ThingSceneTargetType> scenes)
        {
            items.Clear(); luggage.Clear(); scenes.Clear();
            bool placed = false;
            foreach (ThingPresetDefinition preset in presets.Where(value => value != null))
            {
                items.UnionWith(preset.SelectedItemIds);
                luggage.UnionWith(preset.SelectedLuggageTypes);
                scenes.UnionWith(preset.SelectedSceneTargetTypes);
                placed |= string.Equals(preset.Id, ThingPresetFactory.PlayerPlacedPresetId, StringComparison.Ordinal);
            }
            return placed;
        }
    }
}
