using System;
using System.Collections.Generic;
using System.Linq;

// Minimal test doubles: no game assemblies or native Unity calls are loaded.
namespace UnityEngine
{
    public struct Vector3
    {
        public float x,y,z;
        public Vector3(float x,float y,float z) { this.x=x;this.y=y;this.z=z; }
        public float sqrMagnitude => x*x+y*y+z*z;
        public static Vector3 operator -(Vector3 a,Vector3 b) => new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);
        public static Vector3 operator +(Vector3 a,Vector3 b) => new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
        public static Vector3 operator *(Vector3 a,float b) => new Vector3(a.x*b,a.y*b,a.z*b);
        public static Vector3 up=>new Vector3(0,1,0);public static Vector3 down=>new Vector3(0,-1,0);
    }
    public class Component
    {
        public GameObject gameObject;
        public Transform transform => gameObject.transform;
        public bool isActiveAndEnabled = true;
        public Component() { gameObject=new GameObject(); gameObject.Components.Add(this); }
        protected Component(GameObject obj) { gameObject=obj; }
        public T GetComponent<T>() where T:class => gameObject.Components.OfType<T>().FirstOrDefault();
        public T GetComponentInParent<T>(bool inactive=false) where T:class
        {
            for(Transform t=transform;t!=null;t=t.parent) { var value=t.GetComponent<T>();if(value!=null)return value; }
            return null;
        }
        public T GetComponentInChildren<T>(bool inactive=false) where T:class => GetComponent<T>();
        public int GetInstanceID() => GetHashCode();
    }
    public class GameObject
    {
        public string name=""; public bool activeInHierarchy=true; public string tag="";
        public Transform transform; public Scene scene=new Scene();
        public List<Component> Components=new List<Component>();
        public GameObject() { transform=new Transform(this);Components.Add(transform); }
        public bool CompareTag(string value)=>tag==value;
    }
    public class Scene { public bool IsValid()=>true; }
    public class Transform:Component
    {
        public Transform parent; public Vector3 position;
        public Transform(GameObject obj):base(obj) {}
        public bool IsChildOf(Transform root) { for(var t=parent;t!=null;t=t.parent)if(t==root)return true;return false; }
    }
    public static class Time { public static float unscaledTime; }
    public enum FindObjectsSortMode { None }
    public struct RaycastHit { public Transform transform; public float distance; }
    public enum QueryTriggerInteraction { Ignore }
    public static class Physics
    {
        public const int DefaultRaycastLayers=-5; public static RaycastHit[] Hits=Array.Empty<RaycastHit>();
        public static int RaycastNonAlloc(Vector3 origin,Vector3 direction,RaycastHit[] results,float distance,int mask,QueryTriggerInteraction query)
        { Array.Copy(Hits,results,Hits.Length);return Hits.Length; }
    }
}
namespace Zorro.Core
{
    public static class Singleton<T> { public static T Instance; }
    public static class SingletonAsset<T> { public static T Instance; }
}
public class UIData { public string itemName; public bool canPocket=true,canBackpack=true; }
public class Flare:UnityEngine.Component {}
public enum ItemState { Ground,Held }
public class Item:UnityEngine.Component
{
    [Flags] public enum ItemTags { ScoutAmulet=1,GoldenIdol=2,BingBong=4,Mystical=8,PackagedFood=16,Berry=32,Mushroom=64,GourmandRequirement=128,Bird=256 }
    public static List<Item> ALL_ITEMS=new List<Item>();
    public ushort itemID; public Item isSecretlyOtherItemPrefab; public UIData UIData=new UIData();
    public ItemTags itemTags; public ItemState itemState; public string GameNameOverride; public bool blockInteraction;
    public string GetName()=>GameNameOverride??LocalizedText.GetText(LocalizedText.GetNameIndex(UIData.itemName),LocalizedText.CURRENT_LANGUAGE);
}
public class FakeItem:UnityEngine.Component { public Item realItemPrefab; public bool pickedUp; }
public class OrbThatMakesYouSleepy:UnityEngine.Component { public FakeItem fakeBerry; }
public class ItemDatabase { public Dictionary<ushort,Item> itemLookup=new Dictionary<ushort,Item>(); }
[Flags] public enum SpawnPool { LuggageBeach=1,LuggageJungle=2,LuggageTundra=4,LuggageCaldera=8,LuggageClimber=16,LuggageAncient=32,LuggageMesa=64,LuggageRoots=128,LuggageGloom=256,LuggageCitadel=512,LuggageClown=1024 }
public class Spawner:UnityEngine.Component { public SpawnPool spawnPool; private SpawnPool GetSpawnPool()=>spawnPool; }
public class Luggage:Spawner { public string displayName="LUGGAGE";public bool IsOpen;public string GetName()=>"Luggage"; public UnityEngine.Vector3 Center()=>transform.position; }
public class RespawnChest:Luggage {}
public class LuggageCursed:Luggage {}
public class GhostFire:UnityEngine.Component { public bool isLit; public string displayNameIndex="FIRE";public string GetName()=>"Fire"; }
public class StatusField:UnityEngine.Component {}
namespace Peak { public class EarlyWorm:UnityEngine.Component {} public class TrickLuggage:UnityEngine.Component { public StatusField statusField; } }
public static class LocalizedText
{
    public enum Language { English,SimplifiedChinese,TraditionalChinese,French }
    public static Language CURRENT_LANGUAGE;
    public static Dictionary<string,string[]> Names=new Dictionary<string,string[]>();
    public static string GetNameIndex(string name)=>"NAME_"+name.ToUpperInvariant();
    public static string GetText(string name,Language language) => Names.TryGetValue(name,out var values)?values[(int)language]:"LOC: "+name;
}
public class Biome { public enum BiomeType { Shore,Tropics,Alpine,Volcano,Peak,Void } }
public class MapHandler
{
    public class MapSegment { public UnityEngine.GameObject segmentParent,segmentCampfire;public Biome.BiomeType biome; }
    public MapSegment[] segments; public int Current;
    public static MapSegment CurrentMapSegment => Zorro.Core.Singleton<MapHandler>.Instance.segments[Zorro.Core.Singleton<MapHandler>.Instance.Current];
}
public class MountainProgressHandler
{
    public class ProgressPoint { public UnityEngine.Transform transform; public Biome.BiomeType biome; }
    public ProgressPoint[] progressPoints;
}
public class Character:UnityEngine.Component { public static Character localCharacter;public UnityEngine.Vector3 Center=>transform.position; }
namespace WhereIsThing
{
    public class TestSetting<T> { public T Value; }
    public class TestLog { public void LogInfo(string text) {} public void LogWarning(string text) {} }
    public sealed partial class WhereIsThingPlugin
    {
        internal ThingLocationScope _effectiveScopes=ThingLocationScope.Ground;
        internal List<ThingTargetDefinition> _catalog=new List<ThingTargetDefinition>();
        internal HashSet<ushort> _selectedIds=new HashSet<ushort>();
        internal HashSet<ThingLuggageType> _selectedLuggageTypes=new HashSet<ThingLuggageType>();
        internal HashSet<ThingSceneTargetType> _selectedSceneTargetTypes=new HashSet<ThingSceneTargetType>();
        internal List<ThingPresetDefinition> _localPresets=new List<ThingPresetDefinition>();
        internal TestSetting<int> _pickupIdentityVersion=new TestSetting<int>();
        internal TestSetting<ThingNameLanguage> _nameLanguage=new TestSetting<ThingNameLanguage>();
        internal TestLog _log=new TestLog();
        internal HashSet<string> _scanSeenBuffer=new HashSet<string>();
        internal Dictionary<string,(UnityEngine.Transform target,Func<string> title,Func<bool> valid)> Labels=new();
        internal static List<FakeItem> FakeItems=new();
        internal static List<OrbThatMakesYouSleepy> NapberryPlants=new();
        private T[] FindObjectsByType<T>(UnityEngine.FindObjectsSortMode sort)=>FakeItems.Cast<UnityEngine.Component>().Concat(NapberryPlants).Concat(AuditSceneTargets).OfType<T>().ToArray();
        private void AddLabel(string key,UnityEngine.Transform target,Func<string> title,Func<bool> valid,Func<UnityEngine.Vector3> position=null)
        { if(!Labels.ContainsKey(key))Labels[key]=(target,title,valid); }
        internal void Plants() { RefreshRootPlantItems();RefreshRootPlantFakeItems(); }
        internal void Napberries()=>RefreshNapberryPlants();
        internal void Migrate()=>MigratePickupSelections();
        internal void Chest(Luggage luggage)=>RefreshLuggageLabel(luggage);
    }
}
