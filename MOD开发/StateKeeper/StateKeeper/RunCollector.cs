using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Peak.Afflictions;
using UnityEngine;
using Zorro.Core;

namespace StateKeeper
{
    internal sealed class RunCollector : MonoBehaviour
    {
        private const float SampleInterval = 0.2f;
        private const float CharacterRefreshInterval = 0.5f;
        private const float InventoryScanInterval = 0.5f;
        private const float MountainScanInterval = 0.5f;
        private const float SaveInterval = 5f;
        private RunStore _store;
        private RunRecord _record;
        private float _nextSample;
        private float _nextCharacterRefresh;
        private float _nextInventoryScan;
        private float _nextMountainScan;
        private float _nextSave;
        private bool _sawVictory;
        private string _finalizedRunId;
        private readonly RecordingClock _clock = new RecordingClock();
        private float _nextClockAnchor;
        private readonly PerformanceWindow _captureTiming = new PerformanceWindow();
        private readonly PerformanceWindow _inventoryTiming = new PerformanceWindow();
        private readonly Dictionary<int, ItemFingerprint> _lastInventory = new Dictionary<int, ItemFingerprint>();
        private readonly Dictionary<int, int> _lastState = new Dictionary<int, int>();
        private readonly Dictionary<int, bool> _lastMountainReached = new Dictionary<int, bool>();
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
        private bool _mountainProgressInitialized;
        private string _mountainSignature;
        private readonly Dictionary<int, int> _observationContexts = new Dictionary<int, int>();
        private readonly Dictionary<int, float> _lastSyncReceived = new Dictionary<int, float>();

        private bool _collectionEnabled = true;
        private bool _resumePending;
        public bool CollectionEnabled
        {
            get { return _collectionEnabled; }
            set
            {
                if (_collectionEnabled && !value && _record != null) _resumePending = true;
                _collectionEnabled = value;
            }
        }

        public static RunCollector Instance { get; private set; }

