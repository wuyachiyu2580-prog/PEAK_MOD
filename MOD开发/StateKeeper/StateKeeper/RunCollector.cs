using System;
using System.Collections.Generic;
using System.Globalization;
using Peak.Afflictions;
using UnityEngine;

namespace StateKeeper
{
    internal sealed class RunCollector : MonoBehaviour
    {
        private const float SampleInterval = 0.2f;
        private const float CharacterRefreshInterval = 0.5f;
        private const float InventoryScanInterval = 0.5f;
        private const float SaveInterval = 5f;
        private RunStore _store;
        private RunRecord _record;
        private float _nextSample;
        private float _nextCharacterRefresh;
        private float _nextInventoryScan;
        private float _nextSave;
        private bool _sawVictory;
        private string _finalizedRunId;
        private float _fallbackRunTime;
        private readonly Dictionary<int, ItemFingerprint> _lastInventory = new Dictionary<int, ItemFingerprint>();
        private readonly Dictionary<int, int> _lastState = new Dictionary<int, int>();
        private readonly Dictionary<string, int> _playerIndexByUserId = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly List<Character> _characterBuffer = new List<Character>(8);
        private readonly int[] _characterPlayerIndices = new int[16];
        private readonly Vector3[] _characterPositions = new Vector3[16];
        private static readonly string[] InventoryLocations =
        {
            "main:0", "main:1", "main:2", "temporary", "backpack",
            "backpack:0", "backpack:1", "backpack:2", "backpack:3"
        };
        private bool _characterCacheReady;

        public bool CollectionEnabled { get; set; } = true;

        public static RunCollector Instance { get; private set; }

        public void Initialize(RunStore store)
        {
            _store = store;
            Instance = this;
            _record = store.LoadActive();
            PrepareRecordMetadata();
            _nextSample = 0f;
            _nextCharacterRefresh = 0f;
            _nextInventoryScan = 0f;
            _nextSave = 0f;
        }

        private void PrepareRecordMetadata()
        {
            _playerIndexByUserId.Clear();
            if (_record == null) return;
            if (_record.statusTypeOrder == null || _record.statusTypeOrder.Length == 0)
                _record.statusTypeOrder = Enum.GetNames(typeof(CharacterAfflictions.STATUSTYPE));
            for (int i = 0; i < _record.players.Count; i++)
            {
                PlayerIdentity identity = _record.players[i];
                identity.playerIndex = i;
                if (!string.IsNullOrEmpty(identity.userId)) _playerIndexByUserId[identity.userId] = i;
            }
        }

        private void Update()
        {
            try
            {
                if (!CollectionEnabled) return;
                HandleRunBinding();
                if (_record == null) return;

                float now = Time.unscaledTime;
                if (now >= _nextSample)
                {
                    _nextSample = now + SampleInterval;
                    CollectFrame(false);
                }
                if (now >= _nextSave)
                {
                    _nextSave = now + SaveInterval;
                    _store.SaveActive(_record);
                }
            }
            catch (Exception ex)
            {
                    StateKeeperPlugin.LogException("collector update", ex);
            }
        }

        private void HandleRunBinding()
        {
            Guid runId = GetCurrentRunId();
            if (runId == Guid.Empty) return;
            string runIdText = runId.ToString();
            if (_record == null && string.Equals(_finalizedRunId, runIdText, StringComparison.OrdinalIgnoreCase)) return;
            if (_record != null && string.Equals(_record.header.runId, runIdText, StringComparison.OrdinalIgnoreCase)) return;

            if (_record != null && !string.IsNullOrEmpty(_record.header.runId))
            {
                _store.Complete(_record, RunStatus.Aborted, RunOutcome.Aborted);
            }

            _record = new RunRecord();
            _record.header.runId = runIdText;
            _record.header.startedUtc = DateTime.UtcNow.ToString("o");
            _record.header.lastSavedUtc = _record.header.startedUtc;
            _record.statusTypeOrder = Enum.GetNames(typeof(CharacterAfflictions.STATUSTYPE));
            _sawVictory = false;
            _finalizedRunId = null;
            _fallbackRunTime = 0f;
            _characterCacheReady = false;
            _nextCharacterRefresh = 0f;
            _nextInventoryScan = 0f;
            _lastInventory.Clear();
            _lastState.Clear();
            _playerIndexByUserId.Clear();
            _store.SaveActive(_record);
            StateKeeperPlugin.LogInfo("Bound to RunId " + runIdText);
        }

