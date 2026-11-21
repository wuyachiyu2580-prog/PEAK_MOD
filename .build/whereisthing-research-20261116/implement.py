from pathlib import Path
import re
root=Path(__file__).resolve().parents[2]
source=root/'MOD开发/WhereIsThing/WhereIsThing'
backup=Path(__file__).parent/'source-before-integration'
def edit(name, transform):
 p=source/name;old=p.read_text(encoding='utf-8-sig');new=transform(old)
 if old==new:raise RuntimeError('No changes '+name)
 target=backup/name;target.parent.mkdir(parents=True,exist_ok=True)
 if not target.exists():target.write_bytes(p.read_bytes())
 with p.open('w',encoding='utf-8',newline='') as f:f.write(new)
 print('Updated',name)
def rep(s,a,b,count=1):
 n=s.count(a)
 if n!=count:raise RuntimeError(f'Expected {count}, got {n}: {a[:100]}')
 return s.replace(a,b)
def method(s,name,body):
 m=re.search(r'^        private [^\n]*\b'+name+r'\([^\n]*',s,re.M)
 if not m:raise RuntimeError('Missing '+name)
 start=s.index('{',m.end());end=start+1;depth=1
 while depth:depth+=(s[end]=='{')-(s[end]=='}');end+=1
 return s[:start]+'{\n'+body+'\n        }'+s[end:]
