from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
TEST=ROOT/'MOD开发/WhereIsThing/Tests/StateFlow'
s=(ROOT/'.build/whereisthing-fix-research-20261116/ResearchScenarios.cs').read_text(encoding='utf-8-sig')
s='''using System;
using System.Linq;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Zorro.Core;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;
namespace WhereIsThing { public sealed partial class WhereIsThingPlugin {
'''+s[s.index('        private static WhereIsThingPlugin NewHost'):]
s=s.replace('Dictionary<string,object> SharedProperties','PhotonHashtable SharedProperties')
s=s.replace('''            return p;
        }''','''            Input.Down.Clear();
            Singleton<MapHandler>.Instance=null;
            p.ObserveScanSettings();
            p._displayActive=true;
            p.RefreshEffectivePresetState(true,true);
            p._refreshCount=0;
            return p;
        }''',1)
s=s.replace('p._shareModeConfig.Value=ThingPresetShareMode.Off; p.UpdateRoomState();','Time.unscaledTime++;p._shareModeConfig.Value=ThingPresetShareMode.Off; p.UpdateRoomState();')
s=s.replace('p._shareModeConfig.Value=ThingPresetShareMode.BuiltInOnly; p.UpdateRoomState();\n            check("Off','Time.unscaledTime++;p._shareModeConfig.Value=ThingPresetShareMode.BuiltInOnly; p.UpdateRoomState();\n            check("Off')
s=s.replace('PhotonNetwork.LocalPlayer.ActorNumber=7;','Time.unscaledTime++;PhotonNetwork.LocalPlayer.ActorNumber=7;')
s=s.replace('int calls=PhotonNetwork.CurrentRoom.PublishCalls; p.OnMasterClientSwitched','int calls=PhotonNetwork.CurrentRoom.PublishCalls; Time.unscaledTime++;p.OnMasterClientSwitched')
s=s.replace('p._catalogAttempts==1','p._nextCatalogAttempt==100.5f').replace('int attempts=p._catalogAttempts','float attempts=p._nextCatalogAttempt').replace('attempts=p._catalogAttempts','attempts=p._nextCatalogAttempt').replace('p._catalogAttempts==attempts','p._nextCatalogAttempt==attempts')
s=s.replace('p._parsePasses==0 && p._pickerCount==0','p._incomingShareSnapshot==null && p._pickerCount==0')
s=s.replace('''            p.OnRoomPropertiesUpdate(new(){{SharePayloadPropertyKey,"same"}});''','''            var priorSnapshot=p._incomingShareSnapshot;
            var priorPreset=p._sessionPresets[0];
            p.OnRoomPropertiesUpdate(new(){{SharePayloadPropertyKey,"same"}});''')
s=s.replace('p._parsePasses==1 && p._pickerCount==1','ReferenceEquals(priorSnapshot,p._incomingShareSnapshot) && ReferenceEquals(priorPreset,p._sessionPresets[0]) && p._pickerCount==1').replace('p._parsePasses==1','ReferenceEquals(priorSnapshot,p._incomingShareSnapshot)')
s=s.replace('internal static void RunResearch','internal static void RunStateFlows').replace('RunResearch((name,ok)','RunStateFlows((name,ok)').replace('research candidate checks passed. Experimental copies only; not production or game validation.','production state-flow checks passed. Game/API doubles; native rendering and networking still require in-game validation.')
extra='''
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

            p=NewHost("menu");p.UpdateRoomState();p._refreshCount=0;p._hideAt=108;p._pickerWindow.IsOpen=true;
            var label=new RuntimeLabel{Key="moving"};p._labels[label.Key]=label;Time.unscaledTime=105;p.Update();
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

            p=NewHost("filters");var fruit=new FakeItem();FakeItems.Clear();FakeItems.Add(fruit);bool allow=true;
            p._selectedSceneTargetTypes.Add(ThingSceneTargetType.SporeCloud);
            p.RefreshActiveSceneTargets<FakeItem>(ThingSceneTargetType.SporeCloud,x=>allow);
            var sceneLabel=p.Labels.Values.Single();check("scene predicate starts valid",sceneLabel.valid());allow=false;Time.unscaledTime+=0.3f;
            check("existing scene label rechecks additional predicate",!sceneLabel.valid());FakeItems.Clear();
            p=NewHost("camera");Camera.main=new Camera();var camera=p.GetMainCamera();Camera.main=new Camera();Time.unscaledTime+=0.6f;
            check("same-scene camera replacement is observed",p.GetMainCamera()==Camera.main && p.GetMainCamera()!=camera);
'''
anchor='''            check("explicit user restart retains existing refresh behavior",p._hideAt==116);'''
assert anchor in s;s=s.replace(anchor,anchor+'\n'+extra)
(TEST/'Program.cs').write_text(s,encoding='utf-8')
ps=TEST/'ExtractProduction.ps1';s=ps.read_text(encoding='utf-8-sig').replace('public class TestPicker','internal partial class TestPicker').replace('public class TestLabelText','internal partial class TestLabelText');ps.write_text(s,encoding='utf-8')
p=TEST/'GameRuntimeStubs.cs';s=p.read_text(encoding='utf-8-sig').replace('public partial class TestPicker','internal partial class TestPicker').replace('public partial class TestLabelText','internal partial class TestLabelText');p.write_text(s,encoding='utf-8')
print('Formal state-flow tests reference production State.cs and regenerate exact Update/selection/label fragments on every build.')
