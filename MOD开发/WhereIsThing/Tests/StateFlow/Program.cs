using System;
using System.Linq;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Zorro.Core;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;
namespace WhereIsThing { public sealed partial class WhereIsThingPlugin {
        private static WhereIsThingPlugin NewHost(string name)
        {
            Time.unscaledTime=100;
            SingletonAsset<ItemDatabase>.Instance=null;
            PhotonNetwork.InRoom=true; PhotonNetwork.OfflineMode=false; PhotonNetwork.IsMasterClient=true;
            PhotonNetwork.LocalPlayer=new(){ActorNumber=1}; PhotonNetwork.MasterClient=PhotonNetwork.LocalPlayer;
            PhotonNetwork.CurrentRoom=new(){Name=name};
            var p=new WhereIsThingPlugin();
            p._localPresets.Add(new ThingPresetDefinition("test","Test"){Published=true});
            Input.Down.Clear();
            Singleton<MapHandler>.Instance=null;
            p.ObserveScanSettings();
            p._displayActive=true;
            p.RefreshEffectivePresetState(true,true);
            p._refreshCount=0;
            return p;
        }

        private static PhotonHashtable SharedProperties(int actor, string name="Shared", string id="shared")
        {
            var preset=new ThingPresetDefinition(id,name){Published=true};
            preset.SelectedItemIds.Add(501);
            return new(){[ShareModePropertyKey]=(int)ThingPresetShareMode.PublishedPresets,[ShareProtocolPropertyKey]=2,
                [ShareActorPropertyKey]=actor,[SharePayloadPropertyKey]=ThingPresetCodec.SerializePresets(new[]{preset}),[ShareHashPropertyKey]="compat"};
        }

