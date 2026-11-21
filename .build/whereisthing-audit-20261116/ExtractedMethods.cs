using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Photon.Pun;
using PhotonHashtable = System.Collections.Generic.Dictionary<string, object>;
namespace WhereIsThing { public sealed partial class WhereIsThingPlugin {
        private void PublishShareStateIfNeeded()
        {
            if (!_shareDirty || Time.unscaledTime - _lastPublishAt < 0.25f || !PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            {
                return;
            }

            string payload = string.Empty;
            if (_shareModeConfig.Value == ThingPresetShareMode.PublishedPresets)
            {
                payload = ThingPresetCodec.SerializePresets(_localPresets.Where(preset => preset.Published || ThingPresetFactory.IsBuiltInPresetId(preset.Id)));
            }
            string hash = ComputePayloadHash(payload);
            if (string.Equals(payload, _lastPublishedSharePayload, StringComparison.Ordinal) &&
                string.Equals(hash, _lastPublishedShareHash, StringComparison.Ordinal))
            {
                _shareDirty = false;
                _lastPublishAt = Time.unscaledTime;
                return;
            }

            PhotonHashtable props = new PhotonHashtable
            {
                { ShareModePropertyKey, (int)_shareModeConfig.Value },
                { ShareProtocolPropertyKey, ShareProtocolVersion },
                { ShareActorPropertyKey, PhotonNetwork.LocalPlayer == null ? -1 : PhotonNetwork.LocalPlayer.ActorNumber },
                { ShareHashPropertyKey, hash },
                { SharePayloadPropertyKey, payload ?? string.Empty }
            };
            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
            _lastPublishedSharePayload = payload;
            _lastPublishedShareHash = hash;
            _shareDirty = false;
            _lastPublishAt = Time.unscaledTime;
        }

        private void MarkShareDirty(bool immediate)
        {
            _shareDirty = true;
            if (immediate)
            {
                _lastPublishAt = -10f;
            }
        }

        private static string ComputePayloadHash(string payload)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char value in payload ?? string.Empty)
                {
                    hash ^= value;
                    hash *= 16777619;
                }
                return hash.ToString("X8", CultureInfo.InvariantCulture);
            }
        }

        private void UpdateRoomState()
        {
            bool clientRestricted = IsClientPresetRestricted();
            if (clientRestricted != _lastObservedClientRestriction)
            {
                _lastObservedClientRestriction = clientRestricted;
                if (_window != null && _window.IsOpen) _window.Close();
                _editingPresetId = string.Empty;
                RefreshEffectivePresetState(true);
                if (_pickerWindow != null && _pickerWindow.IsOpen) OpenPresetPicker();
            }
            if (_shareModeConfig.Value != _lastObservedShareMode)
            {
                _lastObservedShareMode = _shareModeConfig.Value;
                PersistConfig();
                MarkShareDirty(true);
                if (_pickerWindow != null && _pickerWindow.IsOpen)
                {
                    OpenPresetPicker();
                }
            }

            bool inOnlineRoom = PhotonNetwork.InRoom && !PhotonNetwork.OfflineMode && PhotonNetwork.CurrentRoom != null;
            if (!inOnlineRoom)
            {
                if (!string.IsNullOrEmpty(_activeRoomName))
                {
                    _activeRoomName = string.Empty;
                    ClearSessionOverlay();
                    RefreshEffectivePresetState(true);
                }
                return;
            }

            string roomName = PhotonNetwork.CurrentRoom.Name ?? string.Empty;
            if (!string.Equals(roomName, _activeRoomName, StringComparison.Ordinal))
            {
                _activeRoomName = roomName;
                _ownerNamesByActor.Clear();
                if (PhotonNetwork.IsMasterClient)
                {
                    ClearSessionOverlay();
                    MarkShareDirty(true);
                }
                else
                {
                    ApplySharedStateFromRoomProperties();
                    RefreshEffectivePresetState(true);
                }
            }

            if (PhotonNetwork.IsMasterClient)
            {
                PublishShareStateIfNeeded();
            }
            else if (_sessionPresets.Count > 0 && !_sessionValid && _sessionStaleUntil > 0f && Time.unscaledTime >= _sessionStaleUntil)
            {
                ClearSessionOverlay();
                RefreshEffectivePresetState(true);
                if (_pickerWindow != null && _pickerWindow.IsOpen)
                {
                    OpenPresetPicker();
                }
            }
        }

        public void OnRoomPropertiesUpdate(PhotonHashtable propertiesThatChanged)
        {
            if (!PhotonNetwork.IsMasterClient)
            {
                ApplySharedStateFromRoomProperties();
                RefreshEffectivePresetState(true);
                if (_pickerWindow != null && _pickerWindow.IsOpen)
                {
                    OpenPresetPicker();
                }
            }
        }

        public void OnMasterClientSwitched(Photon.Realtime.Player newMasterClient)
        {
            if (PhotonNetwork.IsMasterClient)
            {
                ClearSessionOverlay();
                MarkShareDirty(true);
            }
            else
            {
                _sessionValid = false;
                _sessionStaleUntil = Time.unscaledTime + 3f;
                ApplySharedStateFromRoomProperties();
            }
            RefreshEffectivePresetState(true);
            if (_pickerWindow != null && _pickerWindow.IsOpen)
            {
                OpenPresetPicker();
            }
        }

        private void ClearSessionOverlay()
        {
            _sessionPresets.Clear();
            _sessionSourceActor = -1;
            _sessionValid = false;
            _sessionStaleUntil = -1f;
        }

        private void ApplySharedStateFromRoomProperties()
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null || PhotonNetwork.IsMasterClient)
            {
                return;
            }

            int shareModeValue;
            if (!TryGetRoomInt(ShareModePropertyKey, out shareModeValue))
            {
                if (_sessionStaleUntil <= 0f || Time.unscaledTime >= _sessionStaleUntil)
                {
                    ClearSessionOverlay();
                }
                return;
            }

            int actorNumber;
            int protocolVersion;
            string payload;
            if (!TryGetRoomInt(ShareActorPropertyKey, out actorNumber) ||
                !TryGetRoomInt(ShareProtocolPropertyKey, out protocolVersion) ||
                !TryGetRoomString(SharePayloadPropertyKey, out payload) ||
                actorNumber != GetCurrentMasterActorNumber() ||
                protocolVersion != ShareProtocolVersion)
            {
                if (_sessionStaleUntil <= 0f || Time.unscaledTime >= _sessionStaleUntil)
                {
                    ClearSessionOverlay();
                }
                return;
            }

            ThingPresetShareMode shareMode = (ThingPresetShareMode)shareModeValue;
            if (shareMode != ThingPresetShareMode.PublishedPresets || string.IsNullOrWhiteSpace(payload))
            {
                ClearSessionOverlay();
                return;
            }

            List<ThingPresetDefinition> presets = ThingPresetCodec.DeserializePresets(payload);
            if (presets.Count == 0)
            {
                ClearSessionOverlay();
                return;
            }

            _sessionPresets.Clear();
            _sessionPresets.AddRange(presets);
            _sessionSourceActor = actorNumber;
            _sessionValid = true;
            _sessionStaleUntil = -1f;
            if (!_sessionPresets.Any(preset => string.Equals(preset.Id, _selectedSharedPresetId, StringComparison.Ordinal)))
            {
                _selectedSharedPresetId = _sessionPresets[0].Id;
                _selectedSharedPresetIdConfig.Value = _selectedSharedPresetId;
            }
        }

        private bool TryGetRoomInt(string key, out int value)
        {
            value = 0;
            if (PhotonNetwork.CurrentRoom == null || PhotonNetwork.CurrentRoom.CustomProperties == null)
            {
                return false;
            }

            object raw;
            if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out raw))
            {
                return false;
            }

            if (raw is int)
            {
                value = (int)raw;
                return true;
            }
            return false;
        }

        private bool TryGetRoomString(string key, out string value)
        {
            value = null;
            if (PhotonNetwork.CurrentRoom == null || PhotonNetwork.CurrentRoom.CustomProperties == null)
            {
                return false;
            }

            object raw;
            if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out raw))
            {
                return false;
            }

            value = raw as string;
            return value != null;
        }

        private int GetCurrentMasterActorNumber()
        {
            return PhotonNetwork.MasterClient == null ? -1 : PhotonNetwork.MasterClient.ActorNumber;
        }

        private bool HasUsableSessionPresets()
        {
            if (_sessionValid && _sessionSourceActor == GetCurrentMasterActorNumber() && _sessionPresets.Count > 0)
            {
                return true;
            }
            return _sessionPresets.Count > 0 && _sessionStaleUntil > Time.unscaledTime;
        }

        private IEnumerable<ThingPresetDefinition> GetSharedPresetSource()
        {
            if (HasUsableSessionPresets())
            {
                return _sessionPresets;
            }
            return _builtInPresets;
        }

        private ThingPresetDefinition GetCurrentEffectivePreset()
        {
            if (IsClientPresetRestricted())
            {
                List<ThingPresetDefinition> shared = GetSharedPresetSource().ToList();
                if (shared.Count == 0)
                {
                    return null;
                }

                ThingPresetDefinition selected = shared.FirstOrDefault(preset => string.Equals(preset.Id, _selectedSharedPresetId, StringComparison.Ordinal));
                if (selected == null)
                {
                    selected = shared[0];
                    _selectedSharedPresetId = selected.Id;
                    _selectedSharedPresetIdConfig.Value = _selectedSharedPresetId;
                }
                return selected;
            }

            return GetCurrentLocalPreset();
        }

        private ThingPresetDefinition GetCurrentLocalPreset()
        {
            return GetEditableLocalPreset(PrimaryLocalPresetId);
        }

        private ThingPresetDefinition GetEditableLocalPreset(string presetId)
        {
            if (string.IsNullOrWhiteSpace(presetId))
            {
                presetId = PrimaryLocalPresetId;
            }
            ThingPresetDefinition preset = _localPresets.FirstOrDefault(value => string.Equals(value.Id, presetId, StringComparison.Ordinal));
            if (preset == null && _localPresets.Count > 0)
            {
                preset = _localPresets[0];
            }
            return preset;
        }

        private void RefreshEffectivePresetState(bool refreshLabels)
        {
            IEnumerable<ThingPresetDefinition> active = IsClientPresetRestricted()
                ? new[] { GetCurrentEffectivePreset() }
                : _localPresets.Where(preset => _activeLocalPresetIds.Contains(preset.Id));
            _labelPlacedFromItemSelection = ThingPresetSelection.Combine(active, _selectedIds,
                _selectedLuggageTypes, _selectedSceneTargetTypes);
            NormalizePickupSelection(_selectedIds);
            ThingNapberry.ExpandSelection(_catalog, _selectedIds, _selectedSceneTargetTypes);
            _effectiveScopes = _locationScopes.Value;
            _effectiveScanMode = _scanMode.Value;
            _effectiveDisplayDuration = Mathf.Clamp(_displayDuration.Value, 2f, 60f);

            _effectiveScopes &= ~ThingLocationScope.Luggage;
            if (_selectedLuggageTypes.Count > 0)
            {
                _effectiveScopes |= ThingLocationScope.Luggage;
            }

            if (refreshLabels && _displayActive)
            {
                _hideAt = _effectiveScanMode == ThingScanMode.Timed
                    ? Time.unscaledTime + Mathf.Max(0.5f, _effectiveDisplayDuration)
                    : float.PositiveInfinity;
                RefreshLabels();
            }
        }

        private bool IsClientPresetRestricted()
        {
            return ThingPresetSelection.IsClientRestricted(_restrictClientPresets.Value,
                PhotonNetwork.InRoom, PhotonNetwork.OfflineMode, PhotonNetwork.IsMasterClient);
        }

        private void RefreshActiveSceneTargets<T>(HashSet<string> seen, ThingSceneTargetType sceneTargetType,
            Func<T, bool> additionalFilter = null) where T : Component
        {
            if (!_selectedSceneTargetTypes.Contains(sceneTargetType))
            {
                return;
            }

            foreach (T target in FindObjectsByType<T>(FindObjectsSortMode.None))
            {
                if (target == null || !target.gameObject.activeInHierarchy || !IsWithinLabelRange(target.transform) ||
                    (additionalFilter != null && !additionalFilter(target)))
                {
                    continue;
                }

                T captured = target;
                AddSceneLabel(seen, sceneTargetType, captured,
                    delegate { return ThingCatalog.GetSceneTargetDisplayName(sceneTargetType, _nameLanguage.Value); },
                    delegate
                    {
                        return captured != null && captured.gameObject.activeInHierarchy &&
                            _selectedSceneTargetTypes.Contains(sceneTargetType);
                    });
            }
        }

        private void AddSceneLabel(HashSet<string> seen, ThingSceneTargetType sceneTargetType, Component target,
            Func<string> titleProvider, Func<bool> isValid, Func<Vector3> positionProvider = null)
        {
            if (target == null || target.gameObject == null)
            {
                return;
            }

            string key = "scene:" + sceneTargetType + ":" + target.GetInstanceID();
            seen.Add(key);
            AddLabel(key, target.transform, titleProvider, isValid, positionProvider);
        }

        private bool TryLoadCatalog()
        {
            if (_catalog.Count > 0)
            {
                return true;
            }

            List<ThingTargetDefinition> loaded = ThingCatalog.Load();
            if (loaded.Count == 0)
            {
                return false;
            }

            _catalog.AddRange(loaded);
            BuildCatalogIndexes();
            MigratePickupSelections();
            MigratePlacedSceneTargetsToItems();
            BuildBuiltInPresetCache();
            ResolveBuiltInPresetTargets();
            SaveLocalPresets();
            RefreshEffectivePresetState(false);
            foreach (ThingTargetDefinition definition in _catalog)
            {
                if (definition.IsLuggage)
                {
                    continue;
                }

                bool selected = definition.ItemIds.Any(_selectedIds.Contains) ||
                    (definition.IsSceneTarget && _selectedSceneTargetTypes.Contains(definition.SceneTargetType));
                if (selected)
                {
                    definition.SetSelected(_selectedIds, _selectedLuggageTypes, _selectedSceneTargetTypes, true);
                }
            }
            _selectedItemIds.Value = string.Join(",", _selectedIds.OrderBy(id => id).Select(id => id.ToString()).ToArray());
            _selectedSceneTargetTypesConfig.Value = SerializeSceneTargetTypes(_selectedSceneTargetTypes);
            return true;
        }

        private void BuildBuiltInPresetCache()
        {
            _builtInPresets.Clear();
            ThingPresetDefinition achievement = ThingPresetFactory.CreateAchievementPreset();
            ThingPresetDefinition survival = ThingPresetFactory.CreateSurvivalPreset();
            ThingPresetDefinition ascentEight = ThingPresetFactory.CreateAscentEightPreset();
            ThingPresetDefinition playerPlaced = ThingPresetFactory.CreatePlayerPlacedPreset();
            ThingPresetFactory.ResolveBuiltInItemTargets(survival, _catalog);
            ThingPresetFactory.ResolveBuiltInItemTargets(ascentEight, _catalog);
            ThingPresetFactory.ResolveBuiltInItemTargets(playerPlaced, _catalog);
            _builtInPresets.Add(achievement);
            _builtInPresets.Add(survival);
            _builtInPresets.Add(ascentEight);
            _builtInPresets.Add(playerPlaced);
        }

        private void ResolveBuiltInPresetTargets()
        {
            foreach (ThingPresetDefinition preset in _localPresets)
            {
                int added = ThingPresetFactory.ResolveBuiltInItemTargets(preset, _catalog);
                if (added > 0)
                {
                    List<string> resolvedNames = _catalog
                        .Where(definition => definition != null && !definition.IsLuggage && !definition.IsSceneTarget &&
                            definition.ItemIds.Any(preset.SelectedItemIds.Contains))
                        .Select(definition => definition.GetDisplayName(ThingNameLanguage.English))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    string resolutionMessage = "[Presets] Resolved built-in targets for '" + ThingPresetFactory.GetDisplayName(preset) +
                        "' (" + preset.Id + "): addedItemIds=" + added + ", targetGroups=" + resolvedNames.Count +
                        ", targets=[" + string.Join(", ", resolvedNames.ToArray()) + "]";
                    if (resolvedNames.Count < 4)
                    {
                        _log.LogWarning(resolutionMessage + "; expected 4 target groups");
                    }
                }
                else if ((string.Equals(preset.Id, ThingPresetFactory.SurvivalPresetId, StringComparison.Ordinal) ||
                    string.Equals(preset.Id, ThingPresetFactory.AscentEightPresetId, StringComparison.Ordinal) ||
                    string.Equals(preset.Id, ThingPresetFactory.PlayerPlacedPresetId, StringComparison.Ordinal)) &&
                    preset.SelectedItemIds.Count == 0)
                {
                    _log.LogWarning("[Presets] Built-in target resolution found no items for '" +
                        ThingPresetFactory.GetDisplayName(preset) + "' (" + preset.Id + ")");
                }
            }
        }
}}
