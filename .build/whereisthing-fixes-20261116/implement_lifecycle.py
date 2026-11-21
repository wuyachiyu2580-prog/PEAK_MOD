from pathlib import Path
import re
ROOT=Path(__file__).resolve().parents[2]
SRC=ROOT/'MOD开发/WhereIsThing/WhereIsThing'

def bounds(s,name):
    m=re.search(r'(?m)^        (?:private|public|internal) (?:static )?[^\n]+\b'+re.escape(name)+r'(?:<[^>]+>)?\(',s)
    assert m,name
    b=s.index('{',m.end());i=b+1;d=1
    while d:d+=(s[i]=='{')-(s[i]=='}');i+=1
    return m.start(),i
def get(s,name):a,b=bounds(s,name);return s[a:b]
def replace(s,name,v):a,b=bounds(s,name);return s[:a]+v+s[b:]
def write(name,s):(SRC/name).write_text(s,encoding='utf-8')

p=SRC/'WhereIsThingPlugin.cs';s=p.read_text(encoding='utf-8-sig')
old=get(s,'Update')
a=old.index('            if (Input.GetKeyDown(_windowKey.Value)')
b=old.index('            if (!_enabled.Value)',a)
old=old[:a]+'''            bool menuInput = (_window != null && _window.IsOpen) || (_pickerWindow != null && _pickerWindow.IsOpen);
            if (Input.GetKeyDown(_windowKey.Value) && IsAltHeld())
            {
                ToggleWindow();
                menuInput = true;
            }
            else if (_window != null && _window.IsOpen)
            {
                _window.Tick();
                if (Input.GetKeyDown(KeyCode.Escape)) _window.Close();
            }
            else if (_pickerWindow != null && _pickerWindow.IsOpen)
            {
                _pickerWindow.Tick();
            }

'''+old[b:]
old=old.replace('if (Input.GetKeyDown(_scanKey.Value) && !IsAltHeld())','if (!menuInput && Input.GetKeyDown(_scanKey.Value) && !IsAltHeld())')
s=replace(s,'Update',old)
for name in ['SetMapScope','SetLocationScope','SetMaxDistance']:
    old=get(s,name);s=replace(s,name,old.replace('OpenPresetPicker();','RefreshPickerSettings();'))
old=get(s,'SetShowOwnerNames');s=replace(s,'SetShowOwnerNames',old.replace('            RefreshLabels();','            RefreshLabels();\n            RefreshPickerSettings();'))
s=s.replace('        private Camera _mainCamera;','        private Camera _mainCamera;\n        private float _nextCameraCheck;')
s=replace(s,'GetMainCamera','''        private Camera GetMainCamera()
        {
            if (_mainCamera == null || !_mainCamera.isActiveAndEnabled || Time.unscaledTime >= _nextCameraCheck)
            {
                _mainCamera = Camera.main;
                _nextCameraCheck = Time.unscaledTime + 0.5f;
            }
            return _mainCamera;
        }''')
old=get(s,'RefreshActiveSceneTargets')
old=old.replace('''                T captured = target;
                AddSceneLabel''','''                T captured = target;
                float nextFilterCheck = 0f;
                bool matchesFilter = true;
                AddSceneLabel''')
old=old.replace('''                        return captured != null && captured.gameObject.activeInHierarchy &&
                            _selectedSceneTargetTypes.Contains(sceneTargetType);''','''                        if (captured == null || !captured.gameObject.activeInHierarchy ||
                            !_selectedSceneTargetTypes.Contains(sceneTargetType)) return false;
                        if (additionalFilter != null && Time.unscaledTime >= nextFilterCheck)
                        {
                            matchesFilter = additionalFilter(captured);
                            nextFilterCheck = Time.unscaledTime + 0.25f;
                        }
                        return matchesFilter;''')
s=replace(s,'RefreshActiveSceneTargets',old)
s=replace(s,'NormalizeLocalPresets','')
write(p.name,s)

# Only remove the confirmed unused preset normalization and scene seen-set chain.
for p in SRC.glob('*.cs'):
    s=p.read_text(encoding='utf-8-sig')
    if p.name=='ThingPresets.cs':s=replace(s,'Normalize','')
    s=re.sub(r'^\s*(?:preset|migrated|existing)\.Normalize\(\);\s*$', '',s,flags=re.M)
    s=re.sub(r'^\s*NormalizeLocalPresets\(\);\s*$', '',s,flags=re.M)
    s=re.sub(r'^.*(?:readonly HashSet<string> _scanSeenBuffer|_scanSeenBuffer\.Clear\(\)|_scanSeenBuffer\.Add\(key\)|seen\.Add\(key\)).*\n','',s,flags=re.M)
    s=s.replace('_scanSeenBuffer, ','').replace('(_scanSeenBuffer)','()')
    s=s.replace('HashSet<string> seen, ','').replace('HashSet<string> seen','').replace('(seen, ','(')
    s=re.sub(r'\n(?:[ \t]*\n){3,}','\n\n',s)
    write(p.name,s)

p=SRC/'ThingPresetPickerWindow.cs';s=p.read_text(encoding='utf-8-sig')
s=replace(s,'Tick','''        public void Tick()
        {
            if (!_isOpen) return;
            MaintainCursor();
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_renameOverlay != null && _renameOverlay.activeSelf) CloseRenameDialog();
                else Close();
            }
        }''')
write(p.name,s)
p=SRC/'ThingLabel.cs';s=p.read_text(encoding='utf-8-sig')
anchor='''            _hasTextState = true;
            _lastTitle = title;'''
