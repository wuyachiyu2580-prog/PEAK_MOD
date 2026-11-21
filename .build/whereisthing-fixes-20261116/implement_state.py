from pathlib import Path
import re, json, hashlib, shutil

ROOT=Path(__file__).resolve().parents[2]
OUT=Path(__file__).parent
PROJECT=ROOT/'MOD开发/WhereIsThing'
SRC=PROJECT/'WhereIsThing'
assert not (OUT/'baseline.json').exists(), 'Do not reapply implementation script'
baseline={}
for folder in [PROJECT, ROOT/'MOD开发/WhySoLaggy']:
    for p in folder.rglob('*'):
        if not p.is_file() or any(x in p.parts for x in ['bin','obj']): continue
        relative=p.relative_to(ROOT)
        baseline[str(relative)]=hashlib.sha256(p.read_bytes()).hexdigest()
        if folder==PROJECT:
            dest=OUT/'backup'/relative;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,dest)
(OUT/'baseline.json').write_text(json.dumps(baseline,ensure_ascii=False,indent=2),encoding='utf-8')
path=SRC/'WhereIsThingPlugin.cs'
text=path.read_text(encoding='utf-8-sig')

def bounds(s,name):
    m=re.search(r'(?m)^        (?:private|public|internal) (?:static )?[^\n]+\b'+re.escape(name)+r'(?:<[^>]+>)?\(',s)
    assert m,name
    b=s.index('{',m.end());i=b+1;d=1
    while d:
        d+=(s[i]=='{')-(s[i]=='}');i+=1
    return m.start(),i

def get(s,name):
    a,b=bounds(s,name);return s[a:b]

def replace(s,name,value):
    a,b=bounds(s,name);return s[:a]+value+s[b:]

methods=['PublishShareStateIfNeeded','MarkShareDirty','ComputePayloadHash','UpdateRoomState',
 'OnRoomPropertiesUpdate','OnMasterClientSwitched','ClearSessionOverlay','ApplySharedStateFromRoomProperties',
 'TryGetRoomInt','TryGetRoomString','GetCurrentMasterActorNumber','HasUsableSessionPresets','GetSharedPresetSource',
 'GetCurrentEffectivePreset','GetCurrentLocalPreset','GetEditableLocalPreset','RefreshEffectivePresetState',
 'IsClientPresetRestricted','TryLoadCatalog','BuildBuiltInPresetCache','ResolveBuiltInPresetTargets']
state='\n\n'.join(get(text,m) for m in methods)
for m in methods:
    a,b=bounds(text,m);text=text[:a]+text[b:]
state=state.replace('RefreshEffectivePresetState(true);','RefreshEffectivePresetState(true, false);').replace('RefreshEffectivePresetState(false);','RefreshEffectivePresetState(false, false);')
text=text.replace('RefreshEffectivePresetState(true);','RefreshEffectivePresetState(true, true);').replace('RefreshEffectivePresetState(false);','RefreshEffectivePresetState(false, false);')

old=get(state,'PublishShareStateIfNeeded')
new=old.replace('!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null','!PhotonNetwork.InRoom || PhotonNetwork.OfflineMode || !PhotonNetwork.IsMasterClient ||\n                PhotonNetwork.CurrentRoom == null || PhotonNetwork.LocalPlayer == null || PhotonNetwork.LocalPlayer.ActorNumber <= 0')
new=new.replace('if (string.Equals(payload, _lastPublishedSharePayload, StringComparison.Ordinal) &&','if (_hasSubmittedShare && ReferenceEquals(_lastSubmittedRoom, PhotonNetwork.CurrentRoom) &&\n                _lastSubmittedActor == PhotonNetwork.LocalPlayer.ActorNumber &&\n                _lastSubmittedMode == _shareModeConfig.Value &&\n                string.Equals(payload, _lastPublishedSharePayload, StringComparison.Ordinal) &&')
new=new.replace('PhotonNetwork.CurrentRoom.SetCustomProperties(props);','''_lastPublishAt = Time.unscaledTime;
            // A true return means submitted, not a server acknowledgement.
            if (!PhotonNetwork.CurrentRoom.SetCustomProperties(props)) return;
            _hasSubmittedShare = true;
            _lastSubmittedRoom = PhotonNetwork.CurrentRoom;
            _lastSubmittedActor = PhotonNetwork.LocalPlayer.ActorNumber;
            _lastSubmittedMode = _shareModeConfig.Value;''')
