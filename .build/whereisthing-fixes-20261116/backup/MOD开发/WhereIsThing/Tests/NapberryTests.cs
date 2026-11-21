using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WhereIsThing;
using Zorro.Core;

internal static class NapberryTests
{
    internal static void Run(Action<string, bool> check, Func<ushort, string, string, Item> make)
    {
        var previousDatabase = SingletonAsset<ItemDatabase>.Instance;
        var previousLanguage = LocalizedText.CURRENT_LANGUAGE;
        Item napberry = make(300, "Napberry", "Napberry");
        Item variant = make(301, "Napberry", "Napberry Variant");
        Item other = make(302, "Other berry", "Other Berry");
        napberry.itemTags = variant.itemTags = Item.ItemTags.Berry;
        LocalizedText.Names[LocalizedText.GetNameIndex("Napberry")] = new[] { "Napberry", "晚安莓", "晚安莓", "Baie du sommeil" };
        LocalizedText.CURRENT_LANGUAGE = LocalizedText.Language.English;
        SingletonAsset<ItemDatabase>.Instance = new ItemDatabase();
        foreach (var item in new[] { napberry, variant, other })
            SingletonAsset<ItemDatabase>.Instance.itemLookup[item.itemID] = item;
        var catalog = ThingCatalog.Load();
        var entries = catalog.Where(entry => entry.Prefabs.Any(ThingNapberry.IsNapberry) ||
            (entry.IsSceneTarget && entry.SceneTargetType == ThingSceneTargetType.NapberryHypnoOrb)).ToList();
        check("Napberry has exactly one catalog entry", entries.Count == 1);
        var target = entries.Single();
        check("unified entry retains all actual item IDs", target.ItemIds.OrderBy(id => id).SequenceEqual(new ushort[] { 300, 301 }));
        check("unified Napberry appears in Food", target.Category == "Food");
        check("unified English title has no unpicked prefix", target.GetDisplayName(ThingNameLanguage.English) == "Napberry");
        check("unified Chinese title matches the item", target.GetDisplayName(ThingNameLanguage.SimplifiedChinese) == "晚安莓");
        LocalizedText.CURRENT_LANGUAGE = LocalizedText.Language.French;
        check("unified title follows the official game translation", target.GetDisplayName(ThingNameLanguage.Game) == "Baie du sommeil");
        check("explicit English remains independent of game language", target.GetDisplayName(ThingNameLanguage.English) == "Napberry");
        LocalizedText.CURRENT_LANGUAGE = LocalizedText.Language.English;

        var items = new HashSet<ushort> { napberry.itemID };
        var scenes = new HashSet<ThingSceneTargetType>();
        var luggage = new HashSet<ThingLuggageType>();
        check("legacy item-only selection checks unified row", target.IsSelected(items, luggage, scenes));
        ThingNapberry.ExpandSelection(catalog, items, scenes);
        check("legacy item-only selection enables plant discovery", scenes.SetEquals(new[] { ThingSceneTargetType.NapberryHypnoOrb }) && items.SetEquals(target.ItemIds));
        target.SetSelected(items, luggage, scenes, false);
        ThingNapberry.ExpandSelection(catalog, items, scenes);
        check("deselection clears both forms without reactivation", items.Count == 0 && scenes.Count == 0);
        scenes.Add(ThingSceneTargetType.NapberryHypnoOrb);
        check("legacy scene-only selection checks unified row", target.IsSelected(items, luggage, scenes));
        ThingNapberry.ExpandSelection(catalog, items, scenes);
        check("legacy scene-only selection enables item and inventory detection", items.SetEquals(target.ItemIds));
        target.SetSelected(items, luggage, scenes, false);
        target.SetSelected(items, luggage, scenes, true);
        check("one toggle selects both forms", scenes.Contains(ThingSceneTargetType.NapberryHypnoOrb) && items.SetEquals(target.ItemIds));
        items.Add(other.itemID); scenes.Add(ThingSceneTargetType.Spider);
        target.SetSelected(items, luggage, scenes, false);
        ThingNapberry.ExpandSelection(catalog, items, scenes);
        check("unrelated targets survive Napberry deselection", items.SetEquals(new[] { other.itemID }) && scenes.SetEquals(new[] { ThingSceneTargetType.Spider }));

        foreach (bool legacyScene in new[] { false, true })
        {
            var preset = new ThingPresetDefinition("legacy", "Legacy");
            if (legacyScene) preset.SelectedSceneTargetTypes.Add(ThingSceneTargetType.NapberryHypnoOrb);
            else preset.SelectedItemIds.Add(napberry.itemID);
            string serialized = ThingPresetCodec.SerializePresets(new[] { preset });
            var decoded = ThingPresetCodec.DeserializePresets(serialized).Single();
            ThingPresetSelection.Combine(new[] { decoded }, items, luggage, scenes);
            ThingNapberry.ExpandSelection(catalog, items, scenes);
            check("old saved/shared preset expands both forms: " + legacyScene, items.SetEquals(target.ItemIds) && scenes.Contains(ThingSceneTargetType.NapberryHypnoOrb));
            check("effective expansion preserves original shared payload: " + legacyScene, ThingPresetCodec.SerializePresets(new[] { decoded }) == serialized);
            check("legacy preset summary shows one Napberry: " + legacyScene, ThingPresetFactory.BuildSummary(decoded, catalog, ThingNameLanguage.English) == "Napberry");
        }
        ThingPresetSelection.Combine(Array.Empty<ThingPresetDefinition>(), items, luggage, scenes);
        ThingNapberry.ExpandSelection(catalog, items, scenes);
        check("switching presets removes derived Napberry selection", items.Count == 0 && scenes.Count == 0);

        var fruit = new FakeItem { realItemPrefab = napberry };
        var plant = new OrbThatMakesYouSleepy { fakeBerry = fruit };
        fruit.transform.position = new Vector3(1, 2, 3);
        plant.transform.position = new Vector3(100, 200, 300);
        WhereIsThingPlugin.NapberryPlants.Add(plant);
        var plugin = new WhereIsThingPlugin();
        plugin._selectedSceneTargetTypes.Add(ThingSceneTargetType.NapberryHypnoOrb);
        plugin.Napberries();
        string key = "plant:" + fruit.GetInstanceID();
        check("unpicked fruit receives one label", plugin.Labels.Count == 1 && plugin.Labels.ContainsKey(key));
        var label = plugin.Labels[key];
        check("label is anchored to fruit instead of persistent root", label.target == fruit.transform && label.target != plant.transform);
        check("plant label matches picked item name", label.title() == ThingCatalog.GetDisplayName(napberry, ThingNameLanguage.Game));
        plugin.Napberries();
        check("rediscovery does not duplicate the fruit label", plugin.Labels.Count == 1);
        check("available fruit is valid", label.valid());
        fruit.gameObject.activeInHierarchy = false;
        check("local pickup hides label even before pickedUp synchronization", !label.valid() && plant.gameObject.activeInHierarchy);
        fruit.gameObject.activeInHierarchy = true;
        check("denied pickup restores label eligibility", label.valid());
        fruit.pickedUp = true;
        check("pickedUp invalidates label even if fruit remains active", !label.valid());
        var afterPickup = new WhereIsThingPlugin();
        afterPickup._selectedSceneTargetTypes.Add(ThingSceneTargetType.NapberryHypnoOrb); afterPickup.Napberries();
        check("picked fruit is excluded from new scans", afterPickup.Labels.Count == 0);
        fruit.gameObject.activeInHierarchy = false;
        check("late-join hidden and picked state cannot label empty root", !label.valid());
        fruit.pickedUp = false; fruit.gameObject.activeInHierarchy = true;
        plugin._effectiveScopes = ThingLocationScope.Held;
        check("Ground scope controls unpicked fruit", !label.valid());
        var heldOnly = new WhereIsThingPlugin { _effectiveScopes = ThingLocationScope.Held };
        heldOnly._selectedSceneTargetTypes.Add(ThingSceneTargetType.NapberryHypnoOrb); heldOnly.Napberries();
        check("Held-only scan creates no plant label", heldOnly.Labels.Count == 0);
        plugin._effectiveScopes = ThingLocationScope.Ground;
        plugin._selectedSceneTargetTypes.Clear();
        check("deselecting Napberry invalidates existing label", !label.valid());
        plugin._selectedSceneTargetTypes.Add(ThingSceneTargetType.NapberryHypnoOrb);
        plant.gameObject.activeInHierarchy = false;
        check("inactive plant invalidates fruit label", !label.valid());
        plant.gameObject.activeInHierarchy = true;
        plant.fakeBerry = new FakeItem { realItemPrefab = napberry };
        check("replacement fruit invalidates captured old label", !label.valid());
        plant.fakeBerry = null;
        var empty = new WhereIsThingPlugin();
        empty._selectedSceneTargetTypes.Add(ThingSceneTargetType.NapberryHypnoOrb); empty.Napberries();
        check("missing fruit is ignored without exception", empty.Labels.Count == 0);
        plant.fakeBerry = fruit; fruit.realItemPrefab = other;
        check("other fruit identity is not mislabeled Napberry", !label.valid());
        fruit.realItemPrefab = napberry; plant.isActiveAndEnabled = false;
        check("disabled hypnosis does not hide an available fruit", label.valid());
        check("null fruit and identity are rejected", !ThingNapberry.IsAvailable(null) && !ThingNapberry.IsNapberry(null));
        SingletonAsset<ItemDatabase>.Instance = new ItemDatabase();
        var fallback = ThingCatalog.Load().Single(entry => entry.IsSceneTarget && entry.SceneTargetType == ThingSceneTargetType.NapberryHypnoOrb);
        check("missing item asset keeps a named legacy fallback", fallback.GetDisplayName(ThingNameLanguage.English) == "Napberry" && fallback.ItemIds.Count == 0);
        WhereIsThingPlugin.NapberryPlants.Clear();
        SingletonAsset<ItemDatabase>.Instance = previousDatabase;
        LocalizedText.CURRENT_LANGUAGE = previousLanguage;
    }
}