assert anchor in s
s=s.replace(anchor,'''            if (_hasTextState && string.Equals(_lastTitle, title, StringComparison.Ordinal) &&
                string.Equals(_lastOwner, owner, StringComparison.Ordinal) && _lastPlaceAboveTarget == placeAboveTarget)
            {
                // Distance changes do not require title layout or player-name updates.
                _lastDistance = distance;
                _mainDistance.text = distance;
                return;
            }

            _hasTextState = true;
            _lastTitle = title;''')
write(p.name,s)
p=SRC/'Helpers/FontHelper.cs';s=p.read_text(encoding='utf-8-sig')
s=s.replace('            string requestedSource;\n','').replace('GetFontForChoice(choice, out requestedSource)','GetFontForChoice(choice)').replace('ThingLabelFont choice, out string source','ThingLabelFont choice')
s=re.sub(r'^\s*source = "[^"\n]*";\n','',s,flags=re.M)
s=s.replace('''        private static TMP_FontAsset FindPreferredFont()
        {
            foreach''','''        private static TMP_FontAsset FindPreferredFont()
        {
            TMP_FontAsset[] candidates = GetCandidateFonts();
            foreach''').replace('foreach (TMP_FontAsset font in GetCandidateFonts())','foreach (TMP_FontAsset font in candidates)')
write('Helpers/FontHelper.cs',s)
p=SRC/'Helpers/ModConfigUiAdapter.cs';s=p.read_text(encoding='utf-8-sig')
a=s.index('        internal sealed class ActionFieldSubscription');b=s.index('{',a);i=b+1;depth=1
while depth:depth+=(s[i]=='{')-(s[i]=='}');i+=1
write('Helpers/ModConfigUiAdapter.cs',s[:a]+s[i:])

p=SRC/'WhereIsThingPlugin.Discovery.cs';s=p.read_text(encoding='utf-8-sig')
old='''                RemoveNetworkRecordLabels(record);
                NetworkObjectRecord replacement = BuildNetworkObjectRecord(record.View);
                replacement.FirstSeenAt = record.FirstSeenAt;
                replacement.NextRetryIndex = record.NextRetryIndex;
                replacement.LastSeenGeneration = record.LastSeenGeneration;
                _networkObjects[viewId] = replacement;
                SyncNetworkObject(replacement);'''
new='''                NetworkObjectRecord replacement = BuildNetworkObjectRecord(record.View);
                if (SameClassification(record, replacement))
                {
                    // Keep the record captured by existing label delegates.
                    record.NeedsLateClassification = replacement.NeedsLateClassification;
                    replacement = record;
                }
                else
                {
                    RemoveNetworkRecordLabels(record);
                    replacement.FirstSeenAt = record.FirstSeenAt;
                    replacement.NextRetryIndex = record.NextRetryIndex;
                    replacement.LastSeenGeneration = record.LastSeenGeneration;
                    _networkObjects[viewId] = replacement;
                }
                SyncNetworkObject(replacement);'''
assert old in s;s=s.replace(old,new)
pos=s.index('        private static bool IsBridgeVine')
s=s[:pos]+'''        private static bool SameClassification(NetworkObjectRecord left, NetworkObjectRecord right)
        {
            if (left.View != right.View || left.Item != right.Item || left.PreferPlacedItemLabel != right.PreferPlacedItemLabel ||
                left.PlacedTargets.Count != right.PlacedTargets.Count) return false;
            for (int i = 0; i < left.PlacedTargets.Count; i++)
            {
                PlacedTargetRecord a = left.PlacedTargets[i];
                PlacedTargetRecord b = right.PlacedTargets[i];
                if (a.Key != b.Key || a.FallbackName != b.FallbackName || a.Target != b.Target || a.Definition != b.Definition ||
                    a.View != b.View || a.OwnerView != b.OwnerView || a.OwnerActorNumber != b.OwnerActorNumber ||
                    a.LegacySceneTargetType != b.LegacySceneTargetType || a.Rope != b.Rope || a.IsRope != b.IsRope ||
                    a.RopeAnchor != b.RopeAnchor || a.PitonHandle != b.PitonHandle || a.ShowOwner != b.ShowOwner ||
                    (a.PositionProvider == null) != (b.PositionProvider == null)) return false;
            }
            return true;
        }

'''+s[pos:]
# Placed label callbacks refer to the stable placed record; validate before allocating new delegates.
needle='''            record.DesiredLabelKeys.Add(placed.Key);
            record.LabelKeys.Add(placed.Key);
            Func<bool> isValid'''
assert needle in s
s=s.replace(needle,'''            record.DesiredLabelKeys.Add(placed.Key);
            record.LabelKeys.Add(placed.Key);
            if (_labels.ContainsKey(placed.Key)) return;
            Func<bool> isValid''')
write(p.name,s)
p=SRC/'Properties/AssemblyInfo.cs';s=p.read_text(encoding='utf-8-sig').replace('0.1.3-dev.20261116.napberry','0.1.3-dev.20261116.fixes');write('Properties/AssemblyInfo.cs',s)
test=ROOT/'MOD开发/WhereIsThing/Tests/GameStubs.cs';s=test.read_text(encoding='utf-8-sig').replace('        internal HashSet<string> _scanSeenBuffer=new HashSet<string>();\n','');test.write_text(s,encoding='utf-8')
print('Implemented menu lifetime, Escape routing, scene filter revalidation, residue cleanup, incremental UI and classification reuse.')