        private Guid GetCurrentRunId()
        {
            try
            {
                if (RunManager.Instance == null) return Guid.Empty;
                return RunManager.Instance.RunId;
            }
            catch { return Guid.Empty; }
        }

        private float GetRunTime()
        {
            try
            {
                if (RunManager.Instance != null)
                    return RunManager.Instance.TimeSinceRunStarted;
            }
            catch { }
            _fallbackRunTime += Time.deltaTime;
            return _fallbackRunTime;
        }

        private List<Character> GetCharacters(float now)
        {
            if (_characterCacheReady && now < _nextCharacterRefresh)
                return _characterBuffer;

            _characterBuffer.Clear();
            try
            {
                foreach (Character character in PlayerHandler.GetAllPlayerCharacters())
                    if (character != null && !_characterBuffer.Contains(character)) _characterBuffer.Add(character);
            }
            catch { }
            try
            {
                foreach (Character character in Character.AllCharacters)
                    if (character != null && !_characterBuffer.Contains(character) && !character.isBot) _characterBuffer.Add(character);
            }
            catch { }
            _characterCacheReady = true;
            _nextCharacterRefresh = now + CharacterRefreshInterval;
            return _characterBuffer;
        }

        private void CollectFrame(bool forceInventory)
        {
            if (_record == null) return;
            float time = Round(GetRunTime(), 3);
            float now = Time.unscaledTime;
            bool captureInventory = forceInventory || now >= _nextInventoryScan;
            if (captureInventory) _nextInventoryScan = now + InventoryScanInterval;
            List<Character> characters = GetCharacters(now);
            var frame = new PlayerSample { time = time };

            EnsureBufferCapacity(characters.Count);
            for (int characterIndex = 0; characterIndex < characters.Count; characterIndex++)
            {
                Character character = characters[characterIndex];
                _characterPlayerIndices[characterIndex] = -1;
                if (character == null || character.data == null || character.refs == null) continue;
                try
                {
                    PlayerIdentity identity = EnsureIdentity(character);
                    _characterPlayerIndices[characterIndex] = identity.playerIndex;
                    Vector3 position = character.Center;
                    _characterPositions[characterIndex] = position;
                    PlayerTelemetry telemetry = CaptureTelemetry(character, identity.playerIndex, position);
                    frame.players.Add(telemetry);
                    DetectStateChange(character, telemetry);
                    if (captureInventory) CaptureInventory(character, identity.playerIndex, time);
                }
                catch (Exception ex)
                {
                    if (StateKeeperPlugin.IsDebugLogging)
                        StateKeeperPlugin.LogException("capture player", ex);
                }
            }

            int validCharacterCount = 0;
            for (int i = 0; i < characters.Count; i++)
                if (_characterPlayerIndices[i] >= 0) validCharacterCount++;
            int pairCount = validCharacterCount * (validCharacterCount - 1) / 2;
            frame.distancePlayerPairs = new int[pairCount * 2];
            frame.distanceMeters = new float[pairCount];
            int pairIndex = 0;
            for (int i = 0; i < characters.Count; i++)
            {
                if (_characterPlayerIndices[i] < 0) continue;
                for (int j = i + 1; j < characters.Count; j++)
                {
                    if (_characterPlayerIndices[j] < 0) continue;
                    try
                    {
                        float meters = Round(Vector3.Distance(_characterPositions[i], _characterPositions[j]) * CharacterStats.unitsToMeters, 3);
                        frame.distancePlayerPairs[pairIndex * 2] = _characterPlayerIndices[i];
                        frame.distancePlayerPairs[pairIndex * 2 + 1] = _characterPlayerIndices[j];
                        frame.distanceMeters[pairIndex] = meters;
                        pairIndex++;
                    }
                    catch { }
                }
            }
            if (pairIndex != pairCount)
            {
                Array.Resize(ref frame.distancePlayerPairs, pairIndex * 2);
                Array.Resize(ref frame.distanceMeters, pairIndex);
            }

            if (frame.players.Count > 0)
                _record.samples.Add(frame);
        }

