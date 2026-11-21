using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Zorro.Core;
using WhereIsThing;
using Photon.Pun;

namespace UnityEngine
{
    public static class Mathf
    {
        public static float Clamp(float v, float low, float high) => Math.Min(Math.Max(v,low),high);
        public static float Max(float a,float b)=>Math.Max(a,b);
    }
}
namespace Photon.Realtime
{
    public class Player { public int ActorNumber; }
    public class Room
    {
        public string Name;
        public Dictionary<string,object> CustomProperties = new();
        public int PublishCalls;
        public void SetCustomProperties(Dictionary<string,object> values)
        { PublishCalls++;foreach(var entry in values)CustomProperties[entry.Key]=entry.Value; }
    }
}
namespace Photon.Pun
{
    public static class PhotonNetwork
    {
        public static bool InRoom=true, OfflineMode=false, IsMasterClient=true;
        public static Photon.Realtime.Room CurrentRoom;
        public static Photon.Realtime.Player LocalPlayer=new(){ActorNumber=1},MasterClient=LocalPlayer;
    }
}
namespace WhereIsThing
{
    public class AuditWindow
    {
        public bool IsOpen;
        public void Close()=>IsOpen=false;
    }
    public class AuditHazard:Component { public bool HazardEnabled=true; }
    public sealed partial class WhereIsThingPlugin
    {
        private const int ShareProtocolVersion=2;
        private const string ShareModePropertyKey="WIT.ShareMode",ShareProtocolPropertyKey="WIT.Protocol",
            ShareActorPropertyKey="WIT.Actor",ShareHashPropertyKey="WIT.Hash",SharePayloadPropertyKey="WIT.Payload";
        private bool _shareDirty=true,_sessionValid,_labelPlacedFromItemSelection,_displayActive=true,_lastObservedClientRestriction;
        private string _lastPublishedSharePayload="",_lastPublishedShareHash="",_activeRoomName="",_editingPresetId="";
        private float _lastPublishAt=-10,_sessionStaleUntil=-1,_hideAt;
        private int _sessionSourceActor=-1;
        private ThingPresetShareMode _lastObservedShareMode=ThingPresetShareMode.PublishedPresets;
        private List<ThingPresetDefinition> _sessionPresets=new(),_builtInPresets=new();
        private List<string> _activeLocalPresetIds=new(){"test"};
        private string PrimaryLocalPresetId=>_activeLocalPresetIds.FirstOrDefault()??"";
        private string _selectedSharedPresetId="";
        private Dictionary<int,string> _ownerNamesByActor=new();
        private TestSetting<bool> _restrictClientPresets=new(){Value=true};
        private TestSetting<ThingPresetShareMode> _shareModeConfig=new(){Value=ThingPresetShareMode.PublishedPresets};
        private TestSetting<ThingLocationScope> _locationScopes=new(){Value=ThingLocationScope.Ground};
        private TestSetting<ThingScanMode> _scanMode=new(){Value=ThingScanMode.Timed};
        private TestSetting<float> _displayDuration=new(){Value=8};
        private TestSetting<string> _selectedSharedPresetIdConfig=new(),_selectedItemIds=new(),_selectedSceneTargetTypesConfig=new();
        private ThingScanMode _effectiveScanMode;
        private float _effectiveDisplayDuration;
        private AuditWindow _window=new(),_pickerWindow=new();
        private int _refreshCount,_pickerCount;
        internal static List<Component> AuditSceneTargets=new();
        private void RefreshLabels()=>_refreshCount++;
        private void OpenPresetPicker()=>_pickerCount++;
        private void PersistConfig(){}
        private void BuildCatalogIndexes(){}
        private void MigratePlacedSceneTargetsToItems(){}
        private void SaveLocalPresets(){}
        private static string SerializeSceneTargetTypes(IEnumerable<ThingSceneTargetType> values)=>string.Join(",",values);
        private bool IsWithinLabelRange(Transform target)=>true;

