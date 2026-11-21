using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WhereIsThing;
using Zorro.Core;

internal static class Program
{
    private static int _passed;
    private static Item Root, Mandrake, Hidden, Bandage, Kit, Remedy;
    private static List<ThingTargetDefinition> Catalog;
    private static void Check(string name, bool condition)
    {
        if (!condition) throw new Exception("FAIL: " + name);
        _passed++;Console.WriteLine("PASS: "+name);
    }
    private static Item Make(ushort id,string name,string prefab)
    {
        var item=new Item {itemID=id,UIData=new UIData {itemName=name}};item.gameObject.name=prefab;
        LocalizedText.Names[LocalizedText.GetNameIndex(name)]=new[]{name,"中文"+name,"繁體"+name,"FR "+name};
        return item;
    }
    public static void Main()
    {
        Root=Make(1,"Medicinal Root","MedicinalRoot");
        Mandrake=Make(2,"Mandrake","Mandrake");
        Hidden=Make(3,"Medicinal Root","Mandrake_Hidden");Hidden.isSecretlyOtherItemPrefab=Mandrake;
        Bandage=Make(4,"Bandages","Bandages");Kit=Make(5,"First Aid Kit","Kit");Remedy=Make(6,"Remedy Fungus","Remedy");
        SingletonAsset<ItemDatabase>.Instance=new ItemDatabase();
        foreach(var item in new[]{Root,Mandrake,Hidden,Bandage,Kit,Remedy}) SingletonAsset<ItemDatabase>.Instance.itemLookup[item.itemID]=item;
        Catalog=ThingCatalog.Load();
        IdentityAndPlants();MigrationAndCodec();Chests();Languages();Segments();
        PresetAndTraitTests.Run(Check, Make);
        NapberryTests.Run(Check, Make);
        Console.WriteLine($"All {_passed} regression checks passed. Unity rendering, physics and networking require in-game validation.");
    }
    private static void IdentityAndPlants()
    {
        Check("hidden mandrake resolves actual pickup",ThingPickupIdentity.Resolve(Hidden)==Mandrake);
        Check("medicinal root retains own identity",ThingPickupIdentity.Resolve(Root)==Root);
        Check("one-step redirect matches game pickup",ThingPickupIdentity.Resolve(new FakeItem{realItemPrefab=Hidden})==Hidden);
        var root=Catalog.Single(x=>x.Prefab==Root);var mandrake=Catalog.Single(x=>x.Prefab==Mandrake);
        Check("catalog separates root and hidden mandrake",root.ItemIds.SequenceEqual(new ushort[]{1}) && mandrake.ItemIds.SequenceEqual(new ushort[]{2}));
        Check("carrier remains available for migration",mandrake.Prefabs.Contains(Hidden));
        Check("old host hidden-ID selection remains visible in summaries",mandrake.IsSelected(new HashSet<ushort>{3},new HashSet<ThingLuggageType>(),new HashSet<ThingSceneTargetType>()));
        var p=new WhereIsThingPlugin();p._catalog=Catalog;p._selectedIds.Add(2);
        Item.ALL_ITEMS.Add(Hidden);Item.ALL_ITEMS.Add(Root);
        p.Plants();Check("unpicked Item label uses carrier transform",p.Labels.Count==1 && p.Labels.Values.First().target==Hidden.transform);
        Check("unpicked Item label names real mandrake",p.Labels.Values.First().title()=="Mandrake");
        Hidden.itemState=ItemState.Held;Check("pickup invalidates ground label",!p.Labels.Values.First().valid());Hidden.itemState=ItemState.Ground;
        var fake=new FakeItem{realItemPrefab=Root};WhereIsThingPlugin.FakeItems.Add(fake);
        p._selectedIds.Add(1);p.Plants();Check("selected FakeItem plant is found",p.Labels.ContainsKey("plant:"+fake.GetInstanceID()));
        var label=p.Labels["plant:"+fake.GetInstanceID()];fake.pickedUp=true;Check("picked FakeItem label expires",!label.valid());fake.pickedUp=false;
        p._effectiveScopes=ThingLocationScope.Held;Check("Ground scope controls terrain plants",!label.valid());
        p._effectiveScopes=ThingLocationScope.Ground;p._selectedIds.Remove(1);Check("deselection expires plant label",!label.valid());
        p.Plants();Check("rediscovery does not duplicate plant entities",p.Labels.Count==3);
    }
    private static void MigrationAndCodec()
    {
        var p=new WhereIsThingPlugin();p._catalog=Catalog;
        var survival=ThingPresetFactory.CreateSurvivalPreset();survival.SelectedItemIds.UnionWith(new ushort[]{1,3,4,5,6});
        var custom=new ThingPresetDefinition("custom","自定义|test");custom.SelectedItemIds.UnionWith(new ushort[]{1,3});
        custom.SelectedLuggageTypes.UnionWith(new[]{ThingLuggageType.Jungle,ThingLuggageType.Clown});
        custom.SelectedSceneTargetTypes.Add(ThingSceneTargetType.TrapChest);
        p._localPresets.AddRange(new[]{survival,custom});p.Migrate();
        Check("untouched survival migration excludes disguised mandrake",survival.SelectedItemIds.SetEquals(new ushort[]{1,4,5,6}));
        Check("custom merged root selection preserves both real pickups",custom.SelectedItemIds.SetEquals(new ushort[]{1,2}));
        custom.SelectedItemIds.Remove(1);p.Migrate();Check("migration runs once and preserves later edits",custom.SelectedItemIds.SetEquals(new ushort[]{2}));
        var encoded=ThingPresetCodec.SerializePresets(p._localPresets);var decoded=ThingPresetCodec.DeserializePresets(encoded);
        Check("old preset wire format roundtrips special characters",decoded[1].Name==custom.Name && decoded[1].SelectedItemIds.SetEquals(custom.SelectedItemIds));
        Check("legacy luggage and trap tokens survive roundtrip",decoded[1].SelectedLuggageTypes.SetEquals(custom.SelectedLuggageTypes) && decoded[1].SelectedSceneTargetTypes.Contains(ThingSceneTargetType.TrapChest));
        var edited=new WhereIsThingPlugin();edited._catalog=Catalog;var editedSurvival=ThingPresetFactory.CreateSurvivalPreset();editedSurvival.SelectedItemIds.UnionWith(new ushort[]{3,5});edited._localPresets.Add(editedSurvival);edited.Migrate();
        Check("edited survival selection retains user intent",editedSurvival.SelectedItemIds.SetEquals(new ushort[]{2,5}));
    }
    private static void Chests()
    {
        var group=ThingTargetDefinition.CreateOrdinaryLuggage();var ids=new HashSet<ushort>();var types=new HashSet<ThingLuggageType>{ThingLuggageType.Jungle};var scenes=new HashSet<ThingSceneTargetType>();
        Check("ordinary group preserves partial region selection",group.IsSelected(ids,types,scenes)&&group.SelectedLuggageCount(types)==1&&group.LuggageMemberCount==10);
        group.SetSelected(ids,types,scenes,true);Check("ordinary group selects all ten legacy pools",types.Count==10&&!types.Contains(ThingLuggageType.Ancient)&&!types.Contains(ThingLuggageType.RespawnChest));
        group.SetSelected(ids,types,scenes,false);Check("ordinary group clear affects only its pools",types.Count==0);
        var p=new WhereIsThingPlugin();p._selectedLuggageTypes.Add(ThingLuggageType.Beach);p._selectedSceneTargetTypes.Add(ThingSceneTargetType.TrapChest);
        var luggage=new Luggage {spawnPool=SpawnPool.LuggageBeach};var trap=new Peak.TrickLuggage{statusField=new StatusField()};luggage.gameObject.Components.Add(trap);
        p.Chest(luggage);p.Chest(luggage);Check("chest and trap selections share a single label",p.Labels.Count==1);
        var label=p.Labels.Values.Single();Check("trap label overrides ordinary name",label.title()=="Trap Chest");
        luggage.IsOpen=true;Check("opened active trap retains hazard label",label.valid()&&label.title().Contains("Active hazard"));
        trap.statusField.isActiveAndEnabled=false;Check("expired opened trap label disappears",!label.valid());
        var normal=new Luggage{spawnPool=SpawnPool.LuggageBeach};p.Chest(normal);normal.IsOpen=true;Check("opened ordinary luggage expires",!p.Labels["luggage:"+normal.GetInstanceID()].valid());
        Check("revival statue is a landmark",ThingTargetDefinition.CreateLuggage(ThingLuggageType.RespawnChest).Category=="Landmarks");
        Check("saved luggage enum values remain stable",(int)ThingLuggageType.RespawnChest==0&&(int)ThingLuggageType.Other==13);
    }
    private static void Languages()
    {
        var variant=Make(20,"Medicinal Root","MedicinalRootVariant");var group=new ThingTargetDefinition(new[]{Root,variant});
        foreach(var game in Enum.GetValues<LocalizedText.Language>())
        {
            LocalizedText.CURRENT_LANGUAGE=game;bool zh=game==LocalizedText.Language.SimplifiedChinese||game==LocalizedText.Language.TraditionalChinese;
            Check("Game suffix and variant language: "+game,ThingCatalog.GetStatueSuffix(ThingNameLanguage.Game)==(zh?"雕像上":"On statue")&&group.GetVariantSuffix(ThingNameLanguage.Game).Contains(zh?"变体":"variants"));
            Check("explicit English ignores game language: "+game,ThingCatalog.GetDisplayName(Root,ThingNameLanguage.English)=="Medicinal Root"&&ThingCatalog.GetContainerSuffix(ThingNameLanguage.English)=="In backpack");
            Check("explicit Chinese ignores game language: "+game,ThingCatalog.GetDisplayName(Root,ThingNameLanguage.SimplifiedChinese)=="中文Medicinal Root"&&ThingCatalog.GetStatueSuffix(ThingNameLanguage.SimplifiedChinese)=="雕像上");
            Check("new scope UI follows game: "+game,ThingUi.MapScopeLabel(ThingMapScope.CurrentSegment).Contains(zh?"当前关卡":"Current segment"));
            Check("new chest text follows effective name language: "+game,ThingCatalog.GetOrdinaryLuggageName(ThingNameLanguage.Game)==(zh?"普通行李箱":"Ordinary Luggage"));
        }
        LocalizedText.CURRENT_LANGUAGE=LocalizedText.Language.SimplifiedChinese;Root.GameNameOverride="LOC: missing";
        Check("invalid Game name retries the selected language table",ThingCatalog.GetDisplayName(Root,ThingNameLanguage.Game)=="中文Medicinal Root");Root.GameNameOverride=null;
        var preset=new ThingPresetDefinition("test","test");preset.SelectedItemIds.Add(1);
        Check("preset target summary honors explicit language",ThingPresetFactory.BuildSummary(preset,Catalog,ThingNameLanguage.English).Contains("Medicinal Root")&&!ThingPresetFactory.BuildSummary(preset,Catalog,ThingNameLanguage.English).Contains("中文"));
        LocalizedText.CURRENT_LANGUAGE=LocalizedText.Language.English;
    }
    private static void Segments()
    {
        var map=new MapHandler{segments=new MapHandler.MapSegment[5],Current=1};
        var biomes=new[]{Biome.BiomeType.Shore,Biome.BiomeType.Tropics,Biome.BiomeType.Alpine,Biome.BiomeType.Volcano,Biome.BiomeType.Volcano};
        float[] starts={-364.2f,203.2f,627.7f,1297.9f,2011.1f};
        var progress=new MountainProgressHandler{progressPoints=new MountainProgressHandler.ProgressPoint[5]};
        for(int i=0;i<5;i++)
        {
            map.segments[i]=new MapHandler.MapSegment{biome=biomes[i],segmentParent=new GameObject(),segmentCampfire=new GameObject()};
            map.segments[i].segmentCampfire.transform.position=new Vector3(0,0,10000+i*100);
            progress.progressPoints[i]=new MountainProgressHandler.ProgressPoint{biome=biomes[i],transform=new GameObject().transform};
            progress.progressPoints[i].transform.position=new Vector3(0,0,starts[i]);
        }
        Singleton<MapHandler>.Instance=map;Singleton<MountainProgressHandler>.Instance=progress;Time.unscaledTime=1;
        var filter=new ThingSegmentFilter();filter.Refresh();
        var ground=new Item();ground.transform.position=new Vector3(0,0,300);
        Check("unparented current segment item appears",filter.Includes(ground.transform,ground.transform.position));
        ground.transform.position=new Vector3(0,0,0);Check("previous segment item filtered",!filter.Includes(ground.transform,ground.transform.position));
        ground.transform.position=new Vector3(0,0,800);Check("next segment item filtered",!filter.Includes(ground.transform,ground.transform.position));
        ground.transform.parent=map.segments[0].segmentParent.transform;ground.transform.position=new Vector3(0,0,300);
        Check("carried cross-segment item uses current position",filter.Includes(ground.transform,ground.transform.position));
        var scene=new GameObject().transform;scene.parent=map.segments[2].segmentParent.transform;scene.position=new Vector3(0,0,300);
        Check("static target uses actual segment hierarchy",!filter.Includes(scene,scene.position));
        scene.parent=map.segments[0].segmentCampfire.transform;Check("entry camp stays included",filter.Includes(scene,scene.position));
        scene.parent=map.segments[2].segmentCampfire.transform;Check("next camp excluded",!filter.Includes(scene,scene.position));
        map.Current=4;Time.unscaledTime++;filter.Refresh();ground.transform.parent=null;ground.transform.position=new Vector3(0,0,2200);
        Check("repeated volcano biome selects second progress boundary",filter.Includes(ground.transform,ground.transform.position));
        ground.transform.position=new Vector3(0,0,1500);Check("previous caldera excluded in kiln",!filter.Includes(ground.transform,ground.transform.position));
        Physics.Hits=new[]{new RaycastHit{transform=map.segments[4].segmentParent.transform,distance=2}};
        Check("supporting terrain handles overlap before Z fallback",filter.Includes(ground.transform,ground.transform.position));
        Physics.Hits=new[]{new RaycastHit{transform=map.segments[3].segmentParent.transform,distance=2}};ground.transform.position=new Vector3(0,0,2200);
        Check("supporting previous terrain remains excluded",!filter.Includes(ground.transform,ground.transform.position));Physics.Hits=Array.Empty<RaycastHit>();
        Check("boundary is half-open and does not overlap adjacent bands",!ThingSegmentFilter.InBand(627.7f,starts,1)&&ThingSegmentFilter.InBand(627.7f,starts,2));
        var extra=map.segments.ToList();extra.Add(new MapHandler.MapSegment{biome=Biome.BiomeType.Void,segmentParent=new GameObject()});map.segments=extra.ToArray();Time.unscaledTime++;filter.Refresh();
        Check("appending Nadir does not disable kiln spatial boundaries",filter.Includes(ground.transform,ground.transform.position));
        map.Current=5;Time.unscaledTime++;filter.Refresh();scene.parent=map.segments[5].segmentParent.transform;
        Check("Nadir static hierarchy works without a progress plane",filter.Includes(scene,scene.position));
        map.Current=99;Time.unscaledTime++;filter.Refresh();Check("invalid transition index fails closed without throwing",!filter.Includes(ground.transform,ground.transform.position));
        map.Current=4;Time.unscaledTime++;filter.Refresh();
        Singleton<MountainProgressHandler>.Instance=null;Character.localCharacter=new Character();Character.localCharacter.transform.position=ground.transform.position;Time.unscaledTime++;filter.Refresh();
        Check("custom map unknown target gets bounded nearby fallback",filter.Includes(ground.transform,ground.transform.position));
        ground.transform.parent=map.segments[0].segmentParent.transform;Check("custom map moved item ignores stale birthplace parent",filter.Includes(ground.transform,ground.transform.position));ground.transform.parent=null;
        ground.transform.position=new Vector3(0,0,2400);Check("custom map fallback cannot leak distant targets",!filter.Includes(ground.transform,ground.transform.position));
        Singleton<MapHandler>.Instance=null;Time.unscaledTime++;filter.Refresh();Check("airport without MapHandler remains usable",filter.Includes(ground.transform,ground.transform.position));
    }
}