        public void Initialize(RunStore store)
        {
            _store = store;
            Instance = this;
            _record = store.LoadActive();
            PrepareRecordMetadata();
            if (_record != null)
            {
                float end = 0;
                foreach (RunChunkInfo chunk in _record.chunks) end = Math.Max(end, chunk.endTime);
                if (_record.activeChunk != null) end = Math.Max(end, _record.activeChunk.endTime);
                _clock.Reset(end + .001f);
                CaptureClockAnchor("RecordingResumed");
            }
            _nextSample = 0f;
            _nextCharacterRefresh = 0f;
            _nextInventoryScan = 0f;
            _nextMountainScan = 0f;
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
                if (now >= _nextMountainScan)
                {
                    _nextMountainScan = now + MountainScanInterval;
                    CaptureMountainProgress();
                    CaptureEffectContext();
                }
                if (now >= _nextSave)
                {
                    _nextSave = now + SaveInterval;
                    if (now >= _nextClockAnchor)
                    {
                        CaptureClockAnchor("ClockAnchor"); _nextClockAnchor = now + 30;
                        string capture = _captureTiming.Take("collector"), inventory = _inventoryTiming.Take("inventory");
                        if (StateKeeperPlugin.IsDebugLogging)
                            StateKeeperPlugin.LogInfo("Performance | " + capture + " | " + inventory + " | process managed=" + GC.GetTotalMemory(false) + " bytes; GC collections=" + GC.CollectionCount(0));
                    }
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
            if (!CollectionEnabled) return;
            Guid runId = GetCurrentRunId();
            if (runId == Guid.Empty) return;
            string runIdText = runId.ToString();
            if (_record == null && string.Equals(_finalizedRunId, runIdText, StringComparison.OrdinalIgnoreCase)) return;
            if (_record != null && string.Equals(_record.header.runId, runIdText, StringComparison.OrdinalIgnoreCase))
            {
                if (_resumePending)
                {
                    _resumePending = false;
                    CaptureClockAnchor("RecordingResumed");
                    _lastInventory.Clear(); _lastState.Clear(); _observationContexts.Clear(); _lastSyncReceived.Clear();
                    _characterCacheReady = false; _nextCharacterRefresh = _nextInventoryScan = 0;
                }
                return;
            }

            if (_record != null && !string.IsNullOrEmpty(_record.header.runId))
            {
                _store.Complete(_record, RunStatus.Aborted, RunOutcome.Aborted);
            }

            _record = new RunRecord();
            _resumePending = false;
            _record.header.runId = runIdText;
            _record.header.startedUtc = DateTime.UtcNow.ToString("o");
            _record.header.lastSavedUtc = _record.header.startedUtc;
            try
            {
                _record.header.hasAscentLevel = true;
                _record.header.ascentLevel = Ascents.currentAscent;
                _record.header.hasCustomRun = true;
                _record.header.isCustomRun = RunSettings.IsCustomRun;
            }
            catch { }
            _record.statusTypeOrder = Enum.GetNames(typeof(CharacterAfflictions.STATUSTYPE));
            _record.collectionRevision = 3;
            _record.collectionCapabilities = new[] { "MonotonicClock", "PetrifyAmount", "ItemActor", "NestedDefinitions", "MushroomContext", "ObservationContext", "LocalRecipientTreatment", "BroadcastRescuePull", "ReviveTargetOnly" };
            _record.afflictionTypeOrder = Enum.GetNames(typeof(Affliction.AfflictionType));
            _record.distanceUnitsToMeters = CharacterStats.unitsToMeters;
            _record.definitions = ItemDefinitionCatalog.Capture();
            _sawVictory = false;
            _finalizedRunId = null;
            _clock.Reset(0); _nextClockAnchor = 0;
            _characterCacheReady = false;
            _nextCharacterRefresh = 0f;
            _nextInventoryScan = 0f;
            _nextMountainScan = 0f;
            _lastInventory.Clear();
            _lastState.Clear();
            _observationContexts.Clear();
            _lastMountainReached.Clear();
            _mountainProgressInitialized = false;
            _mountainSignature = null;
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
            return _clock.Time;
        }

        private void CaptureClockAnchor(string kind)
        {
            if (_record == null) return;
            try
            {
                float gameTime = RunManager.Instance == null ? -1 : RunManager.Instance.TimeSinceRunStarted;
                AddEvent(kind, EventSource.LocalAuthoritative, null, null, null, null, "MonotonicClock", gameTime, GetRunTime(), resourceKey: "gameRunTime");
            }
            catch { }
        }

        private void CaptureMountainProgress()
        {
            if (_record == null) return;
            MountainProgressHandler handler;
            try { handler = Singleton<MountainProgressHandler>.Instance; }
            catch { return; }
            if (handler == null || handler.progressPoints == null || handler.progressPoints.Length == 0) return;

            var definitions = new List<MountainSegmentDefinition>(handler.progressPoints.Length);
            var signature = new StringBuilder();
            for (int i = 0; i < handler.progressPoints.Length; i++)
            {
                MountainProgressHandler.ProgressPoint point = handler.progressPoints[i];
                if (point == null) continue;
                string capturedTitle = point.title;
                try { capturedTitle = point.localizedTitle; } catch { }
                bool hasBoundary = point.transform != null;
                float boundary = hasBoundary ? Round(point.transform.position.z, 3) : 0f;
                string biome = point.biome.ToString();
                definitions.Add(new MountainSegmentDefinition
                {
                    index = i,
                    titleKey = point.title,
                    capturedTitle = capturedTitle,
                    biomeKey = biome,
                    hasBoundaryZ = hasBoundary,
                    boundaryZ = boundary
                });
                signature.Append(i).Append('|').Append(point.title).Append('|').Append(biome).Append('|')
                    .Append(hasBoundary ? boundary.ToString("R", CultureInfo.InvariantCulture) : "none").Append(';');
            }

            string currentSignature = signature.ToString();
            if (!string.Equals(_mountainSignature, currentSignature, StringComparison.Ordinal))
            {
                _mountainSignature = currentSignature;
                _record.mountainSegments = definitions;
                _lastMountainReached.Clear();
                _mountainProgressInitialized = false;
            }

            int furthestReached = -1;
            for (int i = 0; i < handler.progressPoints.Length; i++)
            {
                MountainProgressHandler.ProgressPoint point = handler.progressPoints[i];
                bool reached = point != null && point.Reached;
                bool previous;
                if (_mountainProgressInitialized && _lastMountainReached.TryGetValue(i, out previous) && !previous && reached)
                    AddSegmentEvent("MountainSegmentReached", i, point, "ProgressAdvanced");
                _lastMountainReached[i] = reached;
                if (reached) furthestReached = i;
            }
            if (!_mountainProgressInitialized && furthestReached >= 0)
                AddSegmentEvent("MountainProgressObserved", furthestReached, handler.progressPoints[furthestReached], "InitialObservation");
            _mountainProgressInitialized = true;
        }

        private void AddSegmentEvent(string type, int segmentIndex, MountainProgressHandler.ProgressPoint point, string detail)
        {
            Character subject = null;
            try { subject = Character.localCharacter; } catch { }
            AddEvent(type, EventSource.LocalAuthoritative, subject, null, null, null, detail,
                segmentIndex, segmentIndex - 1, null, null, segmentIndex, point == null ? null : point.title);
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
            if (!CollectionEnabled) return;
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            try { CollectFrameCore(forceInventory); }
            finally { _captureTiming.Record(start); }
        }

        private void CollectFrameCore(bool forceInventory)
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
            CaptureObservationContext(character, playerIndex);
            var telemetry = new PlayerTelemetry
            {
                playerIndex = playerIndex,
                regularStamina = Round(character.data.currentStamina, 4),
                extraStamina = Round(character.data.extraStamina, 4),
                maxStamina = Round(character.GetMaxStamina(), 4),
                petrifyAmount = character.data.petrifyAmount,
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
                statuses = character.refs.afflictions == null ? null : new float[CharacterAfflictions.NumStatusTypes]
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

        internal void ObserveSync(Character character)
        {
            if (!CollectionEnabled) return;
            if (character != null && _record != null) _lastSyncReceived[character.GetInstanceID()] = Time.unscaledTime;
        }

        private void CaptureObservationContext(Character character, int playerIndex)
        {
            float last;
            bool known = character.IsLocal || _lastSyncReceived.TryGetValue(character.GetInstanceID(), out last);
            bool fresh = character.IsLocal || (_lastSyncReceived.TryGetValue(character.GetInstanceID(), out last) && Time.unscaledTime - last <= Math.Max(1f, 4f / Math.Max(1, Photon.Pun.PhotonNetwork.SerializationRate)));
            int context = (character.warping ? 1 : 0) | (character.data.isSkeleton ? 2 : 0) | (character.isZombie ? 4 : 0) | (known ? 8 : 0) | (fresh ? 16 : 0);
            int old;
            if (!_observationContexts.TryGetValue(playerIndex, out old) || old != context)
            {
                _observationContexts[playerIndex] = context;
                AddEvent("ObservationContextChanged", character.IsLocal ? EventSource.LocalAuthoritative : EventSource.RemoteObserved, character, null, null, null, "warp/skeleton/zombie/syncKnown/syncFresh", context, old);
            }
        }

        private void CaptureEffectContext()
        {
            if (_record == null || MushroomManager.instance == null) return;
            int[] effects = MushroomManager.instance.mushroomEffects, stamina = MushroomManager.instance.mushroomStamAmt;
            if (effects == null || stamina == null || effects.Length == 0 || effects.Length != stamina.Length) return;
            // The uninitialized manager contains zero-filled arrays, not a generated mapping.
            bool generated = false;
            for (int i = 0; i < effects.Length; i++) if (effects[i] != 0) { generated = true; break; }
            if (!generated) return;
            if (_record.effectContexts == null) _record.effectContexts = new List<RunEffectContext>();
            RunEffectContext previous = _record.effectContexts.Count == 0 ? null : _record.effectContexts[_record.effectContexts.Count - 1];
            bool same = previous != null && previous.mushroomEffects.Length == effects.Length;
            if (same) for (int i = 0; i < effects.Length; i++) if (previous.mushroomEffects[i] != effects[i] || previous.mushroomStaminaAmounts[i] != stamina[i]) { same = false; break; }
            if (!same) _record.effectContexts.Add(new RunEffectContext { observedTime = Round(GetRunTime(), 3), mushroomEffects = (int[])effects.Clone(), mushroomStaminaAmounts = (int[])stamina.Clone() });
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
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            try { CaptureInventoryCore(character, playerIndex, time); }
            finally { _inventoryTiming.Record(start); }
        }

        private void CaptureInventoryCore(Character character, int playerIndex, float time)
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
            ItemSnapshot currentItem = CaptureItem(slot, location);
            ItemSnapshot previousItem = SnapshotFromFingerprint(previous, location);
            string eventType;
            if (!current.occupied) eventType = "ItemRemovedObserved";
            else if (!previous.occupied || previous.itemId != current.itemId || previous.guid != current.guid) eventType = "ItemChangedObserved";
            else eventType = "ItemResourceChanged";
            EventSource source = character.IsLocal ? EventSource.LocalAuthoritative : EventSource.RemoteObserved;
            if (eventType == "ItemResourceChanged")
            {
                List<ResourceChange> changes = FindChangedResources(previous, current);
                foreach (ResourceChange change in changes)
                    AddEvent(eventType, source, character, null, currentItem, location, "inventory snapshot changed",
                        change.value, change.previousValue, previousItem, change.key);
            }
            else
            {
                AddEvent(eventType, source, character, null, current.occupied ? currentItem : previousItem, location,
                    "inventory snapshot changed", 0f, 0f, previousItem);
            }
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
            IntItemData petterItemUses;
            OptionableBoolItemData used;
            BoolItemData usedValue;
            BoolItemData flareActive;
            BoolItemData powerEnabled;
            IntItemData cooked;
            FloatItemData useRemaining;
            FloatItemData fuel;
            if (data.TryGetDataEntry<OptionableIntItemData>(DataEntryKey.ItemUses, out uses))
            {
                item.hasUsesEntry = true;
                item.hasUsesValue = uses.HasData;
                if (uses.HasData) item.uses = uses.Value;
            }
            if (data.TryGetDataEntry<IntItemData>(DataEntryKey.PetterItemUses, out petterItemUses)) { item.hasPetterItemUses = true; item.petterItemUses = petterItemUses.Value; }
            if (data.TryGetDataEntry<OptionableBoolItemData>(DataEntryKey.Used, out used)) { item.hasUsed = used.HasData; if (used.HasData) item.used = used.Value; }
            else if (data.TryGetDataEntry<BoolItemData>(DataEntryKey.Used, out usedValue)) { item.hasUsed = true; item.used = usedValue.Value; }
            if (data.TryGetDataEntry<BoolItemData>(DataEntryKey.FlareActive, out flareActive)) { item.hasFlareActive = true; item.flareActive = flareActive.Value; }
            if (data.TryGetDataEntry<BoolItemData>(DataEntryKey.PowerEnabled, out powerEnabled)) { item.hasPowerEnabled = true; item.powerEnabled = powerEnabled.Value; }
            if (data.TryGetDataEntry<FloatItemData>(DataEntryKey.UseRemainingPercentage, out useRemaining)) { item.hasUseRemaining = true; item.useRemaining = Round(useRemaining.Value, 4); }
            if (data.TryGetDataEntry<FloatItemData>(DataEntryKey.Fuel, out fuel)) { item.hasFuel = true; item.fuel = Round(fuel.Value, 4); }
            if (data.TryGetDataEntry<IntItemData>(DataEntryKey.CookedAmount, out cooked)) { item.hasCookedAmount = true; item.cookedAmount = cooked.Value; }
        }

        internal static ItemSnapshot CaptureItemResourcesForTests(ItemInstanceData data)
        {
            ItemSnapshot result = new ItemSnapshot();
            PopulateItemResources(data, result);
            return result;
        }

        public void MarkVictory()
        {
            HandleRunBinding();
            if (_record == null) return;
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
            if (!CollectionEnabled) return;
            HandleRunBinding();
            if (_record == null) return;
            string slot = null;
            if (subject != null && subject.player != null && item != null)
            {
                slot = FindItemSlot(subject.player, item);
            }
            AddEvent(type, source, subject, target, item == null ? null : ToItemSnapshot(item), slot, detail,
                0f, 0f, null, type == "ItemUsesReduced" ? "uses" : null, actor: item == null ? null : item.trueHolderCharacter);
        }

        internal void RecordItemOutcome(string type, Item item, Character subject, Character target, Character actor, float value, string detail, string resourceKey = null, float previousValue = 0f)
        {
            if (!CollectionEnabled) return;
            HandleRunBinding();
            if (_record == null || item == null) return;
            AddEvent(type, target != null && target.IsLocal ? EventSource.LocalAuthoritative : EventSource.RemoteObserved,
                subject, target, ToItemSnapshot(item), FindItemSlot(subject == null ? null : subject.player, item), detail, value, previousValue, null, resourceKey, -1, null, actor);
        }

        internal sealed class ItemEventCapture
        {
            internal ItemSnapshot item;
            internal Character subject, target, actor;
        }
        internal ItemEventCapture CaptureItemEvent(Item item, Character subject, Character target)
        {
            if (!CollectionEnabled) return null;
            return item == null ? null : new ItemEventCapture { item = ToItemSnapshot(item), subject = subject, target = target, actor = item.trueHolderCharacter };
        }
        internal void RecordCapturedItemEvent(string type, ItemEventCapture capture)
        {
            if (!CollectionEnabled) return;
            if (capture == null || _record == null) return;
            AddEvent(type, capture.subject != null && capture.subject.IsLocal ? EventSource.LocalAuthoritative : EventSource.RemoteObserved,
                capture.subject, capture.target, capture.item, null, type, actor: capture.actor);
        }

        public void RecordItemSlotEvent(string type, ItemSlot itemSlot, Character subject, EventSource source, string detail)
        {
            if (!CollectionEnabled) return;
            HandleRunBinding();
            if (_record == null) return;
            string slot = itemSlot == null ? null : SlotName(itemSlot.itemSlotID);
            AddEvent(type, source, subject, null, CaptureItem(itemSlot, slot), slot, detail);
        }

        private static string SlotName(byte slotId)
        {
            if (slotId < 3) return "main:" + slotId.ToString(CultureInfo.InvariantCulture);
            if (slotId == 3) return "backpack";
            if (slotId == 250) return "temporary";
            return "slot:" + slotId.ToString(CultureInfo.InvariantCulture);
        }

        public void RecordSimpleEvent(string type, Character subject, EventSource source, string detail)
        {
            if (!CollectionEnabled) return;
            HandleRunBinding();
            if (_record != null) AddEvent(type, source, subject, null, null, null, detail);
        }

        public void RecordJump(Character character)
        {
            if (!CollectionEnabled) return;
            HandleRunBinding();
            if (_record == null || character == null) return;
            AddEvent("PlayerJumped", character.IsLocal ? EventSource.LocalAuthoritative : EventSource.RemoteObserved,
                character, null, null, null, "Character.OnJump");
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
            float value = 0f, float previousValue = 0f, ItemSnapshot previousItem = null, string resourceKey = null,
            int segmentIndex = -1, string segmentKey = null, Character actor = null)
        {
            if (!CollectionEnabled || _record == null) return;
            _record.events.Add(new StatsEvent
            {
                time = Round(GetRunTime(), 3),
                type = type,
                source = source,
                subjectPlayerIndex = GetPlayerIndex(subject),
                targetPlayerIndex = GetPlayerIndex(target),
                actorPlayerIndex = actor == null ? (int?)null : GetPlayerIndex(actor),
                itemId = item == null ? (ushort)0 : item.itemId,
                itemName = item == null ? null : item.itemName,
                itemGuid = item == null ? null : item.guid,
                previousItemGuid = previousItem == null ? null : previousItem.guid,
                resourceKey = resourceKey,
                definitionKey = item == null ? null : item.prefabName,
                slot = slot,
                detail = detail,
                value = value,
                previousValue = previousValue,
                segmentIndex = segmentIndex,
                segmentKey = segmentKey
            });
        }

        private int GetPlayerIndex(Character character)
        {
            if (character == null || _record == null) return -1;
            try { return EnsureIdentity(character).playerIndex; }
            catch { return -1; }
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
            public string itemName;
            public string prefabName;
            public bool hasUsesEntry;
            public bool hasUsesValue;
            public int uses;
            public bool hasPetterItemUses;
            public int petterItemUses;
            public bool hasUsed;
            public bool used;
            public bool hasFlareActive;
            public bool flareActive;
            public bool hasPowerEnabled;
            public bool powerEnabled;
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
                result.itemName = slot.prefab.UIData == null ? slot.prefab.name : slot.prefab.UIData.itemName;
                result.prefabName = slot.prefab.gameObject == null ? slot.prefab.name : slot.prefab.gameObject.name;
                if (slot.data == null) return result;
                result.guid = slot.data.guid;

                OptionableIntItemData uses;
                IntItemData petterItemUses;
                OptionableBoolItemData used;
                BoolItemData usedValue;
                BoolItemData flareActive;
                BoolItemData powerEnabled;
                IntItemData cooked;
                FloatItemData useRemaining;
                FloatItemData fuel;
                if (slot.data.TryGetDataEntry<OptionableIntItemData>(DataEntryKey.ItemUses, out uses)) { result.hasUsesEntry = true; result.hasUsesValue = uses.HasData; if (uses.HasData) result.uses = uses.Value; }
                if (slot.data.TryGetDataEntry<IntItemData>(DataEntryKey.PetterItemUses, out petterItemUses)) { result.hasPetterItemUses = true; result.petterItemUses = petterItemUses.Value; }
                if (slot.data.TryGetDataEntry<OptionableBoolItemData>(DataEntryKey.Used, out used)) { result.hasUsed = used.HasData; if (used.HasData) result.used = used.Value; }
                else if (slot.data.TryGetDataEntry<BoolItemData>(DataEntryKey.Used, out usedValue)) { result.hasUsed = true; result.used = usedValue.Value; }
                if (slot.data.TryGetDataEntry<BoolItemData>(DataEntryKey.FlareActive, out flareActive)) { result.hasFlareActive = true; result.flareActive = flareActive.Value; }
                if (slot.data.TryGetDataEntry<BoolItemData>(DataEntryKey.PowerEnabled, out powerEnabled)) { result.hasPowerEnabled = true; result.powerEnabled = powerEnabled.Value; }
                if (slot.data.TryGetDataEntry<FloatItemData>(DataEntryKey.UseRemainingPercentage, out useRemaining)) { result.hasUseRemaining = true; result.useRemaining = Round(useRemaining.Value, 4); }
                if (slot.data.TryGetDataEntry<FloatItemData>(DataEntryKey.Fuel, out fuel)) { result.hasFuel = true; result.fuel = Round(fuel.Value, 4); }
                if (slot.data.TryGetDataEntry<IntItemData>(DataEntryKey.CookedAmount, out cooked)) { result.hasCookedAmount = true; result.cookedAmount = cooked.Value; }
                return result;
            }

            public bool Equals(ItemFingerprint other)
            {
                return occupied == other.occupied && itemId == other.itemId && guid == other.guid &&
                       string.Equals(itemName, other.itemName, StringComparison.Ordinal) && string.Equals(prefabName, other.prefabName, StringComparison.Ordinal) &&
                       hasUsesEntry == other.hasUsesEntry && hasUsesValue == other.hasUsesValue && uses == other.uses &&
                       hasPetterItemUses == other.hasPetterItemUses && petterItemUses == other.petterItemUses &&
                       hasUsed == other.hasUsed && used == other.used &&
                       hasFlareActive == other.hasFlareActive && flareActive == other.flareActive &&
                       hasPowerEnabled == other.hasPowerEnabled && powerEnabled == other.powerEnabled &&
                       hasUseRemaining == other.hasUseRemaining && useRemaining.Equals(other.useRemaining) &&
                       hasFuel == other.hasFuel && fuel.Equals(other.fuel) &&
                       hasCookedAmount == other.hasCookedAmount && cookedAmount == other.cookedAmount;
            }

            public ItemSnapshot ToSnapshot(string location)
            {
                return new ItemSnapshot
                {
                    slot = location,
                    itemId = itemId,
                    itemName = itemName,
                    prefabName = prefabName,
                    guid = guid == Guid.Empty ? null : guid.ToString(),
                    hasUsesEntry = hasUsesEntry,
                    hasUsesValue = hasUsesValue,
                    uses = uses,
                    hasPetterItemUses = hasPetterItemUses,
                    petterItemUses = petterItemUses,
                    hasUsed = hasUsed,
                    used = used,
                    hasFlareActive = hasFlareActive,
                    flareActive = flareActive,
                    hasPowerEnabled = hasPowerEnabled,
                    powerEnabled = powerEnabled,
                    hasUseRemaining = hasUseRemaining,
                    useRemaining = useRemaining,
                    hasFuel = hasFuel,
                    fuel = fuel,
                    hasCookedAmount = hasCookedAmount,
                    cookedAmount = cookedAmount
                };
            }
        }