        internal static void RunAudit(Action<string,bool,string> observed)
        {
            Time.unscaledTime=100;
            PhotonNetwork.IsMasterClient=true;PhotonNetwork.InRoom=true;PhotonNetwork.OfflineMode=false;
            PhotonNetwork.CurrentRoom=new(){Name="room-A"};
            var host=new WhereIsThingPlugin();
            host._localPresets.Add(new ThingPresetDefinition("test","Test"){Published=true});
            host.UpdateRoomState();
            observed("initial publish control",PhotonNetwork.CurrentRoom.PublishCalls==1,"Initial publication succeeds.");
            PhotonNetwork.CurrentRoom=new(){Name="room-B"};
            Time.unscaledTime++;
            host.UpdateRoomState();
            observed("new room suppresses unchanged payload",PhotonNetwork.CurrentRoom.PublishCalls==0 && !host._shareDirty,
                "No WIT properties are written in the new room; dirty flag is cleared.");

            var modeHost=new WhereIsThingPlugin();
            PhotonNetwork.CurrentRoom=new(){Name="mode-room"};
            modeHost._shareModeConfig.Value=ThingPresetShareMode.BuiltInOnly;
            modeHost.PublishShareStateIfNeeded();
            modeHost._shareModeConfig.Value=ThingPresetShareMode.Off;
            modeHost.MarkShareDirty(true);modeHost.PublishShareStateIfNeeded();
            observed("Off/BuiltInOnly mode-only transition is lost",
                (int)PhotonNetwork.CurrentRoom.CustomProperties[ShareModePropertyKey]==(int)ThingPresetShareMode.BuiltInOnly,
                "WIT.ShareMode still equals BuiltInOnly after setting Off.");

            var startup=new WhereIsThingPlugin();
            startup._localPresets.Add(ThingPresetFactory.CreateSurvivalPreset());
            PhotonNetwork.CurrentRoom=new(){Name="startup-room"};
            startup.PublishShareStateIfNeeded();
            var root=new Item{itemID=501,UIData=new UIData{itemName="Medicinal Root"}};root.gameObject.name="MedicinalRoot";
            var db=new ItemDatabase();db.itemLookup[root.itemID]=root;SingletonAsset<ItemDatabase>.Instance=db;
            startup.TryLoadCatalog();
            Time.unscaledTime++;startup.PublishShareStateIfNeeded();
            var published=ThingPresetCodec.DeserializePresets((string)PhotonNetwork.CurrentRoom.CustomProperties[SharePayloadPropertyKey]).Single();
            observed("catalog resolution is not republished",startup._localPresets[0].SelectedItemIds.Contains(root.itemID) && published.SelectedItemIds.Count==0,
                "Local Survival now has a root target but published Survival remains empty.");

            PhotonNetwork.IsMasterClient=false;
            PhotonNetwork.CurrentRoom=new(){Name="client-room"};
            var client=new WhereIsThingPlugin();client._hideAt=108;client._pickerWindow.IsOpen=true;
            Time.unscaledTime=105;
            client.OnRoomPropertiesUpdate(new Dictionary<string,object>{{"UnrelatedGameKey",123}});
            observed("unrelated room property extends timed scan",client._hideAt==113 && client._refreshCount==1,
                "Expiry changes from 108 to 113 with no WhereIsThing property change.");
            observed("unrelated room property rebuilds open picker",client._pickerCount==1,
                "The open picker is refreshed even for an unrelated room key.");

            client._scanMode.Value=ThingScanMode.Persistent;client.RefreshEffectivePresetState(true);
            client._scanMode.Value=ThingScanMode.Timed;client.RefreshEffectivePresetState(false);
            observed("external persistent-to-timed toggle retains infinite expiry",float.IsPositiveInfinity(client._hideAt) && client._effectiveScanMode==ThingScanMode.Timed,
                "Periodic state refresh updates mode but leaves the existing infinite expiry unchanged.");

            var hazardPlugin=new WhereIsThingPlugin();
            hazardPlugin._selectedSceneTargetTypes.Add(ThingSceneTargetType.SlipperyJellyfish);
            var hazard=new AuditHazard();AuditSceneTargets.Add(hazard);
            hazardPlugin.RefreshActiveSceneTargets<AuditHazard>(new HashSet<string>(),ThingSceneTargetType.SlipperyJellyfish,x=>x.HazardEnabled);
            var label=hazardPlugin.Labels.Single().Value;hazard.HazardEnabled=false;
            hazardPlugin.RefreshActiveSceneTargets<AuditHazard>(new HashSet<string>(),ThingSceneTargetType.SlipperyJellyfish,x=>x.HazardEnabled);
            observed("scene additional filter is missing from existing-label validity",label.valid(),
                "Rescan skips the disabled hazard but its previous label remains valid.");
            AuditSceneTargets.Clear();
        }
    }
}
internal static class AuditProgram
{
    static void Main()
    {
        int count=0;
        WhereIsThingPlugin.RunAudit((name,confirmed,detail)=>
        {
            if(!confirmed)throw new Exception("NOT REPRODUCED: "+name);
            count++;Console.WriteLine("CONFIRMED: "+name+" | "+detail);
        });
        Console.WriteLine("Confirmed "+count+" audit observations using extracted production method bodies; not an in-game test.");
    }
}
