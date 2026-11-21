from pathlib import Path
import hashlib, json, re
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
OUT = Path(__file__).parent
AUDIT = ROOT / '.build/whereisthing-audit-20261116'
SRC = ROOT / 'MOD开发/WhereIsThing/WhereIsThing'
files = [p for p in SRC.rglob('*') if p.is_file() and 'bin' not in p.parts and 'obj' not in p.parts]
(OUT / 'source-before.json').write_text(json.dumps({str(p.relative_to(ROOT)): hashlib.sha256(p.read_bytes()).hexdigest() for p in files}, indent=2), encoding='utf-8')
s = (AUDIT / 'ExtractedMethods.cs').read_text(encoding='utf-8-sig')

def change(old, new):
    global s
    assert s.count(old) == 1, (old[:90], s.count(old))
    s = s.replace(old, new)

change('if (string.Equals(payload, _lastPublishedSharePayload, StringComparison.Ordinal) &&', '''if (_hasSubmittedShare && ReferenceEquals(_lastSubmittedRoom, PhotonNetwork.CurrentRoom) &&
                _lastSubmittedActor == PhotonNetwork.LocalPlayer.ActorNumber &&
                _lastSubmittedMode == _shareModeConfig.Value &&
                string.Equals(payload, _lastPublishedSharePayload, StringComparison.Ordinal) &&''')
change('PhotonNetwork.CurrentRoom.SetCustomProperties(props);', '''_lastPublishAt = Time.unscaledTime;
            if (!PhotonNetwork.CurrentRoom.SetCustomProperties(props)) return;
            _hasSubmittedShare = true;
            _lastSubmittedRoom = PhotonNetwork.CurrentRoom;
            _lastSubmittedActor = PhotonNetwork.LocalPlayer.ActorNumber;
            _lastSubmittedMode = _shareModeConfig.Value;''')
change('private void UpdateRoomState()\n        {', 'private void UpdateRoomState()\n        {\n            ObserveRoomSession();')
change('private void ApplySharedStateFromRoomProperties()', 'private void ApplySharedStateFromRoomPropertiesCore()')
change('private void RefreshEffectivePresetState(bool refreshLabels)', 'private void RefreshEffectivePresetState(bool refreshLabels, bool restartTimer = true)')
change('''                _hideAt = _effectiveScanMode == ThingScanMode.Timed
                    ? Time.unscaledTime + Mathf.Max(0.5f, _effectiveDisplayDuration)
                    : float.PositiveInfinity;
                RefreshLabels();''', '''                if (restartTimer)
                    _hideAt = _effectiveScanMode == ThingScanMode.Timed
                        ? Time.unscaledTime + Mathf.Max(0.5f, _effectiveDisplayDuration)
                        : float.PositiveInfinity;
                RefreshLabels();''')
change('''            SaveLocalPresets();
            RefreshEffectivePresetState(false);''', '''            SaveLocalPresets();
            MarkShareDirty(false);
            RefreshEffectivePresetState(false);''')
change('if (loaded.Count == 0)', 'if (loaded.Count == 0 || !loaded.Any(definition => definition.Prefabs.Any()))')
change('''            if (PhotonNetwork.IsMasterClient)
            {
                PublishShareStateIfNeeded();''', '''            if (PhotonNetwork.IsMasterClient)
            {
                EnsureSharedCatalog();
                PublishShareStateIfNeeded();''')
change('''            if (!PhotonNetwork.IsMasterClient)
            {
                ApplySharedStateFromRoomProperties();
                RefreshEffectivePresetState(true);
                if (_pickerWindow != null && _pickerWindow.IsOpen)
                {
                    OpenPresetPicker();
                }
            }''', '''            if (PhotonNetwork.IsMasterClient || propertiesThatChanged == null ||
                !propertiesThatChanged.Keys.Any(IsShareKey)) return;
            bool changed = ApplySharedStateFromRoomProperties();
            if (!changed || !IsClientPresetRestricted()) return;
            RefreshEffectivePresetState(true, false);
            if (_pickerWindow != null && _pickerWindow.IsOpen) OpenPresetPicker();''')
change('''        public void OnMasterClientSwitched(Photon.Realtime.Player newMasterClient)
        {''', '''        public void OnMasterClientSwitched(Photon.Realtime.Player newMasterClient)
        {
            _hasSubmittedShare = false;''')
# All implicit room/host changes preserve the running timer; explicit user selection retains its old semantics.
for method in ['UpdateRoomState', 'OnMasterClientSwitched']:
    match = re.search(r'(?m)^        (?:private|public) void '+method+r'\(', s)
    start = s.index('{', match.end()); depth = 1; end = start+1
    while depth:
        depth += (s[end]=='{')-(s[end]=='}'); end += 1
    chunk=s[match.start():end]
    s=s[:match.start()]+chunk.replace('RefreshEffectivePresetState(true);','RefreshEffectivePresetState(true, false);')+s[end:]
(OUT/'CandidateMethods.cs').write_text(s, encoding='utf-8')

h=(AUDIT/'AuditHarness.cs').read_text(encoding='utf-8-sig')
h=h[:h.index('internal static class AuditProgram')]
h=h.replace('public int PublishCalls;', 'public int PublishCalls; public int Failures; public bool Deferred; public Dictionary<string,object> Pending;')
h=h.replace('''public void SetCustomProperties(Dictionary<string,object> values)
        { PublishCalls++;foreach(var entry in values)CustomProperties[entry.Key]=entry.Value; }''','''public bool SetCustomProperties(Dictionary<string,object> values)
        {
            PublishCalls++;
            if (Failures > 0) { Failures--; return false; }
            Pending = new(values);
            if (!Deferred) Echo();
            return true;
        }
        public void Echo() { foreach(var entry in Pending) CustomProperties[entry.Key]=entry.Value; Pending=null; }''')
assert 'public bool SetCustomProperties' in h
(OUT/'Harness.cs').write_text(h, encoding='utf-8')
(OUT/'GameStubs.cs').write_bytes((AUDIT/'GameStubs.cs').read_bytes())
project=ET.parse(AUDIT/'Audit.csproj')
for elem in project.findall('.//Compile'):
    if elem.attrib['Include']=='AuditHarness.cs': elem.set('Include','Harness.cs')
    if elem.attrib['Include']=='ExtractedMethods.cs': elem.set('Include','CandidateMethods.cs')
ET.SubElement(project.find('ItemGroup'),'Compile',Include='ResearchScenarios.cs')
project.write(OUT/'Probe.csproj', encoding='utf-8')
print(json.dumps({'production_files_hashed':len(files),'scope':'experimental copies only; no production fix'}))