        private PlayerIdentity EnsureIdentity(Character character)
        {
            string userId = null;
            string displayName = character.name;
            int actorNumber = character.photonView.OwnerActorNr;
            try
            {
                Photon.Realtime.Player owner = character.photonView.Owner;
                if (owner != null)
                {
                    if (!string.IsNullOrEmpty(owner.UserId)) userId = owner.UserId;
                    if (!string.IsNullOrEmpty(owner.NickName)) displayName = owner.NickName;
                    actorNumber = owner.ActorNumber;
                }
            }
            catch { }
            if (string.IsNullOrEmpty(userId))
                userId = "actor:" + actorNumber.ToString(CultureInfo.InvariantCulture);

            int playerIndex;
            PlayerIdentity identity;
            if (!_playerIndexByUserId.TryGetValue(userId, out playerIndex) || playerIndex < 0 || playerIndex >= _record.players.Count)
            {
                playerIndex = _record.players.Count;
                identity = new PlayerIdentity { playerIndex = playerIndex, userId = userId };
                _record.players.Add(identity);
                _playerIndexByUserId[userId] = playerIndex;
            }
            else identity = _record.players[playerIndex];
            identity.playerIndex = playerIndex;
            identity.displayName = displayName;
            identity.actorNumber = actorNumber;
            identity.isLocal = character.IsLocal;
            identity.isBot = character.isBot;
            return identity;
        }

        private PlayerTelemetry CaptureTelemetry(Character character, int playerIndex, Vector3 position)
        {
            var telemetry = new PlayerTelemetry
            {
                playerIndex = playerIndex,
                regularStamina = Round(character.data.currentStamina, 4),
                extraStamina = Round(character.data.extraStamina, 4),
                maxStamina = Round(character.GetMaxStamina(), 4),
                passOutValue = Round(character.data.passOutValue, 4),
                dead = character.data.dead,
                passedOut = character.data.passedOut,
                fullyPassedOut = character.data.fullyPassedOut,
                fullyConscious = character.data.fullyConscious,
                sprinting = character.data.isSprinting,
                grounded = character.data.isGrounded,
                climbing = character.data.isClimbing,
                ropeClimbing = character.data.isRopeClimbing,
                vineClimbing = character.data.isVineClimbing,
                gliding = character.data.currentItem != null && character.data.currentItem.gliderHold,
                parachuteAvailable = character.data.hasParachute,
                struggling = character.refs.afflictions != null && character.refs.afflictions.isStruggling,
                positionX = Round(position.x, 3),
                positionY = Round(position.y, 3),
                positionZ = Round(position.z, 3),
                statuses = new float[CharacterAfflictions.NumStatusTypes]
            };
            try
            {
                telemetry.rocketActive = character.refs.movement != null && character.refs.movement.rocketActive;
                telemetry.rocketLit = character.refs.movement != null && character.refs.movement.rocketLit;
            }
            catch { }
            if (character.refs.afflictions != null)
            {
                for (int i = 0; i < CharacterAfflictions.NumStatusTypes; i++)
                {
                    CharacterAfflictions.STATUSTYPE type = (CharacterAfflictions.STATUSTYPE)i;
                    telemetry.statuses[i] = Round(character.refs.afflictions.GetCurrentStatus(type), 4);
                }
                if (character.refs.afflictions.afflictionList != null)
                    foreach (Affliction affliction in character.refs.afflictions.afflictionList)
                        if (affliction != null) telemetry.activeAfflictionTypes.Add((int)affliction.GetAfflictionType());
            }
            return telemetry;
        }