def plugin(s):
 s=rep(s,'private string _activeLocalPresetId = string.Empty;','private readonly List<string> _activeLocalPresetIds = new List<string>();\n        private string PrimaryLocalPresetId => _activeLocalPresetIds.Count == 0 ? string.Empty : _activeLocalPresetIds[0];\n        private bool _labelPlacedFromItemSelection;')
 s=rep(s,'private ConfigEntry<string> _activeLocalPresetIdConfig;','private ConfigEntry<string> _activeLocalPresetIdConfig;\n        private ConfigEntry<string> _activeLocalPresetIdsConfig;')
 s=rep(s,'            _selectedSharedPresetIdConfig = Config.Bind','            _activeLocalPresetIdsConfig = Config.Bind("Presets", "ActiveLocalPresetIds", string.Empty,\n                new ConfigDescription("Currently enabled local preset IDs.", null, "Hidden"));\n            _selectedSharedPresetIdConfig = Config.Bind')
 s=rep(s,'            _activeLocalPresetId = _activeLocalPresetIdConfig.Value ?? string.Empty;\n','',2)
 s=rep(s,'''            if (!_localPresets.Any(preset => string.Equals(preset.Id, _activeLocalPresetIdConfig.Value, StringComparison.Ordinal)))
            {
                _activeLocalPresetIdConfig.Value = _localPresets[0].Id;
            }''','''            _activeLocalPresetIds.Clear();
            _activeLocalPresetIds.AddRange(ThingPresetSelection.Load(_activeLocalPresetIdsConfig.Value,
                _activeLocalPresetIdConfig.Value, _localPresets.Select(preset => preset.Id)));''')
 s=rep(s,'            _activeLocalPresetIdConfig.Value = _activeLocalPresetId;','            _activeLocalPresetIdsConfig.Value = string.Join(",", _activeLocalPresetIds);\n            _activeLocalPresetIdConfig.Value = PrimaryLocalPresetId;')
 s=rep(s,'                _activeLocalPresetIdConfig,','                _activeLocalPresetIdConfig,\n                _activeLocalPresetIdsConfig,')
 s=rep(s,'                presetId = _activeLocalPresetId;','                presetId = PrimaryLocalPresetId;')
 s=method(s,'GetCurrentLocalPreset','            return GetEditableLocalPreset(PrimaryLocalPresetId);')
 start=s.index('            ThingPresetDefinition preset = GetCurrentEffectivePreset();',s.index('private void RefreshEffectivePresetState'));end=s.index('            NormalizePickupSelection(_selectedIds);',start)
 s=s[:start]+'''            IEnumerable<ThingPresetDefinition> active = IsClientPresetRestricted()
                ? new[] { GetCurrentEffectivePreset() }
                : _localPresets.Where(preset => _activeLocalPresetIds.Contains(preset.Id));
            _labelPlacedFromItemSelection = ThingPresetSelection.Combine(active, _selectedIds,
                _selectedLuggageTypes, _selectedSceneTargetTypes);
'''+s[end:]
 s=rep(s,'            string activePresetId = allowEditing ? _activeLocalPresetId : _selectedSharedPresetId;','            IEnumerable<string> activePresetIds = allowEditing ? (IEnumerable<string>)_activeLocalPresetIds : new[] { _selectedSharedPresetId };')
 s=rep(s,'_pickerWindow.Open(presets, activePresetId,','_pickerWindow.Open(presets, activePresetIds,')
 s=method(s,'SelectLocalPreset','''            if (IsClientPresetRestricted()) return;
            ThingPresetDefinition preset = GetEditableLocalPreset(presetId);
            if (preset == null) return;
            ThingPresetSelection.Toggle(_activeLocalPresetIds, preset.Id);
            SaveLocalPresets();
            RefreshEffectivePresetState(true);
            OpenPresetPicker();''')
 s=rep(s,'            _editingPresetId = preset.Id;\n            _activeLocalPresetId = preset.Id;','            _editingPresetId = preset.Id;')
 s=rep(s,'            _activeLocalPresetId = preset.Id;','            _activeLocalPresetIds.Insert(0, preset.Id);')
 s=rep(s,'''            if (string.Equals(_activeLocalPresetId, presetId, StringComparison.Ordinal))
            {
                _activeLocalPresetId = _localPresets[0].Id;
            }''','''            _activeLocalPresetIds.Remove(presetId);
            if (_activeLocalPresetIds.Count == 0) _activeLocalPresetIds.Add(_localPresets[0].Id);''')
 s=rep(s,'            if (!IsWithinMapScope(target, positionProvider)) return;','            if (!IsWithinLabelRange(target, positionProvider) || !IsWithinMapScope(target, positionProvider)) return;')
 marker='        private bool IsWithinMapScope(Transform target, Func<Vector3> positionProvider)'
 s=rep(s,marker,'''        private bool IsWithinLabelRange(Transform target, Func<Vector3> positionProvider = null)
        {
            if (target == null && positionProvider == null) return false;
            float distance = _maxDistance.Value;
            Camera camera = GetMainCamera();
            if (distance <= 0f || camera == null) return true;
            Vector3 position = positionProvider == null ? target.position : positionProvider();
            return ThingLabelRange.Within(position, camera.transform.position, distance, 10f);
        }

'''+marker)
 s=rep(s,'            Camera camera = GetMainCamera();\n            _labelUpdateBuffer.Clear();','            Camera camera = GetMainCamera();\n            Vector3 cameraPosition = camera == null ? Vector3.zero : camera.transform.position;\n            float maxDistance = _maxDistance.Value;\n            _labelUpdateBuffer.Clear();')
 s=rep(s,'                    label.Update(camera, _maxDistance.Value, _showOffscreen.Value);','                    if (!label.Update(camera, cameraPosition, maxDistance, _showOffscreen.Value)) RemoveLabel(label.Key);')
 s=rep(s,'if (target == null || !target.gameObject.activeInHierarchy ||\n                    (additionalFilter','if (target == null || !target.gameObject.activeInHierarchy || !IsWithinLabelRange(target.transform) ||\n                    (additionalFilter')
 return s
edit('WhereIsThingPlugin.cs',plugin)
def picker(s):
 s=rep(s,'private string _activePresetId;','private readonly HashSet<string> _activePresetIds = new HashSet<string>(StringComparer.Ordinal);')
 s=rep(s,'IEnumerable<ThingPresetDefinition> presets, string activePresetId,','IEnumerable<ThingPresetDefinition> presets, IEnumerable<string> activePresetIds,')
 s=rep(s,'            _activePresetId = activePresetId ?? string.Empty;','            _activePresetIds.Clear();\n            _activePresetIds.UnionWith(activePresetIds ?? Enumerable.Empty<string>());')
 s=rep(s,'bool active = string.Equals(preset.Id, _activePresetId, StringComparison.Ordinal);','bool active = _activePresetIds.Contains(preset.Id);')
 return rep(s,'"Use", ThingUi.Use(active),','"Use", _allowEditing ? ThingUi.Enable(active) : ThingUi.Use(active),')