state=replace(state,'PublishShareStateIfNeeded',new)
state=replace(state,'MarkShareDirty','''        private void MarkShareDirty(bool immediate)
        {
            _shareDirty = true;
            // Even urgent edits must respect the interval after a failed submission.
            if (immediate && Time.unscaledTime - _lastPublishAt >= 0.25f) _lastPublishAt = -10f;
        }''')

state=replace(state,'UpdateRoomState','''        private void UpdateRoomState()
        {
            bool inOnlineRoom = PhotonNetwork.InRoom && !PhotonNetwork.OfflineMode && PhotonNetwork.CurrentRoom != null;
            Room room = inOnlineRoom ? PhotonNetwork.CurrentRoom : null;
            bool roomChanged = !ReferenceEquals(room, _observedRoom);
            if (roomChanged)
            {
                _observedRoom = room;
                _hasSubmittedShare = false;
                _lastSubmittedRoom = null;
                _incomingShareSnapshot = null;
                _lastPublishAt = -10f;
                _nextCatalogAttempt = 0f;
                _ownerNamesByActor.Clear();
                ClearSessionOverlay();
                if (room != null)
                {
                    if (PhotonNetwork.IsMasterClient) MarkShareDirty(true);
                    else ApplySharedStateFromRoomProperties();
                }
            }

            bool clientRestricted = IsClientPresetRestricted();
            bool restrictionChanged = clientRestricted != _lastObservedClientRestriction;
            if (restrictionChanged)
            {
                _lastObservedClientRestriction = clientRestricted;
                _selectionDirty = true;
                if (_window != null && _window.IsOpen) _window.Close();
                _editingPresetId = string.Empty;
            }
            bool shareModeChanged = _shareModeConfig.Value != _lastObservedShareMode;
            if (shareModeChanged)
            {
                _lastObservedShareMode = _shareModeConfig.Value;
                PersistConfig();
                MarkShareDirty(true);
            }
            if (roomChanged || restrictionChanged)
                RefreshEffectivePresetState(true, false);
            if (_pickerWindow != null && _pickerWindow.IsOpen &&
                (restrictionChanged || shareModeChanged || (roomChanged && clientRestricted))) OpenPresetPicker();
            if (!inOnlineRoom) return;

            if (PhotonNetwork.IsMasterClient)
            {
                if (_shareModeConfig.Value == ThingPresetShareMode.PublishedPresets && _catalog.Count == 0 &&
                    Time.unscaledTime >= _nextCatalogAttempt)
                {
                    _nextCatalogAttempt = Time.unscaledTime + NetworkDiscoveryInterval;
                    TryLoadCatalog();
                }
                PublishShareStateIfNeeded();
            }
            else if (_sessionPresets.Count > 0 && !_sessionValid && _sessionStaleUntil > 0f && Time.unscaledTime >= _sessionStaleUntil)
            {
                ClearSessionOverlay();
                _incomingShareSnapshot = null;
                if (clientRestricted)
                {
                    RefreshEffectivePresetState(true, false);
                    if (_pickerWindow != null && _pickerWindow.IsOpen) OpenPresetPicker();
                }
            }
        }''')

state=replace(state,'OnRoomPropertiesUpdate','''        public void OnRoomPropertiesUpdate(PhotonHashtable propertiesThatChanged)
        {
            if (PhotonNetwork.IsMasterClient || propertiesThatChanged == null ||
                !(propertiesThatChanged.ContainsKey(ShareModePropertyKey) ||
                  propertiesThatChanged.ContainsKey(ShareProtocolPropertyKey) ||
                  propertiesThatChanged.ContainsKey(ShareActorPropertyKey) ||
                  propertiesThatChanged.ContainsKey(SharePayloadPropertyKey) ||
                  propertiesThatChanged.ContainsKey(ShareHashPropertyKey))) return;
            bool changed = ApplySharedStateFromRoomProperties();
            if (!changed || !IsClientPresetRestricted()) return;
            RefreshEffectivePresetState(true, false);
            if (_pickerWindow != null && _pickerWindow.IsOpen) OpenPresetPicker();
        }''')
old=get(state,'OnMasterClientSwitched')
old=old.replace('{\n            if (PhotonNetwork.IsMasterClient)','{\n            _hasSubmittedShare = false;\n            _incomingShareSnapshot = null;\n            _selectionDirty = true;\n            if (PhotonNetwork.IsMasterClient)',1)
old=old.replace('if (_pickerWindow != null && _pickerWindow.IsOpen)','if (_pickerWindow != null && _pickerWindow.IsOpen &&\n                (PhotonNetwork.IsMasterClient || IsClientPresetRestricted()))')
state=replace(state,'OnMasterClientSwitched',old)

