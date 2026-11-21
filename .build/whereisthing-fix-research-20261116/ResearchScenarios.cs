using System;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Zorro.Core;

namespace WhereIsThing
{
    // Research-only candidate glue; not a production implementation or game validation.
    public sealed partial class WhereIsThingPlugin
    {
        private bool _hasSubmittedShare;
        private Room _lastSubmittedRoom, _observedRoom;
        private int _lastSubmittedActor;
        private ThingPresetShareMode _lastSubmittedMode;
        private bool _hasIncomingSnapshot;
        private (Room,int,object,object,object,object) _incomingSnapshot;
        private int _parsePasses;
        private ThingScanMode _observedScanMode;
        private float _observedDuration;
        private float _catalogRetryAt;
        private int _catalogAttempts;

        private void EnsureSharedCatalog()
        {
            if (_shareModeConfig.Value != ThingPresetShareMode.PublishedPresets || _catalog.Count > 0 ||
                Time.unscaledTime < _catalogRetryAt) return;
            _catalogRetryAt = Time.unscaledTime + 0.5f;
            _catalogAttempts++;
            TryLoadCatalog();
        }

        private void ObserveRoomSession()
        {
            Room current = PhotonNetwork.InRoom && !PhotonNetwork.OfflineMode ? PhotonNetwork.CurrentRoom : null;
            if (ReferenceEquals(current, _observedRoom)) return;
            _observedRoom = current;
            _hasSubmittedShare = false;
            _hasIncomingSnapshot = false;
            _activeRoomName = string.Empty;
            ClearSessionOverlay();
            if (current != null && PhotonNetwork.IsMasterClient) MarkShareDirty(true);
        }

        private static bool IsShareKey(string key) => key == ShareModePropertyKey || key == ShareProtocolPropertyKey ||
            key == ShareActorPropertyKey || key == SharePayloadPropertyKey || key == ShareHashPropertyKey;

        private object RoomValue(string key) => PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out var value) ? value : null;
        private string SessionState() => _sessionValid + ":" + HasUsableSessionPresets() + ":" + _sessionSourceActor + ":" +
            _selectedSharedPresetId + ":" + ThingPresetCodec.SerializePresets(_sessionPresets);