edit('ThingPresetPickerWindow.cs',picker)
def discovery(s):
 s=rep(s,'if ((_effectiveScopes & ThingLocationScope.Ground) != 0 && item.itemState == ItemState.Ground)','if ((_effectiveScopes & ThingLocationScope.Ground) != 0 && item.itemState == ItemState.Ground && ThingItemTraits.IsReusableGroundItem(item))')
 s=rep(s,'return IsSelectedGroundItem(item, identity);','return IsSelectedGroundItem(item, identity) && ThingItemTraits.IsReusableGroundItem(item);')
 return rep(s,'if (placed.Definition != null && _selectedIds.Contains(placed.Definition.itemID))','if (_labelPlacedFromItemSelection && placed.Definition != null && _selectedIds.Contains(placed.Definition.itemID))')
edit('WhereIsThingPlugin.Discovery.cs',discovery)
def catalog(s):
 s=rep(s,'if (IsUsableName(gameName)) return gameName;','if (IsUsableName(gameName)) return ApplyVariantName(item, language, gameName);')
 s=rep(s,'                    return localized;','                    return ApplyVariantName(item, language, localized);')
 s=rep(s,'            return string.IsNullOrEmpty(item.UIData.itemName) ? item.gameObject.name : item.UIData.itemName;','            return ApplyVariantName(item, language, string.IsNullOrEmpty(item.UIData.itemName) ? item.gameObject.name : item.UIData.itemName);')
 marker='        public static string GetLuggageDisplayName(ThingLuggageType luggageType, ThingNameLanguage language)'
 s=rep(s,marker,'''        private static string ApplyVariantName(Item item, ThingNameLanguage language, string name)
        {
            if (!ThingItemTraits.TryGetMushroomVariant(item, out bool poisonous, out _)) return name;
            // Variety identity is distinct from the current safety of a cooked instance.
            return name + (UseChineseNames(language)
                ? (poisonous ? "（有毒品种）" : "（无毒品种）")
                : (poisonous ? " (Poisonous variety)" : " (Non-poisonous variety)"));
        }

'''+marker)
 return rep(s,'            string displayName = GetDisplayName(item, ThingNameLanguage.English);','            if (ThingItemTraits.TryGetMushroomVariant(item, out _, out string variant)) return "VARIANT:" + variant.ToUpperInvariant();\n            string displayName = GetDisplayName(item, ThingNameLanguage.English);')
edit('ThingTypes.cs',catalog)
def label(s):
 s=rep(s,'            _arrow.enabled = false;\n        }','            _arrow.enabled = false;\n            _root.SetActive(false);\n        }')
 start=s.index('        public void Update(Camera camera, float maxDistance, bool showOffscreen)');end=s.index('        public void Dispose()',start)
 block=s[start:end].replace('public void Update(Camera camera, float maxDistance, bool showOffscreen)','public bool Update(Camera camera, Vector3 cameraPosition, float maxDistance, bool showOffscreen)').replace('return;','return true;')
 block=rep(block,'            Vector3 worldPosition = targetPosition + Vector3.up * 0.8f;','''            if (!ThingLabelRange.Within(targetPosition, cameraPosition, maxDistance))
            {
                SetVisible(false);
                return ThingLabelRange.Within(targetPosition, cameraPosition, maxDistance, 10f);
            }
            Vector3 worldPosition = targetPosition + Vector3.up * 0.8f;''')
 block=rep(block,'Vector3.Distance(camera.transform.position, targetPosition)','Mathf.Sqrt((cameraPosition - targetPosition).sqrMagnitude)')
 block=rep(block,'            SetVisible(true);\n        }','            SetVisible(true);\n            return true;\n        }')
 return rep(s[:start]+block+s[end:],'            _group.alpha = visible ? 1f : 0f;','            _group.alpha = visible ? 1f : 0f;\n            if (_root != null) _root.SetActive(visible);')
edit('ThingLabel.cs',label)
def ui(s):
 s=rep(s,'return Text("Manage preset contents and sharing.", "管理预设内容与共享。");','return Text("Enable several presets at once. Scan results are combined.", "可同时启用多个预设，扫描结果为并集。");')
 marker='        public static string PresetWindowTitle(bool allowEditing)'
 return rep(s,marker,'''        public static string Enable(bool active)
        {
            return active ? Text("Disable", "停用") : Text("Enable", "启用");
        }

'''+marker)
edit('Helpers/ThingUi.cs',ui)