state=replace(state,'ApplySharedStateFromRoomProperties','''        private bool ApplySharedStateFromRoomProperties()
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.OfflineMode || PhotonNetwork.CurrentRoom == null || PhotonNetwork.IsMasterClient)
                return false;
            SharedRoomSnapshot next = ReadSharedRoomSnapshot();
            if (_incomingShareSnapshot != null && _incomingShareSnapshot.SameAs(next)) return false;
            _incomingShareSnapshot = next;
            int mode;
            int actor;
            int protocol;
            string payload;
            if (!TryGetRoomInt(ShareModePropertyKey, out mode) || !TryGetRoomInt(ShareActorPropertyKey, out actor) ||
                !TryGetRoomInt(ShareProtocolPropertyKey, out protocol) || !TryGetRoomString(SharePayloadPropertyKey, out payload) ||
                actor != GetCurrentMasterActorNumber() || protocol != ShareProtocolVersion)
            {
                if (_sessionStaleUntil > Time.unscaledTime) return false;
                bool hadSession = _sessionPresets.Count > 0 || _sessionValid;
                ClearSessionOverlay();
                return hadSession;
            }
            if (mode != (int)ThingPresetShareMode.PublishedPresets || string.IsNullOrWhiteSpace(payload))
            {
                bool hadSession = _sessionPresets.Count > 0 || _sessionValid;
                ClearSessionOverlay();
                return hadSession;
            }
            List<ThingPresetDefinition> presets = ThingPresetCodec.DeserializePresets(payload);
            if (presets.Count == 0)
            {
                bool hadSession = _sessionPresets.Count > 0 || _sessionValid;
                ClearSessionOverlay();
                return hadSession;
            }
            _sessionPresets.Clear();
            _sessionPresets.AddRange(presets);
            _sessionSourceActor = actor;
            _sessionValid = true;
            _sessionStaleUntil = -1f;
            _selectionDirty = true;
            if (!_sessionPresets.Any(preset => string.Equals(preset.Id, _selectedSharedPresetId, StringComparison.Ordinal)))
            {
                _selectedSharedPresetId = _sessionPresets[0].Id;
                _selectedSharedPresetIdConfig.Value = _selectedSharedPresetId;
            }
            return true;
        }''')
state=state.replace('''            _sessionPresets.Clear();
            _sessionSourceActor = -1;''','''            _selectionDirty = true;
            _sessionPresets.Clear();
            _sessionSourceActor = -1;''')

old=get(state,'RefreshEffectivePresetState')
old=old.replace('bool refreshLabels)','bool refreshLabels, bool restartTimer)')
a=old.index('            IEnumerable<ThingPresetDefinition> active')
b=old.index('            _effectiveScopes =',a)
selection=old[a:b]
old=old[:a]+'''            bool restricted = IsClientPresetRestricted();
            bool sharedUsable = restricted && HasUsableSessionPresets();
            if (_selectionDirty || restricted != _selectionWasRestricted || sharedUsable != _selectionUsedShared)
            {
'''+''.join('    '+line+'\n' for line in selection.rstrip().splitlines())+'''                _selectionDirty = false;
                _selectionWasRestricted = restricted;
                _selectionUsedShared = sharedUsable;
            }
'''+old[b:]
old=old.replace('''                _hideAt = _effectiveScanMode == ThingScanMode.Timed
                    ? Time.unscaledTime + Mathf.Max(0.5f, _effectiveDisplayDuration)
                    : float.PositiveInfinity;''','''                if (restartTimer)
                    _hideAt = _effectiveScanMode == ThingScanMode.Timed
                        ? Time.unscaledTime + Mathf.Max(0.5f, _effectiveDisplayDuration)
                        : float.PositiveInfinity;''')
state=replace(state,'RefreshEffectivePresetState',old)
state=state.replace('if (loaded.Count == 0)','if (loaded.Count == 0 || !loaded.Any(definition => definition.Prefabs.Any()))')
state=state.replace('''            SaveLocalPresets();
            RefreshEffectivePresetState(false, false);''','''            SaveLocalPresets();
            _selectionDirty = true;
            MarkShareDirty(false);
            RefreshEffectivePresetState(false, false);''')
state=state.replace('''        private void BuildBuiltInPresetCache()
        {''','''        private void BuildBuiltInPresetCache()
        {
            _selectionDirty = true;''')

text=text.replace('        private string _activeRoomName = string.Empty;\n','')
text=text.replace('''        private void SaveLocalPresets()
        {''','''        private void SaveLocalPresets()
        {
            _selectionDirty = true;
            MarkShareDirty(false);''')