        private void DetectStateChange(Character character, PlayerTelemetry telemetry)
        {
            int signature = 0;
            if (telemetry.dead) signature |= 1 << 0;
            if (telemetry.passedOut) signature |= 1 << 1;
            if (telemetry.fullyPassedOut) signature |= 1 << 2;
            if (telemetry.sprinting) signature |= 1 << 3;
            if (telemetry.grounded) signature |= 1 << 4;
            if (telemetry.climbing) signature |= 1 << 5;
            if (telemetry.ropeClimbing) signature |= 1 << 6;
            if (telemetry.vineClimbing) signature |= 1 << 7;
            if (telemetry.gliding) signature |= 1 << 8;
            if (telemetry.rocketActive) signature |= 1 << 9;
            if (telemetry.rocketLit) signature |= 1 << 10;
            if (telemetry.struggling) signature |= 1 << 11;
            int old;
            if (_lastState.TryGetValue(telemetry.playerIndex, out old) && old != signature)
                AddEvent("StateChanged", character.IsLocal ? EventSource.LocalAuthoritative : EventSource.RemoteObserved,
                    character, null, null, null, signature.ToString(CultureInfo.InvariantCulture));
            _lastState[telemetry.playerIndex] = signature;
        }

        private void CaptureInventory(Character character, int playerIndex, float time)
        {
            global::Player player = character.player;
            if (player == null) return;

            BackpackData backpack = null;
            if (player.backpackSlot != null && player.backpackSlot.data != null)
                player.backpackSlot.data.TryGetDataEntry<BackpackData>(DataEntryKey.BackpackData, out backpack);

            bool changed = false;
            for (int i = 0; i < 3; i++)
            {
                ItemSlot slot = player.itemSlots != null && i < player.itemSlots.Length ? player.itemSlots[i] : null;
                changed |= DetectSlotChange(character, playerIndex, i, slot, InventoryLocations[i]);
            }
            changed |= DetectSlotChange(character, playerIndex, 3, player.tempFullSlot, InventoryLocations[3]);
            changed |= DetectSlotChange(character, playerIndex, 4, player.backpackSlot, InventoryLocations[4]);
            for (int i = 0; i < 4; i++)
            {
                ItemSlot slot = backpack != null && backpack.itemSlots != null && i < backpack.itemSlots.Length ? backpack.itemSlots[i] : null;
                changed |= DetectSlotChange(character, playerIndex, 5 + i, slot, InventoryLocations[5 + i]);
            }
            if (!changed) return;

            var snapshot = new InventorySnapshot { time = time, playerIndex = playerIndex };
            for (int i = 0; i < 3; i++)
            {
                ItemSlot slot = player.itemSlots != null && i < player.itemSlots.Length ? player.itemSlots[i] : null;
                snapshot.slots.Add(CaptureItem(slot, InventoryLocations[i]));
            }
            snapshot.slots.Add(CaptureItem(player.tempFullSlot, InventoryLocations[3]));
            snapshot.slots.Add(CaptureItem(player.backpackSlot, InventoryLocations[4]));
            for (int i = 0; i < 4; i++)
            {
                ItemSlot slot = backpack != null && backpack.itemSlots != null && i < backpack.itemSlots.Length ? backpack.itemSlots[i] : null;
                snapshot.slots.Add(CaptureItem(slot, InventoryLocations[5 + i]));
            }
            _record.inventorySnapshots.Add(snapshot);
        }

        private bool DetectSlotChange(Character character, int playerIndex, int slotCode, ItemSlot slot, string location)
        {
            int key = playerIndex * 32 + slotCode;
            ItemFingerprint current = ItemFingerprint.Capture(slot);
            ItemFingerprint previous;
            if (!_lastInventory.TryGetValue(key, out previous))
            {
                _lastInventory[key] = current;
                return true;
            }
            if (previous.Equals(current)) return false;

            _lastInventory[key] = current;
            ItemSnapshot item = CaptureItem(slot, location);
            string eventType;
            if (!current.occupied) eventType = "ItemRemovedObserved";
            else if (!previous.occupied || previous.itemId != current.itemId || previous.guid != current.guid) eventType = "ItemChangedObserved";
            else eventType = "ItemResourceChanged";
            AddEvent(eventType, character.IsLocal ? EventSource.LocalAuthoritative : EventSource.RemoteObserved,
                character, null, item, location, "inventory snapshot changed");
            return true;
        }

