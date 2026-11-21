using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Photon.Pun;

namespace UnityEngine
{
    public static class Mathf
    {
        public static float Clamp(float v,float a,float b)=>Math.Min(Math.Max(v,a),b);
        public static float Max(float a,float b)=>Math.Max(a,b);
        public static float Round(float value)=>(float)Math.Round(value);
        public static bool Approximately(float a,float b)=>Math.Abs(a-b)<0.00001f;
    }
    public enum KeyCode { C, Escape, LeftAlt }
    public static class Input
    {
        public static HashSet<KeyCode> Down=new();
        public static bool GetKeyDown(KeyCode key)=>Down.Contains(key);
    }
    public class Camera:Component { public static Camera main; }
    public struct Vector2
    {
        public float x,y;
        public Vector2(float a,float b){x=a;y=b;}
        public static Vector2 zero=>new(0,0);
    }
}
namespace ExitGames.Client.Photon
{
    public class Hashtable:Dictionary<string,object>
    {
        public new object this[string key] { get=>TryGetValue(key,out var v)?v:null; set=>base[key]=value; }
    }
}
namespace Photon.Realtime
{
    public class Player { public int ActorNumber; }
    public class Room
    {
        public string Name;
        public ExitGames.Client.Photon.Hashtable CustomProperties=new();
        public int PublishCalls,Failures;
        public bool Deferred;
        private ExitGames.Client.Photon.Hashtable pending;
        public bool SetCustomProperties(ExitGames.Client.Photon.Hashtable values)
        {
            PublishCalls++;
            if(Failures>0){Failures--;return false;}
            pending=new();foreach(var pair in values)pending[pair.Key]=pair.Value;
            if(!Deferred)Echo();
            return true;
        }
        public void Echo(){foreach(var pair in pending)CustomProperties[pair.Key]=pair.Value;pending=null;}
    }
}
namespace Photon.Pun
{
    public class PhotonView:Component { public int ViewID; }
    public static class PhotonNetwork
    {
        public static bool InRoom=true,OfflineMode,IsMasterClient=true;
        public static Photon.Realtime.Room CurrentRoom;
        public static Photon.Realtime.Player LocalPlayer=new(){ActorNumber=1},MasterClient=LocalPlayer;
    }
}
public class Rope:Component {}
public class RopeAnchor:Component {}
public class ClimbHandle:Component {}
namespace WhereIsThing
{
    public class TestWindow
    {
        public bool IsOpen;
        public void Close()=>IsOpen=false;
        public void Tick(){}
    }
    internal partial class TestPicker
    {
        private bool _isOpen;
        private class Overlay { public bool activeSelf; }
        private Overlay _renameOverlay=new();
        public bool IsOpen {get=>_isOpen;set=>_isOpen=value;}
        public bool RenameOpen {get=>_renameOverlay.activeSelf;set=>_renameOverlay.activeSelf=value;}
        public int SettingsRefreshes;
        private void MaintainCursor(){}
        private void CloseRenameDialog()=>_renameOverlay.activeSelf=false;
        public void Close(){_isOpen=false;CloseRenameDialog();}
        public void RefreshSettings(ThingLocationScope s,ThingScanMode m,float d,float max,bool owner,ThingMapScope map)=>SettingsRefreshes++;
    }
    public class ThingLabel
    {
        public string Key;
        public bool IsValid=true;
        public int Updates;
        public bool Update(Camera c,Vector3 p,float d,bool offscreen){Updates++;return true;}
    }
    internal partial class TestLabelText
    {
        private bool _hasTextState,_lastPlaceAboveTarget;
        private string _lastTitle="",_lastOwner="",_lastDistance="";
        private float _fontSize=22;
        private const float OwnerFontScale=.78f,DistanceFontScale=.78f,RowGap=1;
        public sealed class Text {public string text;}
        private readonly Text _mainTitle=new(),_mainOwner=new(),_mainDistance=new();
        public int LayoutWrites;
        private int CountLines(string value)=>1+(value??"").Count(c=>c=='\n');
        private void SetTextPart(Text text,string value,bool enabled,float y,float height,Vector2 pivot,Vector2 pos)
        {LayoutWrites++;text.text=value;}
        public void Render(string t,string o,string d,bool above)=>SetText(t,o,d,above);
        public string Distance=>_mainDistance.text;
        public void InvalidateStyle()=>_hasTextState=false;
    }
    public sealed partial class WhereIsThingPlugin
    {
        private readonly BoundedPhaseProfiler _phaseProfiler=new BoundedPhaseProfiler("Update","Network","Retry","Cleanup","Refresh","Projection");
        private void TickPhaseTiming() { }
        private void UpdatePhaseContext() { }
        private const int ShareProtocolVersion=2,PresetSchemaVersion=6,SceneDiscoveryStepCount=20;
        private const float NetworkDiscoveryInterval=.5f;
        private const int NetworkObjectsPerFrame=128,ClassificationRetriesPerFrame=32;
        private const double ClassificationRetryBudgetMilliseconds=.25;
        private static readonly float[] ClassificationRetryDelays={.05f,.15f,.30f,.50f,1f,2f};
        private const string ShareModePropertyKey="WIT.ShareMode",ShareProtocolPropertyKey="WIT.Protocol",
            ShareActorPropertyKey="WIT.Actor",ShareHashPropertyKey="WIT.Hash",SharePayloadPropertyKey="WIT.Payload";
        private bool _shareDirty=true,_sessionValid,_labelPlacedFromItemSelection,_displayActive,_lastObservedClientRestriction;
        private string _lastPublishedSharePayload="",_lastPublishedShareHash="",_editingPresetId="";
        private float _lastPublishAt=-10,_sessionStaleUntil=-1,_hideAt;
        private int _sessionSourceActor=-1;
        private ThingPresetShareMode _lastObservedShareMode=ThingPresetShareMode.PublishedPresets;
        private List<ThingPresetDefinition> _sessionPresets=new(),_builtInPresets=new();
        private List<string> _activeLocalPresetIds=new(){"test"};
        private string PrimaryLocalPresetId=>_activeLocalPresetIds.FirstOrDefault()??"";
        private string _selectedSharedPresetId="";
        private Dictionary<int,string> _ownerNamesByActor=new();
        private TestSetting<bool> _restrictClientPresets=new(){Value=true},_enabled=new(){Value=true},_showOwnerNames=new(),_showOffscreen=new();
        private TestSetting<ThingPresetShareMode> _shareModeConfig=new(){Value=ThingPresetShareMode.PublishedPresets};
        private TestSetting<ThingLocationScope> _locationScopes=new(){Value=ThingLocationScope.Ground};
        private TestSetting<ThingScanMode> _scanMode=new(){Value=ThingScanMode.Timed};
        private TestSetting<float> _displayDuration=new(){Value=8},_maxDistance=new(){Value=500};
        private TestSetting<ThingMapScope> _mapScope=new();
        private TestSetting<KeyCode> _windowKey=new(){Value=KeyCode.C},_scanKey=new(){Value=KeyCode.C};
        private TestSetting<string> _selectedSharedPresetIdConfig=new(),_selectedItemIds=new(),_selectedSceneTargetTypesConfig=new(),
            _localPresetsConfig=new(),_activeLocalPresetIdsConfig=new(),_activeLocalPresetIdConfig=new(),_selectedLuggageTypesConfig=new();
        private TestSetting<int> _presetSchemaVersion=new();
        private TestSetting<bool> _selectedLuggage=new();
        private ThingScanMode _effectiveScanMode;
        private float _effectiveDisplayDuration,_nextRefresh,_nextSceneDiscovery,_nextCameraCheck;
        private TestWindow _window=new();
        private TestPicker _pickerWindow=new();
        private int _refreshCount,_pickerCount,_clearCount,_sceneDiscoveryStep;
        private Dictionary<string,ThingLabel> _labels=new();
        private List<ThingLabel> _labelUpdateBuffer=new();
        private List<string> _labelKeyRemovalBuffer=new();
        private List<NetworkObjectRecord> _classificationRetries=new();
        private Dictionary<int,NetworkObjectRecord> _networkObjects=new();
        private int _classificationRetryCursor;
        private NetworkObjectRecord _nextClassification,_lastSynced;
        private NetworkObjectRecord BuildNetworkObjectRecord(PhotonView view)=>_nextClassification;
        private void SyncNetworkObject(NetworkObjectRecord record)=>_lastSynced=record;
        private ThingSegmentFilter _segmentFilter=new();
        private int _observedSegmentRevision;
        private ThingMapScope _observedMapScope;
        private Camera _mainCamera;
        private void RefreshLabels()=>_refreshCount++;
        private void OpenPresetPicker(){_pickerCount++;_pickerWindow.IsOpen=true;_pickerWindow.RenameOpen=false;}
        private void PersistConfig(){}
        private void BuildCatalogIndexes(){}
        private bool IsWithinLabelRange(Transform target)=>true;
        private void UpdateLabelStyleIfChanged(){}
        private bool IsAltHeld()=>Input.Down.Contains(KeyCode.LeftAlt);
        private void ToggleWindow(){_pickerWindow.IsOpen=!_pickerWindow.IsOpen;}
        private void ClearLabels(){_clearCount++;_labels.Clear();}
        private void RemoveLabel(string key)=>_labels.Remove(key);
        private void ProcessPhotonDiscovery(){}
        private void ProcessSceneDiscovery(){}
    }
}