        private bool ApplySharedStateFromRoomProperties()
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null || PhotonNetwork.IsMasterClient) return false;
            var snapshot = (PhotonNetwork.CurrentRoom, GetCurrentMasterActorNumber(), RoomValue(ShareModePropertyKey),
                RoomValue(ShareProtocolPropertyKey), RoomValue(ShareActorPropertyKey), RoomValue(SharePayloadPropertyKey));
            if (_hasIncomingSnapshot && _incomingSnapshot.Equals(snapshot)) return false;
            string before = SessionState();
            _parsePasses++;
            ApplySharedStateFromRoomPropertiesCore();
            _incomingSnapshot = snapshot;
            _hasIncomingSnapshot = true;
            return before != SessionState();
        }

        private void ObserveScanSettings()
        {
            var mode = _scanMode.Value;
            float duration = Mathf.Clamp(_displayDuration.Value, 2f, 60f);
            if (_observedScanMode == mode && _observedDuration == duration) return;
            _observedScanMode = mode;
            _observedDuration = duration;
            RefreshEffectivePresetState(true, true);
        }

        private static WhereIsThingPlugin NewHost(string name)
        {
            Time.unscaledTime=100;
            SingletonAsset<ItemDatabase>.Instance=null;
            PhotonNetwork.InRoom=true; PhotonNetwork.OfflineMode=false; PhotonNetwork.IsMasterClient=true;
            PhotonNetwork.LocalPlayer=new(){ActorNumber=1}; PhotonNetwork.MasterClient=PhotonNetwork.LocalPlayer;
            PhotonNetwork.CurrentRoom=new(){Name=name};
            var p=new WhereIsThingPlugin();
            p._localPresets.Add(new ThingPresetDefinition("test","Test"){Published=true});
            return p;
        }

        private static Dictionary<string,object> SharedProperties(int actor, string name="Shared", string id="shared")
        {
            var preset=new ThingPresetDefinition(id,name){Published=true};
            preset.SelectedItemIds.Add(501);
            return new(){[ShareModePropertyKey]=(int)ThingPresetShareMode.PublishedPresets,[ShareProtocolPropertyKey]=2,
                [ShareActorPropertyKey]=actor,[SharePayloadPropertyKey]=ThingPresetCodec.SerializePresets(new[]{preset}),[ShareHashPropertyKey]="compat"};
        }

        internal static void RunResearch(Action<string,bool> check)
        {
            var p=NewHost("A"); p.UpdateRoomState();
            check("initial submission",PhotonNetwork.CurrentRoom.PublishCalls==1);
            p.MarkShareDirty(false); Time.unscaledTime++; p.PublishShareStateIfNeeded();
            check("unchanged same-session data is suppressed",PhotonNetwork.CurrentRoom.PublishCalls==1);
            PhotonNetwork.CurrentRoom=new(){Name="B"}; Time.unscaledTime++; p.UpdateRoomState();
            check("identical data is submitted to another room",PhotonNetwork.CurrentRoom.PublishCalls==1);
            PhotonNetwork.CurrentRoom=new(){Name="B"}; Time.unscaledTime++; p.UpdateRoomState();
            check("same-name new room object is a new session",PhotonNetwork.CurrentRoom.PublishCalls==1);
            PhotonNetwork.InRoom=false; p.UpdateRoomState();
            check("leave invalidates submitted cache",!p._hasSubmittedShare);
            PhotonNetwork.InRoom=true; Time.unscaledTime++; p.UpdateRoomState();
            check("rejoin submits again",PhotonNetwork.CurrentRoom.PublishCalls==2);

            p=NewHost("mode"); p._shareModeConfig.Value=ThingPresetShareMode.BuiltInOnly; p.UpdateRoomState();
            p._shareModeConfig.Value=ThingPresetShareMode.Off; p.UpdateRoomState();
            check("BuiltInOnly to Off changes room mode",(int)PhotonNetwork.CurrentRoom.CustomProperties[ShareModePropertyKey]==0);
            p._shareModeConfig.Value=ThingPresetShareMode.BuiltInOnly; p.UpdateRoomState();
            check("Off to BuiltInOnly changes room mode",(int)PhotonNetwork.CurrentRoom.CustomProperties[ShareModePropertyKey]==1);
            PhotonNetwork.LocalPlayer.ActorNumber=7; p.MarkShareDirty(true); p.PublishShareStateIfNeeded();
            check("new publisher actor is submitted",(int)PhotonNetwork.CurrentRoom.CustomProperties[ShareActorPropertyKey]==7);
            int calls=PhotonNetwork.CurrentRoom.PublishCalls; p.OnMasterClientSwitched(PhotonNetwork.LocalPlayer); p.PublishShareStateIfNeeded();
            check("becoming host forces publication even with identical data",PhotonNetwork.CurrentRoom.PublishCalls==calls+1);

            p=NewHost("failure"); PhotonNetwork.CurrentRoom.Failures=1; p.UpdateRoomState();
            check("failed submission retains dirty and does not cache success",p._shareDirty && !p._hasSubmittedShare);
            Time.unscaledTime+=0.1f; p.PublishShareStateIfNeeded();
            check("failed submission is throttled",PhotonNetwork.CurrentRoom.PublishCalls==1);
            Time.unscaledTime+=0.2f; p.PublishShareStateIfNeeded();
            check("failed submission can recover",PhotonNetwork.CurrentRoom.PublishCalls==2 && !p._shareDirty);
            p=NewHost("deferred"); PhotonNetwork.CurrentRoom.Deferred=true; p.UpdateRoomState();
            check("send acceptance is distinct from server echo",PhotonNetwork.CurrentRoom.CustomProperties.Count==0);
            Time.unscaledTime++; p.MarkShareDirty(false); p.PublishShareStateIfNeeded();
            check("pending echo does not cause same-content retransmission",PhotonNetwork.CurrentRoom.PublishCalls==1);
            PhotonNetwork.CurrentRoom.Echo();
            check("deferred echo contains expected actor",(int)PhotonNetwork.CurrentRoom.CustomProperties[ShareActorPropertyKey]==1);

            p=NewHost("catalog"); p._localPresets.Clear(); p._localPresets.Add(ThingPresetFactory.CreateSurvivalPreset()); p.UpdateRoomState();
            var root=new Item{itemID=501,UIData=new UIData{itemName="Medicinal Root"}}; root.gameObject.name="MedicinalRoot";
            var db=new ItemDatabase();db.itemLookup[root.itemID]=root;SingletonAsset<ItemDatabase>.Instance=db;
            p.TryLoadCatalog();Time.unscaledTime++;p.PublishShareStateIfNeeded();
            var shared=ThingPresetCodec.DeserializePresets((string)PhotonNetwork.CurrentRoom.CustomProperties[SharePayloadPropertyKey]).Single();
            check("catalog resolution republishes resolved item IDs",shared.SelectedItemIds.Contains(root.itemID));
            calls=PhotonNetwork.CurrentRoom.PublishCalls;p.TryLoadCatalog();Time.unscaledTime++;p.PublishShareStateIfNeeded();
            check("catalog cache does not repeatedly publish",PhotonNetwork.CurrentRoom.PublishCalls==calls);

            p=NewHost("automatic-catalog");p._localPresets.Clear();p._localPresets.Add(ThingPresetFactory.CreateSurvivalPreset());
            p.UpdateRoomState();
            check("unready catalog schedules a bounded retry",p._catalog.Count==0 && p._catalogAttempts==1);
            SingletonAsset<ItemDatabase>.Instance=new ItemDatabase();Time.unscaledTime+=0.5f;p.UpdateRoomState();
            check("scene-only entries cannot finalize an empty item database",p._catalog.Count==0 && p._pickupIdentityVersion.Value==0);
            int attempts=p._catalogAttempts;Time.unscaledTime+=0.1f;p.UpdateRoomState();
            check("catalog retry is not attempted each frame",p._catalogAttempts==attempts);
            SingletonAsset<ItemDatabase>.Instance=db;Time.unscaledTime+=0.5f;p.UpdateRoomState();
            shared=ThingPresetCodec.DeserializePresets((string)PhotonNetwork.CurrentRoom.CustomProperties[SharePayloadPropertyKey]).Single();
            check("host publishes ready catalog without opening UI or scanning",shared.SelectedItemIds.Contains(501));
            attempts=p._catalogAttempts;Time.unscaledTime++;p.UpdateRoomState();
            check("ready catalog stops readiness retries",p._catalogAttempts==attempts);

            p=NewHost("client");PhotonNetwork.IsMasterClient=false;PhotonNetwork.MasterClient=new(){ActorNumber=2};
            p._hideAt=108;p._pickerWindow.IsOpen=true;Time.unscaledTime=105;
            p.OnRoomPropertiesUpdate(new(){{"Game.Unrelated",1}});
            check("unrelated properties do not extend timer",p._hideAt==108);
            check("unrelated properties do not parse or rebuild picker",p._parsePasses==0 && p._pickerCount==0);
            PhotonNetwork.CurrentRoom.CustomProperties=SharedProperties(2);p.OnRoomPropertiesUpdate(new(){{SharePayloadPropertyKey,"changed"}});
            check("real shared change refreshes content without extending timer",p._refreshCount==1 && p._hideAt==108);
            check("real shared change updates restricted picker",p._pickerCount==1 && p._sessionValid);
            p.OnRoomPropertiesUpdate(new(){{SharePayloadPropertyKey,"same"}});
            check("same relevant values do not reparse or rebuild",p._parsePasses==1 && p._pickerCount==1);
            PhotonNetwork.CurrentRoom.CustomProperties[ShareHashPropertyKey]="other";p.OnRoomPropertiesUpdate(new(){{ShareHashPropertyKey,"other"}});
            check("unused hash-only update does not reparse",p._parsePasses==1);
            p._restrictClientPresets.Value=false;PhotonNetwork.CurrentRoom.CustomProperties=SharedProperties(2,"Renamed");p.OnRoomPropertiesUpdate(new(){{SharePayloadPropertyKey,"changed"}});
            check("unrestricted client retains local UI and timer",p._pickerCount==1 && p._refreshCount==1 && p._hideAt==108);
            check("unrestricted client keeps incoming cache ready",p._sessionPresets.Single().Name=="Renamed");
            p._restrictClientPresets.Value=true;p.RefreshEffectivePresetState(true,false);
            check("reenabling restriction uses the cached shared target",p._selectedIds.Contains(501) && p._hideAt==108);
            PhotonNetwork.MasterClient=new(){ActorNumber=3};p.OnMasterClientSwitched(PhotonNetwork.MasterClient);
            check("host transition preserves existing three-second grace",p.HasUsableSessionPresets() && p._sessionStaleUntil==108);
            Time.unscaledTime=106;PhotonNetwork.CurrentRoom.CustomProperties=SharedProperties(3,"NewHost");p.OnRoomPropertiesUpdate(new(){{ShareActorPropertyKey,3}});
            check("new host data replaces stale session",p._sessionValid && p._sessionSourceActor==3 && p._sessionStaleUntil<0);
            PhotonNetwork.CurrentRoom.CustomProperties[ShareProtocolPropertyKey]=99;p.OnRoomPropertiesUpdate(new(){{ShareProtocolPropertyKey,99}});
            check("invalid protocol falls back without restarting timer",!p._sessionValid && p._sessionPresets.Count==0 && p._hideAt==108);

            p=NewHost("grace");PhotonNetwork.IsMasterClient=false;PhotonNetwork.MasterClient=new(){ActorNumber=2};
            PhotonNetwork.CurrentRoom.CustomProperties=SharedProperties(2);p.UpdateRoomState();p._hideAt=108;
            Time.unscaledTime=105;PhotonNetwork.MasterClient=new(){ActorNumber=3};p.OnMasterClientSwitched(PhotonNetwork.MasterClient);
            Time.unscaledTime=107.9f;p.UpdateRoomState();check("grace retains old preset until deadline",p.HasUsableSessionPresets());
            Time.unscaledTime=108;p.UpdateRoomState();check("grace expires even without another property callback",p._sessionPresets.Count==0 && p._hideAt==108);
            PhotonNetwork.CurrentRoom.CustomProperties=SharedProperties(3);p.OnRoomPropertiesUpdate(new(){{ShareActorPropertyKey,3}});
            check("late new host data recovers after grace expiry",p._sessionValid && p._sessionSourceActor==3);
            PhotonNetwork.CurrentRoom=new(){Name="grace"};p.UpdateRoomState();
            check("same-name new room cannot retain previous shared overlay",p._sessionPresets.Count==0 && !p._sessionValid);

            p=NewHost("timer");p._scanMode.Value=ThingScanMode.Persistent;p.ObserveScanSettings();
            check("persistent mode has infinite deadline",float.IsPositiveInfinity(p._hideAt));
            Time.unscaledTime=105;p._scanMode.Value=ThingScanMode.Timed;p.ObserveScanSettings();
            check("external persistent to timed starts a finite duration",p._hideAt==113 && p._effectiveScanMode==ThingScanMode.Timed);
            Time.unscaledTime=106;p.ObserveScanSettings();
            check("unchanged configuration never restarts timer",p._hideAt==113);
            p.RefreshEffectivePresetState(true,false);
            check("content-only refresh preserves deadline",p._hideAt==113);
            p._displayDuration.Value=10;p.ObserveScanSettings();
            check("explicit duration change restarts current timed scan",p._hideAt==116);
            p._scanMode.Value=ThingScanMode.Persistent;p.ObserveScanSettings();
            check("timed to persistent clears finite expiry",float.IsPositiveInfinity(p._hideAt));
            p._displayActive=false;p._scanMode.Value=ThingScanMode.Timed;p.ObserveScanSettings();
            check("configuration change does not start an inactive scan",!p._displayActive);
            p._displayActive=true;p.RefreshEffectivePresetState(true,true);
            check("explicit user restart retains existing refresh behavior",p._hideAt==116);
        }
    }
}
internal static class ResearchProgram
{
    public static void Main()
    {
        int total=0;
        WhereIsThing.WhereIsThingPlugin.RunResearch((name,ok)=>{
            if(!ok)throw new Exception("FAILED: "+name);
            total++;Console.WriteLine("PASS: "+name);
        });
        Console.WriteLine("All "+total+" research candidate checks passed. Experimental copies only; not production or game validation.");
    }
}
