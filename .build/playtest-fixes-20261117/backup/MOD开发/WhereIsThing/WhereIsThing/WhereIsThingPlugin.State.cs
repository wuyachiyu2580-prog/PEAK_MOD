using System;
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
        private Room _observedRoom;
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

        private void PublishShareStateIfNeeded()
        {
            if (!_shareDirty || Time.unscaledTime - _lastPublishAt < 0.25f || !PhotonNetwork.InRoom || PhotonNetwork.OfflineMode || !PhotonNetwork.IsMasterClient ||
                PhotonNetwork.CurrentRoom == null || PhotonNetwork.LocalPlayer == null || PhotonNetwork.LocalPlayer.ActorNumber <= 0)
            {
                return;
            }

            string payload = string.Empty;
            if (_shareModeConfig.Value == ThingPresetShareMode.PublishedPresets)
            {
                payload = ThingPresetCodec.SerializePresets(_localPresets.Where(preset => preset.Published || ThingPresetFactory.IsBuiltInPresetId(preset.Id)));
            }
            string hash = ComputePayloadHash(payload);
            if (_hasSubmittedShare && ReferenceEquals(_lastSubmittedRoom, PhotonNetwork.CurrentRoom) &&
                _lastSubmittedActor == PhotonNetwork.LocalPlayer.ActorNumber &&
                _lastSubmittedMode == _shareModeConfig.Value &&
                string.Equals(payload, _lastPublishedSharePayload, StringComparison.Ordinal) &&
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
            _lastPublishAt = Time.unscaledTime;
            // A true return means submitted, not a server acknowledgement.
            if (!PhotonNetwork.CurrentRoom.SetCustomProperties(props)) return;
            _hasSubmittedShare = true;
            _lastSubmittedRoom = PhotonNetwork.CurrentRoom;
            _lastSubmittedActor = PhotonNetwork.LocalPlayer.ActorNumber;
            _lastSubmittedMode = _shareModeConfig.Value;
            _lastPublishedSharePayload = payload;
            _lastPublishedShareHash = hash;
            _shareDirty = false;
            _lastPublishAt = Time.unscaledTime;
        }

        private void MarkShareDirty(bool immediate)
        {
            _shareDirty = true;
            // Even urgent edits must respect the interval after a failed submission.
            if (immediate && Time.unscaledTime - _lastPublishAt >= 0.25f) _lastPublishAt = -10f;
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
        }

        public void OnRoomPropertiesUpdate(PhotonHashtable propertiesThatChanged)
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
        }

        public void OnMasterClientSwitched(Photon.Realtime.Player newMasterClient)
        {
            _hasSubmittedShare = false;
            _incomingShareSnapshot = null;
            _selectionDirty = true;
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
            RefreshEffectivePresetState(true, false);
            if (_pickerWindow != null && _pickerWindow.IsOpen &&
                (PhotonNetwork.IsMasterClient || IsClientPresetRestricted()))
            {
                OpenPresetPicker();
            }
        }

        private void ClearSessionOverlay()
        {
            _selectionDirty = true;
            _sessionPresets.Clear();
            _sessionSourceActor = -1;
            _sessionValid = false;
            _sessionStaleUntil = -1f;
        }

        private bool ApplySharedStateFromRoomProperties()
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

        private void RefreshEffectivePresetState(bool refreshLabels, bool restartTimer)
        {
            bool restricted = IsClientPresetRestricted();
            bool sharedUsable = restricted && HasUsableSessionPresets();
            if (_selectionDirty || restricted != _selectionWasRestricted || sharedUsable != _selectionUsedShared)
            {
                IEnumerable<ThingPresetDefinition> active = IsClientPresetRestricted()
                    ? new[] { GetCurrentEffectivePreset() }
                    : _localPresets.Where(preset => _activeLocalPresetIds.Contains(preset.Id));
                _labelPlacedFromItemSelection = ThingPresetSelection.Combine(active, _selectedIds,
                    _selectedLuggageTypes, _selectedSceneTargetTypes);
                NormalizePickupSelection(_selectedIds);
                ThingNapberry.ExpandSelection(_catalog, _selectedIds, _selectedSceneTargetTypes);
                _selectionDirty = false;
                _selectionWasRestricted = restricted;
                _selectionUsedShared = sharedUsable;
            }
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
                if (restartTimer)
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

        private bool TryLoadCatalog()
        {
            if (_catalog.Count > 0)
            {
                return true;
            }

            List<ThingTargetDefinition> loaded = ThingCatalog.Load();
            if (loaded.Count == 0 || !loaded.Any(definition => definition.Prefabs.Any()))
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
            _selectionDirty = true;
            MarkShareDirty(false);
            RefreshEffectivePresetState(false, false);
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
            _selectionDirty = true;
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
    }
}
