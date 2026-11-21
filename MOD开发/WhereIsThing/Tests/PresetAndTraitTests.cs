using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WhereIsThing;
using Zorro.Core;

internal static class PresetAndTraitTests
{
    internal static void Run(Action<string, bool> check, Func<ushort, string, string, Item> make)
    {
        foreach (bool enabled in new[] { false, true })
        foreach (bool inRoom in new[] { false, true })
        foreach (bool offline in new[] { false, true })
        foreach (bool host in new[] { false, true })
            check($"client restriction: enabled={enabled}, room={inRoom}, offline={offline}, host={host}",
                ThingPresetSelection.IsClientRestricted(enabled, inRoom, offline, host) ==
                (enabled && inRoom && !offline && !host));

        string[] ids = { "first", "second", ThingPresetFactory.PlayerPlacedPresetId };
        check("legacy active preset migrates", ThingPresetSelection.Load("", "second", ids).SequenceEqual(new[] { "second" }));
        check("multi-selection trims, deduplicates and discards missing IDs",
            ThingPresetSelection.Load(" second, missing, first,second ", "first", ids).SequenceEqual(new[] { "second", "first" }));
        check("valid multi-selection takes priority over legacy",
            ThingPresetSelection.Load("second", "first", ids).SequenceEqual(new[] { "second" }));
        check("invalid selection falls back to legacy",
            ThingPresetSelection.Load("missing", "second", ids).SequenceEqual(new[] { "second" }));
        check("missing legacy falls back to first preset",
            ThingPresetSelection.Load(null, "missing", ids).SequenceEqual(new[] { "first" }));
        check("empty catalog has no fabricated preset", ThingPresetSelection.Load(null, null, Array.Empty<string>()).Count == 0);
        var active = new List<string> { "first" };
        check("cannot disable final preset", !ThingPresetSelection.Toggle(active, "first") && active.SequenceEqual(new[] { "first" }));
        check("enabling preserves existing preset", ThingPresetSelection.Toggle(active, "second") && active.SequenceEqual(new[] { "first", "second" }));
        check("disabling keeps other preset", ThingPresetSelection.Toggle(active, "first") && active.SequenceEqual(new[] { "second" }));
        var a = new ThingPresetDefinition("first", "A");
        var b = new ThingPresetDefinition("second", "B");
        a.SelectedItemIds.UnionWith(new ushort[] { 1, 2 }); b.SelectedItemIds.UnionWith(new ushort[] { 2, 3 });
        a.SelectedLuggageTypes.Add(ThingLuggageType.Beach); b.SelectedLuggageTypes.Add(ThingLuggageType.Cursed);
        a.SelectedSceneTargetTypes.Add(ThingSceneTargetType.RopePlaced); b.SelectedSceneTargetTypes.Add(ThingSceneTargetType.TrapChest);
        var items = new HashSet<ushort> { 999 };
        var luggage = new HashSet<ThingLuggageType> { ThingLuggageType.Jungle };
        var scenes = new HashSet<ThingSceneTargetType>();
        bool placed = ThingPresetSelection.Combine(new[] { a, b, null }, items, luggage, scenes);
        check("preset union clears stale targets and deduplicates items", items.SetEquals(new ushort[] { 1, 2, 3 }));
        check("preset union includes both luggage filters", luggage.SetEquals(new[] { ThingLuggageType.Beach, ThingLuggageType.Cursed }));
        check("explicit placed scene targets survive without item-based placement", !placed && scenes.Contains(ThingSceneTargetType.RopePlaced));
        var placedPreset = new ThingPresetDefinition(ThingPresetFactory.PlayerPlacedPresetId, "Placed");
        check("built-in placed preset enables item-based placement",
            ThingPresetSelection.Combine(new[] { a, placedPreset }, items, luggage, scenes));
        check("deselecting placed preset closes the item-based gate",
            !ThingPresetSelection.Combine(new[] { b }, items, luggage, scenes) && items.SetEquals(new ushort[] { 2, 3 }));

        var originalDatabase = SingletonAsset<ItemDatabase>.Instance;
        var mushrooms = new List<Item>();
        ushort id = 100;
        foreach (string type in new[] { "Cluster", "Lace", "Normie" })
        foreach (bool poison in new[] { false, true })
        {
            string prefab = "Mushroom " + type + (poison ? " Poison" : "");
            Item item = make(id++, type + " Fungus", prefab);
            mushrooms.Add(item);
            bool foundPoison; string variant;
            check("classifies " + prefab, ThingItemTraits.TryGetMushroomVariant(item, out foundPoison, out variant) && foundPoison == poison && variant == prefab);
            string english = ThingCatalog.GetDisplayName(item, ThingNameLanguage.English);
            check("English variety: " + prefab, english.EndsWith(poison ? " (Poisonous variety)" : " (Non-poisonous variety)"));
            check("Chinese variety: " + prefab, ThingCatalog.GetDisplayName(item, ThingNameLanguage.SimplifiedChinese).EndsWith(poison ? "（有毒品种）" : "（无毒品种）"));
            item.gameObject.name += " (Clone)";
            check("clone keeps identity: " + prefab, ThingItemTraits.TryGetMushroomVariant(item, out foundPoison, out variant) && foundPoison == poison && variant == prefab);
        }
        SingletonAsset<ItemDatabase>.Instance = new ItemDatabase();
        foreach (Item item in mushrooms) SingletonAsset<ItemDatabase>.Instance.itemLookup[item.itemID] = item;
        var clone = make(id++, "Cluster Fungus", "Mushroom Cluster");
        SingletonAsset<ItemDatabase>.Instance.itemLookup[clone.itemID] = clone;
        var definitions = ThingCatalog.Load().Where(value => value.Prefab != null).ToList();
        check("six mushroom varieties remain separate with identical names", definitions.Count == 6);
        var normal = definitions.Single(value => value.ItemIds.Contains((ushort)100));
        check("clone and prefab group together without poisonous sibling", normal.ItemIds.Contains(clone.itemID) && !normal.ItemIds.Contains((ushort)101));
        check("selecting non-poisonous mushroom does not select poisonous sibling",
            !definitions.Single(value => value.ItemIds.Contains((ushort)101)).IsSelected(new HashSet<ushort>(normal.ItemIds), new HashSet<ThingLuggageType>(), new HashSet<ThingSceneTargetType>()));
        SingletonAsset<ItemDatabase>.Instance = originalDatabase;
        Item unknown = make(200, "Shroomberry", "Shroomberry Red");
        bool unknownPoison; string unknownVariant;
        check("unknown mushroom is not assumed safe", !ThingItemTraits.TryGetMushroomVariant(unknown, out unknownPoison, out unknownVariant) && ThingCatalog.GetDisplayName(unknown, ThingNameLanguage.English) == "Shroomberry");
        check("null mushroom is ignored", !ThingItemTraits.TryGetMushroomVariant(null, out unknownPoison, out unknownVariant));
        LocalizedText.CURRENT_LANGUAGE = LocalizedText.Language.SimplifiedChinese;
        check("Game names use Chinese variety suffix", ThingCatalog.GetDisplayName(mushrooms[0], ThingNameLanguage.Game).EndsWith("（无毒品种）"));
        LocalizedText.CURRENT_LANGUAGE = LocalizedText.Language.English;
        check("English toggle labels describe available action", ThingUi.Enable(true) == "Disable" && ThingUi.Enable(false) == "Enable");

        check("ordinary ground item is reusable", ThingItemTraits.IsReusableGroundItem(unknown));
        unknown.blockInteraction = true;
        check("blocked item is filtered", !ThingItemTraits.IsReusableGroundItem(unknown));
        unknown.blockInteraction = false; unknown.gameObject.Components.Add(new Flare());
        unknown.UIData.canPocket = false; unknown.UIData.canBackpack = false;
        check("spent non-storable flare is filtered", !ThingItemTraits.IsReusableGroundItem(unknown));
        unknown.UIData.canPocket = true;
        check("pocketable flare remains", ThingItemTraits.IsReusableGroundItem(unknown));
        unknown.UIData.canPocket = false; unknown.UIData.canBackpack = true;
        check("backpackable flare remains", ThingItemTraits.IsReusableGroundItem(unknown));
        check("null ground item is ignored", !ThingItemTraits.IsReusableGroundItem(null));
        Vector3 camera = new Vector3(5, 2, -3);
        check("distance limit includes exact boundary", ThingLabelRange.Within(camera + new Vector3(3, 4, 0), camera, 5));
        check("distance limit excludes farther target", !ThingLabelRange.Within(camera + new Vector3(5.01f, 0, 0), camera, 5));
        check("ten-meter buffer retains hidden target", ThingLabelRange.Within(camera + new Vector3(15, 0, 0), camera, 5, 10));
        check("outside buffer removes target", !ThingLabelRange.Within(camera + new Vector3(15.01f, 0, 0), camera, 5, 10));
        check("unlimited distance stays unlimited", ThingLabelRange.Within(new Vector3(10000, 10000, 10000), camera, 0));
    }
}