        private static ItemSnapshot CaptureItem(ItemSlot slot, string location)
        {
            ItemSnapshot item = new ItemSnapshot { slot = location };
            if (slot == null || slot.IsEmpty() || slot.prefab == null) return item;

            Item prefab = slot.prefab;
            item.itemId = prefab.itemID;
            item.itemName = prefab.UIData == null ? prefab.name : prefab.UIData.itemName;
            item.prefabName = prefab.gameObject == null ? prefab.name : prefab.gameObject.name;
            item.guid = slot.data == null ? null : slot.data.guid.ToString();
            PopulateItemResources(slot.data, item);
            return item;
        }

        private static void PopulateItemResources(ItemInstanceData data, ItemSnapshot item)
        {
            if (data == null) return;
            OptionableIntItemData uses;
            IntItemData cooked;
            FloatItemData useRemaining;
            FloatItemData fuel;
            if (data.TryGetDataEntry<OptionableIntItemData>(DataEntryKey.ItemUses, out uses)) { item.hasUses = true; item.uses = uses.Value; }
            if (data.TryGetDataEntry<FloatItemData>(DataEntryKey.UseRemainingPercentage, out useRemaining)) { item.hasUseRemaining = true; item.useRemaining = Round(useRemaining.Value, 4); }
            if (data.TryGetDataEntry<FloatItemData>(DataEntryKey.Fuel, out fuel)) { item.hasFuel = true; item.fuel = Round(fuel.Value, 4); }
            if (data.TryGetDataEntry<IntItemData>(DataEntryKey.CookedAmount, out cooked)) { item.hasCookedAmount = true; item.cookedAmount = cooked.Value; }
        }

        public void MarkVictory()
        {
            HandleRunBinding();
            _sawVictory = true;
            AddEvent("RunVictoryObserved", EventSource.RemoteObserved, null, null, null, null, "TriggerSomeoneWonRun");
        }

        public void EndRun()
        {
            HandleRunBinding();
            if (_record == null) return;
            CollectFrame(true);
            RunRecord finished = _record;
            _store.Complete(finished, RunStatus.Completed, _sawVictory ? RunOutcome.Victory : RunOutcome.Defeat);
            _record = null;
            _finalizedRunId = finished.header.runId;
            _lastInventory.Clear();
            _lastState.Clear();
            StateKeeperPlugin.LogInfo("Finalized RunId " + finished.header.runId + " as " + finished.header.outcome);
        }

        public void RecordItemEvent(string type, Item item, Character subject, Character target, EventSource source, string detail)
        {
            HandleRunBinding();
            if (_record == null) return;
            string slot = null;
            if (subject != null && subject.player != null && item != null)
            {
                slot = FindItemSlot(subject.player, item);
            }
            AddEvent(type, source, subject, target, item == null ? null : ToItemSnapshot(item), slot, detail);
        }

        public void RecordSimpleEvent(string type, Character subject, EventSource source, string detail)
        {
            HandleRunBinding();
            if (_record != null) AddEvent(type, source, subject, null, null, null, detail);
        }

        private static string FindItemSlot(global::Player player, Item item)
        {
            if (player == null || item == null) return null;
            if (player.itemSlots != null)
                for (int i = 0; i < player.itemSlots.Length; i++)
                    if (player.itemSlots[i] != null && player.itemSlots[i].prefab == item) return "main:" + i;
            if (player.tempFullSlot != null && player.tempFullSlot.prefab == item) return "temporary";
            if (player.backpackSlot != null && player.backpackSlot.prefab == item) return "backpack";
            return null;
        }