        internal static void RunStateFlows(Action<string,bool> check)
        {
            var diagnostic=new BoundedPhaseProfiler("Update","Nested");
            check("phase timer defaults off",!diagnostic.Active);
            int reports=0;diagnostic.Tick(true,30,0,_=>reports++);
            for(int i=1;i<=100;i++){diagnostic.Current=new BoundedPhaseProfiler.Context{Objects=i,Selection=4,Frame=1000+i};diagnostic.Observe(0,i*.001);diagnostic.Observe(1,.001);}
            check("phase percentile upper bound covers P95",diagnostic.P95Upper(0)>=.095&&diagnostic.P95Upper(0)<=.1);
            check("phase timer expires at configured deadline",diagnostic.Tick(true,30,30,_=>reports++)&&!diagnostic.Active&&reports==1);
            check("phase report retains nested context",diagnostic.LastReport.Contains("objects=100")&&diagnostic.LastReport.Contains("selection=4")&&diagnostic.LastReport.Contains("nested=true"));
            string reportDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            try
            {
                string firstReport = BoundedPhaseProfiler.SaveReport(reportDirectory, diagnostic.LastReport);
                string secondReport = BoundedPhaseProfiler.SaveReport(reportDirectory, "second window");
                check("phase report persists full text and peak frame",
                    System.IO.File.ReadAllText(firstReport) == diagnostic.LastReport && diagnostic.LastReport.Contains("frame=1100"));
                check("successive phase reports have independent files",
                    secondReport != firstReport && System.IO.File.ReadAllText(secondReport) == "second window");
            }
            finally
            {
                if (System.IO.Directory.Exists(reportDirectory)) System.IO.Directory.Delete(reportDirectory, true);
            }
            diagnostic.Tick(true,30,100,_=>reports++);check("phase timer does not restart itself",!diagnostic.Active&&reports==1);
            diagnostic.Tick(false,30,100,null);diagnostic.Tick(true,30,100,null);check("explicit rearm starts a new window",diagnostic.Active);
            var scope=diagnostic.Measure(0);diagnostic.Tick(false,30,101,null);diagnostic.Tick(true,30,102,null);scope.Dispose();
            check("old phase scopes cannot pollute a new window",diagnostic.P95Upper(0)==0);
            diagnostic.Stop(103,null,"test");
            var overlap = new BoundedPhaseProfiler("Update", "LabelProjection", "NetworkCleanup", "Scene00Statues");
            overlap.Tick(true, 45, 0, null);
            overlap.Current = new BoundedPhaseProfiler.Context { Frame = 12 };
            overlap.Observe(2, 2);
            overlap.Observe(3, 3);
            overlap.Observe(1, 1);
            overlap.Observe(0, 8);
            overlap.Current = new BoundedPhaseProfiler.Context { Frame = 13 };
            overlap.Observe(2, 1);
            overlap.Observe(0, 4);
            for (int i = 0; i < 127; i++)
            {
                overlap.Observe(3, 1);
                overlap.Observe(0, 2);
            }
            overlap.Stop(45, null, "test");
            check("same frame includes independent discovery and projection costs", overlap.LastReport.Contains(
                "frame=12 scannedStatues=True ranCleanup=True updateMs=8.0000 statueMs=3.0000 cleanupMs=2.0000 projectionMs=1.0000"));
            check("cleanup-only frame does not inherit previous costs", overlap.LastReport.Contains(
                "frame=13 scannedStatues=False ranCleanup=True updateMs=4.0000 statueMs=0.0000 cleanupMs=1.0000 projectionMs=0.0000"));
            check("bounded detail reports omitted discovery frames", overlap.LastReport.Contains("omittedDiscoveryFrames=1"));
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
            Time.unscaledTime++;p._shareModeConfig.Value=ThingPresetShareMode.Off; p.UpdateRoomState();
            check("BuiltInOnly to Off changes room mode",(int)PhotonNetwork.CurrentRoom.CustomProperties[ShareModePropertyKey]==0);
            Time.unscaledTime++;p._shareModeConfig.Value=ThingPresetShareMode.BuiltInOnly; p.UpdateRoomState();
            check("Off to BuiltInOnly changes room mode",(int)PhotonNetwork.CurrentRoom.CustomProperties[ShareModePropertyKey]==1);
            Time.unscaledTime++;PhotonNetwork.LocalPlayer.ActorNumber=7; p.MarkShareDirty(true); p.PublishShareStateIfNeeded();
            check("new publisher actor is submitted",(int)PhotonNetwork.CurrentRoom.CustomProperties[ShareActorPropertyKey]==7);
            int calls=PhotonNetwork.CurrentRoom.PublishCalls; Time.unscaledTime++;p.OnMasterClientSwitched(PhotonNetwork.LocalPlayer); p.PublishShareStateIfNeeded();
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
            check("unready catalog schedules a bounded retry",p._catalog.Count==0 && p._nextCatalogAttempt==100.5f);
            SingletonAsset<ItemDatabase>.Instance=new ItemDatabase();Time.unscaledTime+=0.5f;p.UpdateRoomState();
            check("scene-only entries cannot finalize an empty item database",p._catalog.Count==0 && p._pickupIdentityVersion.Value==0);
            float attempts=p._nextCatalogAttempt;Time.unscaledTime+=0.1f;p.UpdateRoomState();
            check("catalog retry is not attempted each frame",p._nextCatalogAttempt==attempts);
            SingletonAsset<ItemDatabase>.Instance=db;Time.unscaledTime+=0.5f;p.UpdateRoomState();
            shared=ThingPresetCodec.DeserializePresets((string)PhotonNetwork.CurrentRoom.CustomProperties[SharePayloadPropertyKey]).Single();
            check("host publishes ready catalog without opening UI or scanning",shared.SelectedItemIds.Contains(501));
            attempts=p._nextCatalogAttempt;Time.unscaledTime++;p.UpdateRoomState();
            check("ready catalog stops readiness retries",p._nextCatalogAttempt==attempts);

            p=NewHost("client");PhotonNetwork.IsMasterClient=false;PhotonNetwork.MasterClient=new(){ActorNumber=2};
            p._hideAt=108;p._pickerWindow.IsOpen=true;Time.unscaledTime=105;
            p.OnRoomPropertiesUpdate(new(){{"Game.Unrelated",1}});
            check("unrelated properties do not extend timer",p._hideAt==108);
            check("unrelated properties do not parse or rebuild picker",p._incomingShareSnapshot==null && p._pickerCount==0);
            PhotonNetwork.CurrentRoom.CustomProperties=SharedProperties(2);p.OnRoomPropertiesUpdate(new(){{SharePayloadPropertyKey,"changed"}});
            check("real shared change refreshes content without extending timer",p._refreshCount==1 && p._hideAt==108);
            check("real shared change updates restricted picker",p._pickerCount==1 && p._sessionValid);
            var priorSnapshot=p._incomingShareSnapshot;
            var priorPreset=p._sessionPresets[0];
            p.OnRoomPropertiesUpdate(new(){{SharePayloadPropertyKey,"same"}});
            check("same relevant values do not reparse or rebuild",ReferenceEquals(priorSnapshot,p._incomingShareSnapshot) && ReferenceEquals(priorPreset,p._sessionPresets[0]) && p._pickerCount==1);
            PhotonNetwork.CurrentRoom.CustomProperties[ShareHashPropertyKey]="other";p.OnRoomPropertiesUpdate(new(){{ShareHashPropertyKey,"other"}});
            check("unused hash-only update does not reparse",ReferenceEquals(priorSnapshot,p._incomingShareSnapshot));
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

            p=NewHost("failure-edits");PhotonNetwork.CurrentRoom.Failures=1;p.UpdateRoomState();
            Time.unscaledTime+=0.1f;p.MarkShareDirty(true);p.PublishShareStateIfNeeded();
            check("urgent edit cannot bypass failed-send throttle",PhotonNetwork.CurrentRoom.PublishCalls==1 && p._shareDirty);
            Time.unscaledTime+=0.2f;p.PublishShareStateIfNeeded();check("urgent edit recovers after throttle",PhotonNetwork.CurrentRoom.PublishCalls==2);
            p=NewHost("actor-not-ready");PhotonNetwork.LocalPlayer=null;p.UpdateRoomState();
            check("missing local actor does not publish invalid actor",PhotonNetwork.CurrentRoom.PublishCalls==0 && p._shareDirty);
            PhotonNetwork.LocalPlayer=new(){ActorNumber=1};p.UpdateRoomState();check("local actor readiness permits submission",PhotonNetwork.CurrentRoom.PublishCalls==1);

            p=NewHost("selection-cache");p._localPresets[0].SelectedItemIds.Add(10);p.SaveLocalPresets();p.RefreshEffectivePresetState(false,false);
            check("saved preset content invalidates effective selection",p._selectedIds.SetEquals(new ushort[]{10}));
            var second=new ThingPresetDefinition("second","Second");second.SelectedItemIds.Add(20);p._localPresets.Add(second);p.SaveLocalPresets();p.SelectLocalPreset(second.Id);
            check("active preset toggle recomputes union",p._selectedIds.SetEquals(new ushort[]{10,20}));
            p.SelectLocalPreset("test");check("deselected preset disappears from cached union",p._selectedIds.SetEquals(new ushort[]{20}));
            p._localPresets[1].SelectedItemIds.Clear();p._localPresets[1].SelectedItemIds.Add(30);p.SaveLocalPresets();p.RefreshEffectivePresetState(false,false);
            check("editing active preset updates cached targets",p._selectedIds.SetEquals(new ushort[]{30}));
            p._restrictClientPresets.Value=true;PhotonNetwork.IsMasterClient=false;PhotonNetwork.MasterClient=new(){ActorNumber=2};
            PhotonNetwork.CurrentRoom.CustomProperties=SharedProperties(2);p.UpdateRoomState();
            check("restriction switches cached source to shared preset",p._selectedIds.SetEquals(new ushort[]{501}));
            p._restrictClientPresets.Value=false;p.UpdateRoomState();check("unrestrict restores unchanged local union",p._selectedIds.SetEquals(new ushort[]{30}));

            p = NewHost("scan-key");
            p._catalog.Add(new ThingTargetDefinition(new[] { root }));
            p._localPresets[0].SelectedItemIds.Add(root.itemID);
            p._selectionDirty = true;
            p.Update();
            p._displayActive = false;
            p._refreshCount = 0;
            Input.Down.Add(KeyCode.C);
            p.Update();
            check("scan key refreshes once on the activation frame", p._displayActive && p._refreshCount == 1);
            Input.Down.Clear();
            Time.unscaledTime += 0.25f;
            p.Update();
            check("next frame before the discovery deadline skips refresh", p._refreshCount == 1);
            int beforeRestart = p._refreshCount;
            Input.Down.Add(KeyCode.C);
            p.Update();
            check("another scan key immediately refreshes once and restarts timed display",
                p._refreshCount == beforeRestart + 1 && p._hideAt == Time.unscaledTime + 8f);
            Input.Down.Clear();
            int beforeScheduled = p._refreshCount;
            Time.unscaledTime += NetworkDiscoveryInterval;
            p.Update();
            check("automatic refresh resumes at its normal deadline", p._refreshCount == beforeScheduled + 1);

            p = NewHost("scan-unavailable");
            p._displayActive = false;
            p.UpdateRoomState();
            Input.Down.Add(KeyCode.C);
            p.Update();
            check("scan waits for a usable catalog", !p._displayActive && p._refreshCount == 0);
            p._catalog.Add(new ThingTargetDefinition(new[] { root }));
            p.Update();
            check("empty selection keeps scan inactive", !p._displayActive && p._refreshCount == 0);
            p._localPresets[0].SelectedItemIds.Add(root.itemID);
            p._selectionDirty = true;
            Input.Down.Add(KeyCode.LeftAlt);
            p.Update();
            check("Alt+C opens the picker without starting a scan",
                p._pickerWindow.IsOpen && !p._displayActive && p._refreshCount == 0);
            Input.Down.Clear();

            p=NewHost("menu");p.UpdateRoomState();p._refreshCount=0;p._hideAt=108;p._pickerWindow.IsOpen=true;
            var label=new ThingLabel{Key="moving"};p._labels[label.Key]=label;Time.unscaledTime=105;p.Update();
            check("open picker continues updating label projection",label.Updates==1);
            label.IsValid=false;p.Update();check("open picker removes invalid picked target",!p._labels.ContainsKey(label.Key));
            Time.unscaledTime=108;p.Update();check("timed scan expires while picker is open",!p._displayActive && p._clearCount>0);
            p._displayActive=true;p._enabled.Value=false;p.Update();check("disable takes effect while picker is open",!p._displayActive);
            p._enabled.Value=true;p._displayActive=true;p._scanMode.Value=ThingScanMode.Persistent;p.ObserveScanSettings();
            Time.unscaledTime=120;p._scanMode.Value=ThingScanMode.Timed;p.Update();check("configuration mode change works while picker is open",p._hideAt==128 && p._displayActive);
            Time.unscaledTime=128;p._scanMode.Value=ThingScanMode.Persistent;p.Update();check("switch to persistent applies before expiry check",p._displayActive && float.IsPositiveInfinity(p._hideAt));
            p._pickerWindow.RenameOpen=true;Input.Down.Add(KeyCode.Escape);p.Update();
            check("first Escape closes rename only",p._pickerWindow.IsOpen && !p._pickerWindow.RenameOpen);
            p.Update();check("second Escape closes picker",!p._pickerWindow.IsOpen);Input.Down.Clear();
            p._pickerWindow.IsOpen=true;Input.Down.Add(KeyCode.C);int refreshes=p._refreshCount;p.Update();
            check("scan hotkey is suppressed while menu is open",p._refreshCount==refreshes);Input.Down.Clear();
            p._pickerWindow.IsOpen=false;p._window.IsOpen=true;Time.unscaledTime=130;p._scanMode.Value=ThingScanMode.Timed;p.ObserveScanSettings();
            Time.unscaledTime=138;p.Update();check("timed expiry also runs in selection editor",!p._displayActive);

            var labelText=new TestLabelText();labelText.Render("Berry","Player","10m",true);int writes=labelText.LayoutWrites;
            labelText.Render("Berry","Player","11m",true);check("distance change updates text without relayout",labelText.Distance=="11m" && labelText.LayoutWrites==writes);
            labelText.Render("Root","Player","11m",true);check("changed title still relayouts",labelText.LayoutWrites==writes+3);
            writes=labelText.LayoutWrites;labelText.Render("","","11m",false);check("screen boundary changes layout",labelText.LayoutWrites==writes+3);
            writes=labelText.LayoutWrites;labelText.InvalidateStyle();labelText.Render("","","11m",false);check("style invalidation still relayouts",labelText.LayoutWrites==writes+3);

            var view=new PhotonView();var item=new Item();var left=new NetworkObjectRecord{View=view,Item=item};var right=new NetworkObjectRecord{View=view,Item=item};
            check("unchanged ordinary item classification can retain labels",SameClassification(left,right));
            right.Item=new Item();check("replacement item is not reused",!SameClassification(left,right));right.Item=item;
            var target=new Component();var a=new PlacedTargetRecord{Key="placed",Target=target,View=view,OwnerActorNumber=1};
            var b=new PlacedTargetRecord{Key="placed",Target=target,View=view,OwnerActorNumber=1};left.PlacedTargets.Add(a);right.PlacedTargets.Add(b);
            check("unchanged placed classification can retain callbacks",SameClassification(left,right));
            b.OwnerActorNumber=2;check("new owner requires binding refresh",!SameClassification(left,right));b.OwnerActorNumber=1;b.RopeAnchor=new RopeAnchor();
            check("new rope anchor requires binding refresh",!SameClassification(left,right));

            p=NewHost("bindings");var record=new NetworkObjectRecord();var carrier=new Component();var slotBinding=new object();var definition=new Item();
            check("first binding requests label creation",p.PrepareNetworkLabel(record,"slot",carrier.transform,slotBinding,definition,false));
            p._labels["slot"]=new ThingLabel{Key="slot"};record.DesiredLabelKeys.Clear();
            check("identical binding avoids rebuilding delegates",!p.PrepareNetworkLabel(record,"slot",carrier.transform,slotBinding,definition,false));
            p.ReconcileNetworkRecordLabels(record);
            check("reused label remains in desired set",p._labels.ContainsKey("slot") && record.DesiredLabelKeys.Contains("slot"));
            check("same slot new item removes old binding",p.PrepareNetworkLabel(record,"slot",carrier.transform,slotBinding,new Item(),false) && !p._labels.ContainsKey("slot"));
            p._labels["slot"]=new ThingLabel{Key="slot"};
            check("replaced slot object refreshes callbacks",p.PrepareNetworkLabel(record,"slot",carrier.transform,new object(),definition,false) && !p._labels.ContainsKey("slot"));
            p._labels["slot"]=new ThingLabel{Key="slot"};
            check("new target transform refreshes binding",p.PrepareNetworkLabel(record,"slot",new Component().transform,slotBinding,definition,false) && !p._labels.ContainsKey("slot"));
            record.DesiredLabelKeys.Clear();p.ReconcileNetworkRecordLabels(record);
            check("removed target releases cached binding",record.LabelBindings.Count==0 && record.LabelKeys.Count==0);

            p=NewHost("retry-integration");view=new PhotonView{ViewID=8};item=new Item();
            var original=new NetworkObjectRecord{View=view,Item=item,FirstSeenAt=99.9f,NeedsLateClassification=true,LastSeenGeneration=7};
            p._networkObjects[8]=original;p._classificationRetries.Add(original);original.LabelKeys.Add("live");p._labels["live"]=new ThingLabel{Key="live"};
            p._nextClassification=new NetworkObjectRecord{View=view,Item=item,NeedsLateClassification=true};p.ProcessClassificationRetries();
            check("unchanged retry preserves existing record and label",ReferenceEquals(p._networkObjects[8],original) && ReferenceEquals(p._lastSynced,original) && p._labels.ContainsKey("live"));
            check("unchanged retry advances its original retry cursor",original.NextRetryIndex==1 && ReferenceEquals(p._classificationRetries[0],original));
            Time.unscaledTime+=.1f;p._nextClassification=new NetworkObjectRecord{View=view,Item=new Item(),NeedsLateClassification=false};p.ProcessClassificationRetries();
            check("changed retry removes stale label and replaces record",!p._labels.ContainsKey("live") && ReferenceEquals(p._networkObjects[8],p._nextClassification));
            check("changed retry retains timing generation and finishes",p._classificationRetries.Count==0 && p._nextClassification.FirstSeenAt==99.9f && p._nextClassification.LastSeenGeneration==7 && p._nextClassification.NextRetryIndex==2);

            p=NewHost("settings-ui");p._pickerWindow.IsOpen=true;p._pickerWindow.RenameOpen=true;p._hideAt=108;Time.unscaledTime=105;
            p.SetMaxDistance(240);check("distance changes retain picker rename and deadline",p._pickerCount==0 && p._pickerWindow.RenameOpen && p._hideAt==108 && p._maxDistance.Value==240);
            int settingsRefreshes=p._pickerWindow.SettingsRefreshes;p.SetMaxDistance(240);
            check("unchanged distance does not refresh settings",p._pickerWindow.SettingsRefreshes==settingsRefreshes);
            p.SetLocationScope(ThingLocationScope.Held,true);check("scope settings update without rebuilding preset rows",p._pickerCount==0 && p._pickerWindow.RenameOpen && (p._effectiveScopes&ThingLocationScope.Held)!=0 && p._hideAt==113);
            p.SetShowOwnerNames(true);check("owner setting preserves local editor",p._pickerCount==0 && p._pickerWindow.RenameOpen);
            p.CycleLocalScanMode();check("own UI mode change uses shared observer",p._effectiveScanMode==ThingScanMode.Persistent && float.IsPositiveInfinity(p._hideAt));
            p.CycleLocalScanMode();p.AdjustLocalDisplayDuration(1);check("own UI duration uses shared observer",p._effectiveDisplayDuration==10 && p._hideAt==115);
            Time.unscaledTime++;p.ObserveScanSettings();check("next frame does not restart own UI change again",p._hideAt==115);

            p=NewHost("filters");var fruit=new FakeItem();FakeItems.Clear();FakeItems.Add(fruit);bool allow=true;
            p._selectedSceneTargetTypes.Add(ThingSceneTargetType.SporeCloud);
            p.RefreshActiveSceneTargets<FakeItem>(ThingSceneTargetType.SporeCloud,x=>allow);
            var sceneLabel=p.Labels.Values.Single();check("scene predicate starts valid",sceneLabel.valid());allow=false;Time.unscaledTime+=0.3f;
            check("existing scene label rechecks additional predicate",!sceneLabel.valid());FakeItems.Clear();
            p=NewHost("camera");Camera.main=new Camera();var camera=p.GetMainCamera();Camera.main=new Camera();Time.unscaledTime+=0.6f;
            check("same-scene camera replacement is observed",p.GetMainCamera()==Camera.main && p.GetMainCamera()!=camera);

        }
    }
}
internal static class ResearchProgram
{
    public static void Main()
    {
        int total=0;
        WhereIsThing.WhereIsThingPlugin.RunStateFlows((name,ok)=>{
            if(!ok)throw new Exception("FAILED: "+name);
            total++;Console.WriteLine("PASS: "+name);
        });
        Console.WriteLine("All "+total+" production state-flow checks passed. Game/API doubles; native rendering and networking still require in-game validation.");
    }
}