        private static ItemSnapshot SnapshotFromFingerprint(ItemFingerprint fingerprint, string location)
        {
            return fingerprint.occupied ? fingerprint.ToSnapshot(location) : new ItemSnapshot { slot = location };
        }

        private static List<ResourceChange> FindChangedResources(ItemFingerprint previous, ItemFingerprint current)
        {
            var result = new List<ResourceChange>(4);
            if (previous.hasUsesValue && current.hasUsesValue && previous.uses != current.uses) result.Add(new ResourceChange("uses", previous.uses, current.uses));
            if (previous.hasPetterItemUses && current.hasPetterItemUses && previous.petterItemUses != current.petterItemUses) result.Add(new ResourceChange("petterItemUses", previous.petterItemUses, current.petterItemUses));
            if (previous.hasUsed && current.hasUsed && previous.used != current.used) result.Add(new ResourceChange("used", previous.used ? 1f : 0f, current.used ? 1f : 0f));
            if (previous.hasFlareActive && current.hasFlareActive && previous.flareActive != current.flareActive) result.Add(new ResourceChange("flareActive", previous.flareActive ? 1f : 0f, current.flareActive ? 1f : 0f));
            if (previous.hasPowerEnabled && current.hasPowerEnabled && previous.powerEnabled != current.powerEnabled) result.Add(new ResourceChange("powerEnabled", previous.powerEnabled ? 1f : 0f, current.powerEnabled ? 1f : 0f));
            if (previous.hasUseRemaining && current.hasUseRemaining && !previous.useRemaining.Equals(current.useRemaining)) result.Add(new ResourceChange("useRemaining", previous.useRemaining, current.useRemaining));
            if (previous.hasFuel && current.hasFuel && !previous.fuel.Equals(current.fuel)) result.Add(new ResourceChange("fuel", previous.fuel, current.fuel));
            if (previous.hasCookedAmount && current.hasCookedAmount && previous.cookedAmount != current.cookedAmount) result.Add(new ResourceChange("cookedAmount", previous.cookedAmount, current.cookedAmount));
            return result;
        }

        private struct ResourceChange
        {
            public readonly string key;
            public readonly float previousValue;
            public readonly float value;

            public ResourceChange(string key, float previousValue, float value)
            {
                this.key = key;
                this.previousValue = previousValue;
                this.value = value;
            }
        }
    }
}