        private static ItemSnapshot ToItemSnapshot(Item item)
        {
            ItemSnapshot result = new ItemSnapshot
            {
                itemId = item.itemID,
                itemName = item.UIData == null ? item.name : item.UIData.itemName,
                prefabName = item.gameObject == null ? item.name : item.gameObject.name,
                guid = item.data == null ? null : item.data.guid.ToString()
            };
            PopulateItemResources(item.data, result);
            return result;
        }

        private void AddEvent(string type, EventSource source, Character subject, Character target, ItemSnapshot item, string slot, string detail,
            float value = 0f, float previousValue = 0f)
        {
            if (_record == null) return;
            _record.events.Add(new StatsEvent
            {
                time = Round(GetRunTime(), 3),
                type = type,
                source = source,
                subjectPlayerIndex = GetPlayerIndex(subject),
                targetPlayerIndex = GetPlayerIndex(target),
                itemId = item == null ? (ushort)0 : item.itemId,
                itemName = item == null ? null : item.itemName,
                slot = slot,
                detail = detail,
                value = value,
                previousValue = previousValue
            });
        }

        private int GetPlayerIndex(Character character)
        {
            if (character == null || _record == null) return -1;
            try { return EnsureIdentity(character).playerIndex; }
            catch { return -1; }
        }

        public void ToggleFavoriteLatest()
        {
            if (_store != null) _store.ToggleLatestFavorite();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            try
            {
                if (_record != null && _store != null) _store.SaveActive(_record);
                if (_store != null) _store.FlushPendingWrites();
            }
            catch (Exception ex) { StateKeeperPlugin.LogException("collector shutdown save", ex); }
        }

        private static float Round(float value, int digits)
        {
            float scale = digits == 3 ? 1000f : 10000f;
            return Mathf.Round(value * scale) / scale;
        }

        private void EnsureBufferCapacity(int count)
        {
            if (count <= _characterPlayerIndices.Length) return;
            throw new InvalidOperationException("StateKeeper character buffer exceeded its supported size.");
        }

        private struct ItemFingerprint : IEquatable<ItemFingerprint>
        {
            public bool occupied;
            public ushort itemId;
            public Guid guid;
            public bool hasUses;
            public int uses;
            public bool hasUseRemaining;
            public float useRemaining;
            public bool hasFuel;
            public float fuel;
            public bool hasCookedAmount;
            public int cookedAmount;

            public static ItemFingerprint Capture(ItemSlot slot)
            {
                var result = new ItemFingerprint();
                if (slot == null || slot.IsEmpty() || slot.prefab == null) return result;
                result.occupied = true;
                result.itemId = slot.prefab.itemID;
                if (slot.data == null) return result;
                result.guid = slot.data.guid;

                OptionableIntItemData uses;
                IntItemData cooked;
                FloatItemData useRemaining;
                FloatItemData fuel;
                if (slot.data.TryGetDataEntry<OptionableIntItemData>(DataEntryKey.ItemUses, out uses)) { result.hasUses = true; result.uses = uses.Value; }
                if (slot.data.TryGetDataEntry<FloatItemData>(DataEntryKey.UseRemainingPercentage, out useRemaining)) { result.hasUseRemaining = true; result.useRemaining = Round(useRemaining.Value, 4); }
                if (slot.data.TryGetDataEntry<FloatItemData>(DataEntryKey.Fuel, out fuel)) { result.hasFuel = true; result.fuel = Round(fuel.Value, 4); }
                if (slot.data.TryGetDataEntry<IntItemData>(DataEntryKey.CookedAmount, out cooked)) { result.hasCookedAmount = true; result.cookedAmount = cooked.Value; }
                return result;
            }

            public bool Equals(ItemFingerprint other)
            {
                return occupied == other.occupied && itemId == other.itemId && guid == other.guid &&
                       hasUses == other.hasUses && uses == other.uses &&
                       hasUseRemaining == other.hasUseRemaining && useRemaining.Equals(other.useRemaining) &&
                       hasFuel == other.hasFuel && fuel.Equals(other.fuel) &&
                       hasCookedAmount == other.hasCookedAmount && cookedAmount == other.cookedAmount;
            }
        }
    }
}