text=text.replace('''            _selectedSharedPresetId = presetId;
            _selectedSharedPresetIdConfig.Value = presetId;''','''            _selectionDirty = true;
            _selectedSharedPresetId = presetId;
            _selectedSharedPresetIdConfig.Value = presetId;''')
for method in ['CycleLocalScanMode','AdjustLocalDisplayDuration']:
    old=get(text,method);new=old.replace('RefreshEffectivePresetState(true, true);','ObserveScanSettings();').replace('            OpenPresetPicker();\n','')
    text=replace(text,method,new)
text=text.replace('''            UpdateRoomState();
            UpdateLabelStyleIfChanged();''','''            ObserveScanSettings();
            UpdateRoomState();
            UpdateLabelStyleIfChanged();''')

extra='''        private Room _observedRoom;
        private Room _lastSubmittedRoom;
        private int _lastSubmittedActor;
        private ThingPresetShareMode _lastSubmittedMode;
        private bool _hasSubmittedShare;
        private float _nextCatalogAttempt;
        private bool _selectionDirty = true;
        private bool _selectionWasRestricted;
        private bool _selectionUsedShared;
        private bool _scanSettingsObserved;
        private ThingScanMode _observedScanMode;
        private float _observedDisplayDuration;
        private SharedRoomSnapshot _incomingShareSnapshot;

        private sealed class SharedRoomSnapshot
        {
            public Room Room;
            public int Master;
            public object Mode, Protocol, Actor, Payload;
            public bool SameAs(SharedRoomSnapshot other)
            {
                return ReferenceEquals(Room, other.Room) && Master == other.Master &&
                    Equals(Mode, other.Mode) && Equals(Protocol, other.Protocol) &&
                    Equals(Actor, other.Actor) && Equals(Payload, other.Payload);
            }
        }

        private SharedRoomSnapshot ReadSharedRoomSnapshot()
        {
            PhotonHashtable properties = PhotonNetwork.CurrentRoom.CustomProperties;
            return new SharedRoomSnapshot
            {
                Room = PhotonNetwork.CurrentRoom,
                Master = GetCurrentMasterActorNumber(),
                Mode = properties[ShareModePropertyKey],
                Protocol = properties[ShareProtocolPropertyKey],
                Actor = properties[ShareActorPropertyKey],
                Payload = properties[SharePayloadPropertyKey]
            };
        }

        private void ObserveScanSettings()
        {
            ThingScanMode mode = _scanMode.Value;
            float duration = Mathf.Clamp(_displayDuration.Value, 2f, 60f);
            if (_scanSettingsObserved && mode == _observedScanMode && Mathf.Approximately(duration, _observedDisplayDuration)) return;
            bool changed = _scanSettingsObserved;
            _scanSettingsObserved = true;
            _observedScanMode = mode;
            _observedDisplayDuration = duration;
            RefreshEffectivePresetState(changed, changed);
            RefreshPickerSettings();
        }

        private void RefreshPickerSettings()
        {
            if (_pickerWindow != null && _pickerWindow.IsOpen)
                _pickerWindow.RefreshSettings(_locationScopes.Value, _scanMode.Value, _displayDuration.Value,
                    _maxDistance.Value, _showOwnerNames.Value, _mapScope.Value);
        }
'''
header='''using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;

namespace WhereIsThing
{
    public sealed partial class WhereIsThingPlugin
    {
'''
(SRC/'WhereIsThingPlugin.State.cs').write_text(header+extra+'\n'+state+'\n    }\n}\n',encoding='utf-8')
text=re.sub(r'\n(?:[ \t]*\n){3,}','\n\n',text)
path.write_text(text,encoding='utf-8')
picker=SRC/'ThingPresetPickerWindow.cs';s=picker.read_text(encoding='utf-8-sig')
pos=s.index('        private void RebuildSettings()')
s=s[:pos]+'''        public void RefreshSettings(ThingLocationScope scopes, ThingScanMode scanMode, float duration,
            float maxDistance, bool showOwnerNames, ThingMapScope mapScope)
        {
            _scopes = scopes;
            _scanMode = scanMode;
            _displayDuration = Mathf.Clamp(duration, 2f, 60f);
            _maxDistance = Mathf.Round(Mathf.Clamp(maxDistance, 0f, MaxDistanceMaximum) / MaxDistanceStep) * MaxDistanceStep;
            _showOwnerNames = showOwnerNames;
            _mapScope = mapScope;
            RebuildSettings();
        }

'''+s[pos:]
picker.write_text(s,encoding='utf-8')
print('Implemented production session sharing, catalog readiness, cached selection and scan setting coordination.')
