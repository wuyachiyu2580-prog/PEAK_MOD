using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace StateKeeper
{
    // Pure-data engine. Unity objects never cross into this background pipeline.
    internal sealed class RunAnalysisEngine
    {
        internal const float GapLimit = 1f;
        private readonly RunRecord _manifest;
        internal readonly AnalysisResult Result;
        private readonly Dictionary<int, AnalysisPlayer> _players;
        private readonly Dictionary<int, PlayerTelemetry> _previous = new Dictionary<int, PlayerTelemetry>();
        private readonly ItemAttributionEngine _attribution;
        private readonly LifecycleAnalysis _lifecycle;
        private readonly ReportAnalysis _report;
        private EvidenceReference _evidence;
        private float _sourceSampleTime = float.NaN;
        private readonly Dictionary<int, PlayerTelemetry> _positions = new Dictionary<int, PlayerTelemetry>();
        private readonly Dictionary<int, float> _positionTimes = new Dictionary<int, float>();
        private readonly HashSet<int> _recoveringPositions = new HashSet<int>();
        private readonly Dictionary<string, QualityObservation> _lastDiagnostics = new Dictionary<string, QualityObservation>();
        private readonly Dictionary<int, Dictionary<string, ItemSnapshot>> _inventory = new Dictionary<int, Dictionary<string, ItemSnapshot>>();
        private readonly Dictionary<string, Removal> _removed = new Dictionary<string, Removal>();
        private readonly Dictionary<string, Arrival> _arrivals = new Dictionary<string, Arrival>();
        private readonly Dictionary<string, AnalysisDistancePair> _pairs = new Dictionary<string, AnalysisDistancePair>();
        private readonly Dictionary<string, float> _lastDistances = new Dictionary<string, float>();
        private readonly Dictionary<int, AnalysisIsolationEpisode> _isolation = new Dictionary<int, AnalysisIsolationEpisode>();
        private readonly Dictionary<int, float> _isolationLast = new Dictionary<int, float>();
        private readonly Dictionary<int, Trend> _trends = new Dictionary<int, Trend>();
        private readonly Dictionary<int, float> _reached = new Dictionary<int, float>();
        private readonly HashSet<int> _initialReached = new HashSet<int>();
        private readonly HashSet<int> _derived = new HashSet<int>();
        private readonly Dictionary<string, AnalysisItemObservation> _recentEvidence = new Dictionary<string, AnalysisItemObservation>();
        private readonly HashSet<int> _blockedPlayers = new HashSet<int>();
        private float _lastTime = float.NaN;
        private int _epoch;
        private readonly float _binWidth;
        private readonly int _local;
        private bool _firstSample = true;

        internal RunAnalysisEngine(RunRecord manifest)
        {
            _manifest = manifest;
            Result = new AnalysisResult
            {
                schemaVersion = manifest.schemaVersion, runId = manifest.header.runId,
                collectionCapabilities = manifest.collectionCapabilities ?? new string[0],
                sourceLastSavedUtc = manifest.header.lastSavedUtc,
                sourceChunkCount = manifest.chunks.Count,
                sourceSampleCount = manifest.chunks.Sum(c => c?.sampleCount ?? 0),
                sourceInventorySnapshotCount = manifest.chunks.Sum(c => c?.inventorySnapshotCount ?? 0),
                sourceEventCount = manifest.chunks.Sum(c => c?.eventCount ?? 0),
                definitions = manifest.definitions ?? new List<ItemDefinition>(),
                mountainSegments = manifest.mountainSegments ?? new List<MountainSegmentDefinition>(),
                statusTypeOrder = manifest.statusTypeOrder ?? new string[0],
                afflictionTypeOrder = manifest.afflictionTypeOrder ?? new[] { "PoisonOverTime", "InfiniteStamina", "FasterBoi", "Exhausted", "Glowing", "ColdOverTime", "Chaos", "AdjustStatus", "ClearAllStatus", "PreventPoisonHealing", "AddBonusStamina", "DrowsyOverTime", "AdjustStatusOverTime", "Sunscreen", "BingBongShield", "ZombieBite", "Invincibility", "LowGravity", "Blind", "Numb", "ClimbingChalk", "NoHunger", "HealAll", "DoubleJumpAmulet", "RadiateInfiniteStam", "MassSuperJump" },
                hasAscentLevel = manifest.header.hasAscentLevel, ascentLevel = manifest.header.ascentLevel,
                hasCustomRun = manifest.header.hasCustomRun, isCustomRun = manifest.header.isCustomRun,
                gameVersion = manifest.header.gameVersion
            };
            Result.overview.outcome = manifest.header.outcome;
            foreach (PlayerIdentity p in manifest.players ?? new List<PlayerIdentity>())
                if (p != null && !Result.players.Any(x => x.playerIndex == p.playerIndex))
                    Result.players.Add(new AnalysisPlayer { playerIndex = p.playerIndex, stableUserId = ReportComparison.ValidSteamId(p.userId) ? p.userId : null, displayName = Regex.Replace(p.displayName ?? "", "<[^>]*>", ""), staminaMin = float.PositiveInfinity });
            _players = Result.players.ToDictionary(p => p.playerIndex);
            _local = (manifest.players ?? new List<PlayerIdentity>()).Where(p => p.isLocal).Select(p => p.playerIndex).DefaultIfEmpty(-1).First();
            Result.recordingUserId = manifest.players?.FirstOrDefault(p => p.isLocal)?.userId;
            _binWidth = Math.Max(1f, manifest.chunks.Where(c => c != null).Select(c => c.endTime).DefaultIfEmpty(0f).Max() / 1200f);
            Result.overview.playerCount = _players.Count;
            Result.overview.mountainSegmentCount = Result.mountainSegments.Count;
            _attribution = new ItemAttributionEngine(manifest, Result);
            _lifecycle = new LifecycleAnalysis(Result);
            _report = new ReportAnalysis(Result);
        }

        internal void Break(string reason)
        {
            foreach (int id in _isolation.Keys.ToArray()) EndIsolation(id, reason);
            foreach (Trend trend in _trends.Values) trend.Finish();
            _attribution.Reset();
            _lifecycle.Interrupt(); _report.Interrupt();
            _positions.Clear(); _positionTimes.Clear(); _recoveringPositions.Clear();
            _previous.Clear(); _lastDistances.Clear();
            _inventory.Clear(); _removed.Clear(); _arrivals.Clear(); _recentEvidence.Clear();
            _lastTime = float.NaN; _epoch++;
        }

        internal void AddChunk(RunChunk chunk, CancellationToken token)
        {
            var blocks = new List<List<Entry>> { new List<Entry>() };
            var resets = new HashSet<int>();
            int index = 0;
            foreach (PlayerSample sample in chunk.samples ?? new List<PlayerSample>())
            {
                token.ThrowIfCancellationRequested();
                if (sample != null && Finite(sample.time))
                {
                    if (Finite(_sourceSampleTime) && sample.time <= _sourceSampleTime)
                    {
                        Result.quality.timeBackwardsCount++;
                        if (blocks[blocks.Count - 1].Count > 0) blocks.Add(new List<Entry>());
                        resets.Add(blocks.Count - 1);
                    }
                    _sourceSampleTime = sample.time;
                    blocks[blocks.Count - 1].Add(new Entry { time = sample.time, sample = sample, index = index });
                }
                index++;
            }
            var loose = new List<Entry>();
            index = 0;
            foreach (InventorySnapshot s in chunk.inventorySnapshots ?? new List<InventorySnapshot>())
            { if (s != null) loose.Add(new Entry { time = s.time, inventory = s, index = index }); index++; }
            index = 0;
            foreach (StatsEvent e in chunk.events ?? new List<StatsEvent>())
            { if (e != null) loose.Add(new Entry { time = e.time, ev = e, index = index }); index++; }
            var ranges = blocks.Select(b => Tuple.Create(b.Count == 0 ? chunk.startTime : b[0].time, b.Count == 0 ? chunk.endTime : b[b.Count - 1].time)).ToList();
            var ambiguous = new List<Entry>();
            foreach (Entry entry in loose)
            {
                var matches = Enumerable.Range(0, blocks.Count).Where(i => entry.time >= (i == 0 ? Math.Min(chunk.startTime, ranges[i].Item1) : ranges[i].Item1) &&
                    entry.time <= (i == blocks.Count - 1 ? Math.Max(chunk.endTime, ranges[i].Item2) : ranges[i].Item2)).ToList();
                if (blocks.Count == 1 && resets.Count == 0) blocks[0].Add(entry);
                else if (matches.Count == 1) blocks[matches[0]].Add(entry);
                else ambiguous.Add(entry);
            }
            for (int b = 0; b < blocks.Count; b++)
            {
                if (resets.Contains(b)) Break("ClockReset");
                foreach (Entry entry in blocks[b].OrderBy(e => e.time).ThenBy(e => e.ev != null ? 0 : e.inventory != null ? 1 : 2)) ProcessEntry(chunk.sequence, entry, true, token);
            }
            foreach (Entry entry in ambiguous) ProcessEntry(chunk.sequence, entry, false, token);
        }

        private void ProcessEntry(int chunk, Entry entry, bool associate, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (!Finite(entry.time)) { Result.quality.nonFiniteValueCount++; return; }
            _evidence = new EvidenceReference { runId = Result.runId, epoch = associate ? _epoch : -1, chunk = chunk,
                stream = entry.sample != null ? "samples" : entry.ev != null ? "events" : "inventorySnapshots", index = entry.index };
            if (entry.sample != null) AddSample(entry.sample);
            else if (entry.inventory != null)
            {
                Result.processedInventoryCount++;
                if (associate) AddInventory(entry.inventory);
                else
                {
                    foreach (var item in entry.inventory.slots ?? new List<ItemSnapshot>())
                        if (item != null) AddItem(Row(entry.time, entry.inventory.playerIndex, item, "InventoryUnplaced", "Ambiguous"), false);
                    Diagnose(entry.time, entry.inventory.playerIndex, "ClockAmbiguous");
                    _inventory.Remove(entry.inventory.playerIndex);
                }
            }
            else AddEvent(entry.ev, associate);
        }

        private void Diagnose(float time, int player, string reason, PlayerTelemetry p = null, float? distance = null)
        {
            string key = _evidence.epoch + ":" + player + ":" + reason;
            QualityObservation last; _lastDiagnostics.TryGetValue(key, out last);
            if (last != null && last.playerIndex == player && last.reason == reason && last.evidence.epoch == _evidence.epoch && time >= last.endTime && time - last.endTime <= GapLimit && !distance.HasValue)
            { last.endTime = time; last.count++; return; }
            var observation = new QualityObservation { evidence = _evidence, time = time, endTime = time, playerIndex = player, reason = reason,
                x = p?.positionX ?? 0, y = p?.positionY ?? 0, z = p?.positionZ ?? 0, distance = distance };
            Result.diagnostics.Add(observation); _lastDiagnostics[key] = observation;
        }

        private void InterruptPlayer(int id, float time)
        {
            _lifecycle.Interrupt(id); _report.Interrupt(id); _attribution.Interrupt(id, time);
            _previous.Remove(id); _positions.Remove(id); _positionTimes.Remove(id); _recoveringPositions.Remove(id);
            EndIsolation(id, "ObservationInterrupted");
            Trend trend; if (_trends.TryGetValue(id, out trend)) trend.Finish();
            foreach (string key in _lastDistances.Keys.Where(k => k.StartsWith(id + ":", StringComparison.Ordinal) || k.EndsWith(":" + id, StringComparison.Ordinal)).ToArray()) _lastDistances.Remove(key);
        }

        private void AddSample(PlayerSample sample)
        {
            if (sample == null || !Finite(sample.time)) { Result.quality.nonFiniteValueCount++; Break("InvalidTime"); return; }
            float dt = sample.time - _lastTime;
            if (Finite(_lastTime) && dt <= 0) { Result.quality.timeBackwardsCount++; Break("ClockReset"); }
            else if (Finite(_lastTime) && dt > GapLimit) { Result.quality.gapCount++; Break("ObservationGap"); }
            if (_evidence != null) _evidence.epoch = _epoch;
            dt = sample.time - _lastTime;
            bool continuous = Finite(dt) && dt > 0 && dt <= GapLimit;
            Result.overview.durationSeconds = Math.Max(Result.overview.durationSeconds, sample.time);
            var current = new Dictionary<int, PlayerTelemetry>();
            foreach (PlayerTelemetry p in sample.players ?? new List<PlayerTelemetry>())
            {
                if (p == null || !_players.ContainsKey(p.playerIndex)) continue;
                if (_blockedPlayers.Contains(p.playerIndex)) { Diagnose(sample.time, p.playerIndex, "ObservationInterrupted", p); continue; }
                if (p.dead) { _lifecycle.Sample(sample.time, p, _evidence); current[p.playerIndex] = p; _positions.Remove(p.playerIndex); continue; }
                if (!ValidPosition(p) || !ValidStamina(p) || (p.statuses != null && p.statuses.Any(v => !Finite(v))))
                { Result.quality.invalidPositionCount++; Diagnose(sample.time, p.playerIndex, "InvalidPosition", p); InterruptPlayer(p.playerIndex, sample.time); _recoveringPositions.Add(p.playerIndex); continue; }
                PlayerTelemetry oldPosition; float oldTime;
                bool hasPosition = _positions.TryGetValue(p.playerIndex, out oldPosition) && _positionTimes.TryGetValue(p.playerIndex, out oldTime);
                bool jump = hasPosition && sample.time > _positionTimes[p.playerIndex] && sample.time - _positionTimes[p.playerIndex] <= GapLimit &&
                    WorldDistance(p, oldPosition) > Math.Max(20f, 60f * (sample.time - _positionTimes[p.playerIndex]));
                _positions[p.playerIndex] = p; _positionTimes[p.playerIndex] = sample.time;
                if (jump)
                {
                    InterruptPlayer(p.playerIndex, sample.time); _positions[p.playerIndex] = p; _positionTimes[p.playerIndex] = sample.time;
                    _recoveringPositions.Add(p.playerIndex); Diagnose(sample.time, p.playerIndex, "PositionDiscontinuity", p); continue;
                }
                if (_recoveringPositions.Remove(p.playerIndex)) { Diagnose(sample.time, p.playerIndex, "PositionRecovery", p); continue; }
                _lifecycle.Sample(sample.time, p, _evidence);
                current[p.playerIndex] = p;
            }
            _report.Sample(sample.time, current, _evidence);
            foreach (int id in _previous.Keys.ToArray()) if (!current.ContainsKey(id))
            { Result.quality.missingPlayerSampleCount++; _lifecycle.Interrupt(id); EndIsolation(id, "Unavailable"); }
            foreach (PlayerTelemetry p in current.Values)
            {
                AnalysisPlayer player = _players[p.playerIndex];
                PlayerTelemetry before;
                bool hasBefore = _previous.TryGetValue(p.playerIndex, out before);
                if (continuous && hasBefore) player.observedSeconds += dt;
                if (!ValidPosition(p)) Result.quality.invalidPositionCount++;
                if (!ValidStamina(p)) { Result.quality.nonFiniteValueCount++; continue; }
                float total = p.regularStamina + p.extraStamina;
                if (p.regularStamina > p.maxStamina + .002f || p.regularStamina < 0 || p.extraStamina < 0) Result.quality.outOfRangeStaminaCount++;
                bool liveInterval = continuous && hasBefore && !p.dead && !before.dead && ValidStamina(before);
                if (!p.dead) { player.staminaMin = Math.Min(player.staminaMin, total); player.staminaMax = Math.Max(player.staminaMax, total); }
                if (liveInterval) { player.aliveSeconds += dt; player.staminaAverage += (before.regularStamina + before.extraStamina) * dt; }
                AnalysisSeriesPoint point = player.staminaSeries.LastOrDefault();
                bool broken = !continuous || !hasBefore || before.dead != p.dead;
                if (point == null || point.epoch != _epoch || point.dead != p.dead || (int)(sample.time / _binWidth) != (int)(point.time / _binWidth) || broken)
                {
                    point = new AnalysisSeriesPoint { evidence = _evidence, time = sample.time, endTime = sample.time, epoch = _epoch, breakBefore = broken, minimum = total, maximum = total,
                        regularMinimum = p.regularStamina, regularMaximum = p.regularStamina, extraMinimum = p.extraStamina, extraMaximum = p.extraStamina };
                    player.staminaSeries.Add(point);
                }
                point.endTime = sample.time; point.sampleCount++; point.dead = p.dead;
                point.regularStamina = p.regularStamina; point.extraStamina = p.extraStamina; point.totalStamina = total;
                point.maxStamina = p.maxStamina; point.normalizedStamina = p.maxStamina > 0 ? p.regularStamina / p.maxStamina : 0;
                point.minimum = Math.Min(point.minimum, total); point.maximum = Math.Max(point.maximum, total);
                point.regularMinimum = Math.Min(point.regularMinimum, p.regularStamina); point.regularMaximum = Math.Max(point.regularMaximum, p.regularStamina);
                point.extraMinimum = Math.Min(point.extraMinimum, p.extraStamina); point.extraMaximum = Math.Max(point.extraMaximum, p.extraStamina);
                point.petrifyAmount = p.petrifyAmount;
                point.statuses = p.statuses != null && p.statuses.All(Finite) ? p.statuses : null;
                point.afflictions = p.activeAfflictionTypes;
                if (continuous && hasBefore) point.observedSeconds += dt;
                if (liveInterval)
                {
                    point.validSeconds += dt; point.totalIntegral += dt * (before.regularStamina + before.extraStamina);
                    if (point.movementSeconds == null) point.movementSeconds = new float[ReportAnalysis.MovementTypes.Length];
                    if (point.statusIntegrals == null) point.statusIntegrals = new float[Result.statusTypeOrder.Length];
                    point.movementSeconds[Array.IndexOf(ReportAnalysis.MovementTypes, ReportAnalysis.Movement(before))] += dt;
                    for (int i = 0; i < Math.Min(point.statusIntegrals.Length, before.statuses?.Length ?? 0); i++)
                        if (Finite(before.statuses[i])) point.statusIntegrals[i] += Math.Max(0, before.statuses[i]) * dt;
                }
                Trend trend;
                if (!_trends.TryGetValue(p.playerIndex, out trend)) _trends[p.playerIndex] = trend = new Trend(player);
                if (!liveInterval) trend.Finish();
                if (!p.dead) trend.Add(sample.time, total);
            }
            var nearest = new Dictionary<int, float>();
            var distances = new Dictionary<string, float>();
            float span = 0;
            for (int i = 0; i < (sample.distanceMeters?.Length ?? 0); i++)
            {
                if (sample.distancePlayerPairs == null || i * 2 + 1 >= sample.distancePlayerPairs.Length) { Result.quality.excludedDistanceCount++; continue; }
                int a = sample.distancePlayerPairs[i * 2], b = sample.distancePlayerPairs[i * 2 + 1];
                PlayerTelemetry pa, pb; float d = sample.distanceMeters[i];
                if (a == b || !current.TryGetValue(a, out pa) || !current.TryGetValue(b, out pb) || !ValidPosition(pa) || !ValidPosition(pb) || !Finite(d) || d < 0)
                { Result.quality.excludedDistanceCount++; continue; }
                if (_manifest.distanceUnitsToMeters.HasValue)
                {
                    float expected = WorldDistance(pa, pb) * _manifest.distanceUnitsToMeters.Value;
                    if (Math.Abs(expected - d) > Math.Max(.1f, expected * .01f))
                    { Result.quality.excludedDistanceCount++; Diagnose(sample.time, a, "DistanceMismatch", pa, d); continue; }
                }
                string key = Math.Min(a, b) + ":" + Math.Max(a, b);
                if (distances.ContainsKey(key)) continue;
                distances[key] = d;
                AnalysisDistancePair pair;
                if (!_pairs.TryGetValue(key, out pair)) _pairs[key] = pair = new AnalysisDistancePair { playerA = Math.Min(a, b), playerB = Math.Max(a, b), nearestObserved = d };
                pair.sampleCount++; pair.max = Math.Max(pair.max, d); pair.nearestObserved = Math.Min(pair.nearestObserved, d);
                float old; PlayerTelemetry oa, ob;
                bool validInterval = continuous && _lastDistances.TryGetValue(key, out old) && _previous.TryGetValue(a, out oa) && _previous.TryGetValue(b, out ob) && ValidPosition(oa) && ValidPosition(ob);
                if (validInterval) { pair.intervals.Add(_lastTime); pair.intervals.Add(dt); pair.intervals.Add(_lastDistances[key]); pair.intervalEpochs.Add(_epoch); }
                AddDistancePoint(pair.series, sample.time, d, !validInterval);
                _report.Pair(sample.time, a, b, d, validInterval, _evidence);
                nearest[a] = nearest.ContainsKey(a) ? Math.Min(nearest[a], d) : d;
                nearest[b] = nearest.ContainsKey(b) ? Math.Min(nearest[b], d) : d;
                span = Math.Max(span, d);
            }
            foreach (AnalysisPlayer player in Result.players)
            {
                float d;
                if (!nearest.TryGetValue(player.playerIndex, out d)) { EndIsolation(player.playerIndex, "Unavailable"); continue; }
                bool broken = !continuous || !_previous.ContainsKey(player.playerIndex) || !ValidPosition(_previous[player.playerIndex]);
                AddDistancePoint(player.nearestSeries, sample.time, d, broken);
                if (broken) EndIsolation(player.playerIndex, "ObservationGap");
                AnalysisIsolationEpisode iso;
                if (!_isolation.TryGetValue(player.playerIndex, out iso) && d > 100) _isolation[player.playerIndex] = iso = new AnalysisIsolationEpisode { evidence = _evidence, startTime = sample.time, peakNearestDistance = d };
                if (iso != null) { iso.peakNearestDistance = Math.Max(iso.peakNearestDistance, d); _isolationLast[player.playerIndex] = sample.time; if (d < 80) EndIsolation(player.playerIndex, "Rejoined"); }
            }
            if (distances.Count > 0) AddDistancePoint(Result.teamSpan, sample.time, span, !continuous || _lastDistances.Count == 0);
            PlayerTelemetry local;
            if (current.TryGetValue(_local, out local) && ValidPosition(local))
                foreach (MountainSegmentDefinition segment in Result.mountainSegments.Where(s => s != null && s.hasBoundaryZ))
                    if (!_reached.ContainsKey(segment.index) && local.positionZ >= segment.boundaryZ)
                    { if (_firstSample) _initialReached.Add(segment.index); else { _reached[segment.index] = sample.time; _derived.Add(segment.index); } }
            _firstSample = false;
            _previous.Clear(); foreach (var p in current) _previous[p.Key] = p.Value;
            _lastDistances.Clear(); foreach (var p in distances) _lastDistances[p.Key] = p.Value;
            _lastTime = sample.time;
            _attribution.Sample(new PlayerSample { time = sample.time, players = current.Values.ToList() }, _epoch);
        }

        private void AddDistancePoint(List<AnalysisDistancePoint> points, float time, float value, bool broken)
        {
            AnalysisDistancePoint last = points.LastOrDefault();
            if (last == null || broken || last.epoch != _epoch || time - last.endTime > GapLimit || (int)(time / _binWidth) != (int)(last.time / _binWidth))
            { last = new AnalysisDistancePoint { evidence = _evidence, time = time, epoch = _epoch, minimum = value, breakBefore = broken || last == null || time - last.endTime > GapLimit }; points.Add(last); }
            last.value = (last.value * last.count + value) / (last.count + 1); last.count++; last.endTime = time; last.maximum = Math.Max(last.maximum, value); last.minimum = Math.Min(last.minimum, value);
        }

        private void EndIsolation(int id, string reason)
        {
            AnalysisIsolationEpisode iso; float last;
            if (!_isolation.TryGetValue(id, out iso)) return;
            if (_isolationLast.TryGetValue(id, out last) && last - iso.startTime >= 10)
            {
                iso.endTime = last; iso.endReason = reason; _players[id].isolationEpisodes.Add(iso);
                _players[id].isolatedSeconds += last - iso.startTime;
                Result.episodes.Add(new ReportEpisode { evidence = iso.evidence, playerIndex = id, start = iso.startTime, end = last, activeSeconds = last - iso.startTime, peak = iso.peakNearestDistance, kind = "Isolation" });
            }
            _isolation.Remove(id); _isolationLast.Remove(id);
        }

        private void AddInventory(InventorySnapshot snapshot)
        {
            if (!_players.ContainsKey(snapshot.playerIndex)) return;
            Dictionary<string, ItemSnapshot> previous;
            if (!_inventory.TryGetValue(snapshot.playerIndex, out previous)) previous = new Dictionary<string, ItemSnapshot>(StringComparer.OrdinalIgnoreCase);
            var current = new Dictionary<string, ItemSnapshot>(StringComparer.OrdinalIgnoreCase);
            var conflicts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ItemSnapshot item in snapshot.slots ?? new List<ItemSnapshot>())
            {
                if (item == null || string.IsNullOrEmpty(item.guid)) continue;
                if (current.ContainsKey(item.guid)) { Result.quality.warnings.Add("Duplicate inventory GUID at " + snapshot.time); conflicts.Add(item.guid); continue; }
                if (_inventory.Any(p => p.Key != snapshot.playerIndex && p.Value.ContainsKey(item.guid))) conflicts.Add(item.guid);
                current[item.guid] = item;
            }
            foreach (var entry in previous) if (!current.ContainsKey(entry.Key))
            {
                _removed[entry.Key] = new Removal { time = snapshot.time, player = snapshot.playerIndex };
                AddItem(Row(snapshot.time, snapshot.playerIndex, entry.Value, "ItemRemovedObserved", "Ambiguous"));
                Arrival arrival;
                if (_arrivals.TryGetValue(entry.Key, out arrival) && arrival.player != snapshot.playerIndex && snapshot.time >= arrival.time && snapshot.time - arrival.time <= 2 &&
                    _inventory.Count(p => p.Key != snapshot.playerIndex && p.Value.ContainsKey(entry.Key)) == 1 &&
                    _inventory.TryGetValue(arrival.player, out Dictionary<string, ItemSnapshot> receiving) && receiving.ContainsKey(entry.Key))
                {
                    var transfer = Row(snapshot.time, arrival.player, arrival.item, "ItemTransferObserved", "Likely");
                    transfer.actorPlayerIndex = snapshot.playerIndex; transfer.actorInferred = true; transfer.targetPlayerIndex = arrival.player;
                    AddItem(transfer);
                }
            }
            foreach (var entry in current)
            {
                ItemSnapshot before;
                if (previous.TryGetValue(entry.Key, out before))
                {
                    if (before.slot != entry.Value.slot) AddItem(Row(snapshot.time, snapshot.playerIndex, entry.Value, "ItemMoved", "Certain"));
                    foreach (ResourceDelta delta in Resources(before, entry.Value))
                    {
                        var row = Row(snapshot.time, snapshot.playerIndex, entry.Value, "ResourceChanged", "Certain");
                        row.resourceKey = delta.key; row.previousValue = delta.before; row.value = delta.after;
                        if (conflicts.Contains(entry.Key)) row.detail = "ConflictingGuid";
                        AddItem(row);
                    }
                }
                else
                {
                    _arrivals[entry.Key] = new Arrival { time = snapshot.time, player = snapshot.playerIndex, item = entry.Value };
                    var row = Row(snapshot.time, snapshot.playerIndex, entry.Value, "ItemAppeared", "Certain");
                    Removal removed;
                    bool conflict = _inventory.Any(p => p.Key != snapshot.playerIndex && p.Value.ContainsKey(entry.Key));
                    if (!conflict && _removed.TryGetValue(entry.Key, out removed) && removed.player != snapshot.playerIndex && snapshot.time - removed.time >= 0 && snapshot.time - removed.time <= 2)
                    { row.kind = "ItemTransferObserved"; row.actorPlayerIndex = removed.player; row.actorInferred = true; row.targetPlayerIndex = snapshot.playerIndex; row.confidence = "Likely"; }
                    AddItem(row);
                }
            }
            _inventory[snapshot.playerIndex] = current;
            foreach (string key in _arrivals.Where(p => snapshot.time - p.Value.time > 2).Select(p => p.Key).ToArray()) _arrivals.Remove(key);
            _attribution.Inventory(snapshot, _epoch, _evidence);
        }

        internal static IEnumerable<ResourceDelta> Resources(ItemSnapshot before, ItemSnapshot after)
        {
            if (before.guid != after.guid) yield break;
            var pairs = new[] {
                new ResourceDelta("uses", before.hasUsesValue && after.hasUsesValue, before.uses, after.uses),
                new ResourceDelta("fuel", before.hasFuel && after.hasFuel, before.fuel, after.fuel),
                new ResourceDelta("useRemaining", before.hasUseRemaining && after.hasUseRemaining, before.useRemaining, after.useRemaining),
                new ResourceDelta("cookedAmount", before.hasCookedAmount && after.hasCookedAmount, before.cookedAmount, after.cookedAmount),
                new ResourceDelta("petterItemUses", before.hasPetterItemUses && after.hasPetterItemUses, before.petterItemUses, after.petterItemUses),
                new ResourceDelta("used", before.hasUsed && after.hasUsed, before.used ? 1 : 0, after.used ? 1 : 0),
                new ResourceDelta("flareActive", before.hasFlareActive && after.hasFlareActive, before.flareActive ? 1 : 0, after.flareActive ? 1 : 0),
                new ResourceDelta("powerEnabled", before.hasPowerEnabled && after.hasPowerEnabled, before.powerEnabled ? 1 : 0, after.powerEnabled ? 1 : 0) };
            foreach (ResourceDelta p in pairs) if (p.valid && Finite(p.before) && Finite(p.after) && Math.Abs(p.before - p.after) > .0001f) yield return p;
        }

        private void AddEvent(StatsEvent e, bool associate)
        {
            if (e == null || !Finite(e.time)) return;
            if (e.type == "RecordingResumed" && associate) { Break("RecordingResumed"); _evidence.epoch = _epoch; }
            if (associate) _attribution.Event(e, _epoch);
            _lifecycle.Event(e, _evidence);
            Result.overview.durationSeconds = Math.Max(Result.overview.durationSeconds, e.time);
            AnalysisPlayer player;
            if (_players.TryGetValue(e.subjectPlayerIndex, out player))
            {
                if (e.type == "PlayerJumped") { player.jumpCount++; player.jumpTimes.Add(e.time); player.jumpEvidence.Add(_evidence); }
            }
            if (e.type == "PlayerRevivedObserved" || e.type == "PlayerLifeSaved" || e.type == "PlayerFriendHealed" || e.type == "PlayerRescuePulled")
            {
                AddAssistance(e, associate);
                return;
            }
            if (e.type == "MountainProgressObserved" && e.segmentIndex >= 0 && associate) _initialReached.Add(e.segmentIndex);
            if (e.type == "MountainSegmentReached" && e.segmentIndex >= 0 && !_initialReached.Contains(e.segmentIndex) && associate)
            {
                if (!_reached.ContainsKey(e.segmentIndex) || _derived.Contains(e.segmentIndex)) _reached[e.segmentIndex] = e.time;
                _derived.Remove(e.segmentIndex);
                Result.timeline.Add(new ReportEvent { evidence = _evidence, time = e.time, endTime = e.time, kind = "ProgressReached", detail = e.segmentKey, playerIndex = e.subjectPlayerIndex, confidence = "Certain" });
            }
            if (e.type == "ObservationContextChanged")
            {
                if (!associate) { Diagnose(e.time, e.subjectPlayerIndex, "ClockAmbiguous"); return; }
                int context = (int)e.value;
                if ((context & 7) != 0 || (context & 24) != 24) _blockedPlayers.Add(e.subjectPlayerIndex); else _blockedPlayers.Remove(e.subjectPlayerIndex);
                InterruptPlayer(e.subjectPlayerIndex, e.time); return;
            }
            if (string.IsNullOrEmpty(e.itemName) && string.IsNullOrEmpty(e.itemGuid)) return;
            Result.overview.itemObservationCount++;
            // Inventory snapshots are the authority for presence and numeric resource changes.
            if (e.type == "ItemChangedObserved" || e.type == "ItemRemovedObserved" || e.type == "ItemResourceChanged" || e.type == "ItemPickupRequested" || e.type == "ItemUseStarted" || e.type == "ItemUsesReduced") return;
            var row = new AnalysisItemObservation { time = e.time, epoch = _epoch, playerIndex = e.subjectPlayerIndex, actorPlayerIndex = e.actorPlayerIndex ?? -1,
                targetPlayerIndex = e.targetPlayerIndex, itemId = e.itemId, itemGuid = e.itemGuid, itemName = e.itemName, prefabName = e.definitionKey,
                slot = e.slot, kind = e.type, confidence = e.type == "ItemConsumed" ? "Certain" : "Ambiguous", detail = e.detail };
            if ((e.type == "ItemConsumed" || e.type == "ItemFedToPlayer") && row.targetPlayerIndex < 0) row.targetPlayerIndex = row.playerIndex;
            if (associate && row.actorPlayerIndex < 0 && !string.IsNullOrEmpty(row.itemGuid))
            {
                int[] owners = _inventory.Where(p => p.Value.ContainsKey(row.itemGuid)).Select(p => p.Key).ToArray();
                Removal removed;
                if (owners.Length == 1) { row.actorPlayerIndex = owners[0]; row.actorInferred = true; }
                else if (owners.Length == 0 && _removed.TryGetValue(row.itemGuid, out removed) && e.time >= removed.time && e.time - removed.time <= 2) { row.actorPlayerIndex = removed.player; row.actorInferred = true; }
                else if (owners.Length > 1) row.detail = "ConflictingGuid";
            }
            if (associate && row.actorPlayerIndex >= 0 && !string.IsNullOrEmpty(row.itemGuid) && _inventory.TryGetValue(row.actorPlayerIndex, out Dictionary<string, ItemSnapshot> inventory) && inventory.TryGetValue(row.itemGuid, out ItemSnapshot held))
                row.cookedAmount = held.hasCookedAmount ? held.cookedAmount : (int?)null;
            AddItem(row, associate);
        }

        private void AddAssistance(StatsEvent e, bool associate)
        {
            int target = e.type == "PlayerRevivedObserved" ? e.subjectPlayerIndex : e.targetPlayerIndex;
            if (!_players.ContainsKey(target)) return;
            // Missing actor remains unknown: the event subject/host/last holder is not a substitute.
            int actor = e.actorPlayerIndex.HasValue && _players.ContainsKey(e.actorPlayerIndex.Value) ? e.actorPlayerIndex.Value : -1;
            if (e.type == "PlayerRevivedObserved") actor = -1;
            if (actor == target || !Finite(e.value)) return;
            if (Result.assistance.Count > 0 && Result.assistance.AsEnumerable().Reverse().Take(32).Any(a => a.epoch == _epoch &&
                a.time == e.time && a.kind == e.type && a.actorPlayerIndex == actor && a.targetPlayerIndex == target &&
                a.itemGuid == e.itemGuid && a.resourceKey == e.resourceKey && a.amount == e.value)) return;
            Result.assistance.Add(new AnalysisAssistanceEvent { time = e.time, epoch = _epoch, kind = e.type,
                actorPlayerIndex = actor, targetPlayerIndex = target, itemName = e.itemName, itemGuid = e.itemGuid,
                amount = Math.Max(0, e.value), resourceKey = e.resourceKey });
            Result.timeline.Add(new ReportEvent { evidence = _evidence, time = e.time, endTime = e.time, kind = e.type, playerIndex = actor,
                otherPlayerIndex = target, confidence = "Certain", detail = e.itemName });
            AnalysisPlayer player;
            if (!_players.TryGetValue(actor, out player)) return;
            if (e.type == "PlayerLifeSaved") player.lifeSavedCount++;
            if (e.type == "PlayerRescuePulled") player.rescuePullCount++;
            if (e.type == "PlayerFriendHealed")
            {
                player.friendHealingAmount += Math.Max(0, e.value);
                if (associate) _attribution.DirectTreatment(e, _evidence);
            }
        }

        private void AddItem(AnalysisItemObservation row, bool associate = true)
        {
            row.epoch = _evidence?.epoch ?? _epoch; row.evidence = _evidence;
            string key = row.playerIndex + ":" + row.itemGuid + ":" + row.kind + ":" + row.resourceKey;
            AnalysisItemObservation old;
            if (_recentEvidence.TryGetValue(key, out old) && old.epoch == row.epoch && Math.Abs(row.time - old.time) < .05f && old.previousValue == row.previousValue && old.value == row.value) { old.evidenceCount++; return; }
            _recentEvidence[key] = row;
            if (_recentEvidence.Count > 4096) _recentEvidence.Clear();
            row.observationId = Result.items.Count + 1;
            Result.items.Add(row);
            if (associate) _attribution.Observe(row);
        }

        private static AnalysisItemObservation Row(float time, int player, ItemSnapshot item, string kind, string confidence)
        {
            return new AnalysisItemObservation { time = time, playerIndex = player, itemId = item.itemId, itemGuid = item.guid, itemName = item.itemName, prefabName = item.prefabName, slot = item.slot, kind = kind, confidence = confidence, cookedAmount = item.hasCookedAmount ? item.cookedAmount : (int?)null };
        }

        internal AnalysisResult Finish(CancellationToken token = default(CancellationToken))
        {
            token.ThrowIfCancellationRequested();
            _attribution.Finish();
            _lifecycle.Finish();
            foreach (int id in _isolation.Keys.ToArray()) EndIsolation(id, "RecordingEnd");
            foreach (Trend trend in _trends.Values) trend.Finish();
            foreach (AnalysisPlayer p in Result.players) { p.staminaAverage = p.aliveSeconds > 0 ? p.staminaAverage / p.aliveSeconds : 0; if (!Finite(p.staminaMin)) p.staminaMin = 0; }
            Result.overview.jumpCount = Result.players.Sum(p => p.jumpCount);
            Result.overview.lifeSavedCount = Result.players.Sum(p => p.lifeSavedCount);
            Result.overview.rescuePullCount = Result.players.Sum(p => p.rescuePullCount);
            Result.overview.friendHealingAmount = Result.players.Sum(p => p.friendHealingAmount);
            foreach (AnalysisDistancePair pair in _pairs.Values) { SummarizeDistance(pair, 0, float.MaxValue); Result.distancePairs.Add(pair); }
            token.ThrowIfCancellationRequested();
            Result.overview.validDistanceCount = Result.distancePairs.Sum(p => p.sampleCount);
            var catalog = Result.mountainSegments.Where(s => s != null).OrderBy(s => s.index).ToList();
            if (catalog.Count > 0 && _initialReached.Count == 0 && _reached.TryGetValue(catalog[0].index, out float firstReached))
                Result.segments.Add(new AnalysisSegment { index = -2, titleKey = "INITIAL", displayName = "Initial", hasStartTime = true, hasEndTime = true, startTime = 0, endTime = firstReached });
            for (int i = 0; i < catalog.Count; i++)
            {
                MountainSegmentDefinition segment = catalog[i];
                float start; bool known = !_initialReached.Contains(segment.index) && _reached.TryGetValue(segment.index, out start);
                start = known ? _reached[segment.index] : 0;
                float end = Result.overview.durationSeconds;
                bool endKnown = i == catalog.Count - 1 || _reached.TryGetValue(catalog[i + 1].index, out end);
                Result.segments.Add(new AnalysisSegment { index = segment.index, titleKey = segment.titleKey, displayName = segment.capturedTitle,
                    hasStartTime = known, hasEndTime = endKnown && end >= start, startTime = start, endTime = end, derivedFromBoundary = _derived.Contains(segment.index) });
            }
            _report.Finish();
            token.ThrowIfCancellationRequested();
            return Result;
        }

        internal static void SummarizeDistance(AnalysisDistancePair pair, float start, float end, int? epoch = null)
        {
            var values = new List<Tuple<float, float>>();
            double seconds = 0, over25 = 0, over50 = 0, over100 = 0;
            for (int i = 0; i + 2 < pair.intervals.Count; i += 3)
            {
                if (epoch.HasValue && (pair.intervalEpochs.Count <= i / 3 || pair.intervalEpochs[i / 3] != epoch.Value)) continue;
                float t = pair.intervals[i], w = Math.Max(0, Math.Min(end, t + pair.intervals[i + 1]) - Math.Max(start, t)), d = pair.intervals[i + 2];
                if (w <= 0) continue;
                seconds += w; if (d > 25) over25 += w; if (d > 50) over50 += w; if (d > 100) over100 += w;
                values.Add(Tuple.Create(d, w));
            }
            values.Sort((a, b) => a.Item1.CompareTo(b.Item1));
            pair.validSeconds = (float)seconds; pair.over25Ratio = seconds > 0 ? (float)(over25 / seconds) : 0;
            pair.over50Ratio = seconds > 0 ? (float)(over50 / seconds) : 0; pair.over100Ratio = seconds > 0 ? (float)(over100 / seconds) : 0;
            pair.median = WeightedQuantile(values, seconds * .5); pair.p90 = WeightedQuantile(values, seconds * .9); pair.p99 = WeightedQuantile(values, seconds * .99);
        }
        private static float WeightedQuantile(List<Tuple<float, float>> values, double target) { double sum = 0; foreach (var p in values) { sum += p.Item2; if (sum >= target) return p.Item1; } return 0; }
        internal static ItemDefinition Definition(List<ItemDefinition> definitions, ushort id, string name)
        {
            string stable = name != null && name.EndsWith("(Clone)", StringComparison.Ordinal) ? name.Substring(0, name.Length - 7).TrimEnd() : name;
            return definitions.FirstOrDefault(d => d != null && d.itemId == id && (string.IsNullOrEmpty(stable) || d.prefabName == stable))
                ?? definitions.FirstOrDefault(d => d != null && !string.IsNullOrEmpty(stable) && d.prefabName == stable);
        }
        internal static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        internal static bool ValidStamina(PlayerTelemetry p) { return p != null && Finite(p.regularStamina) && Finite(p.extraStamina) && Finite(p.maxStamina); }
        internal static bool ValidPosition(PlayerTelemetry p) { return p != null && !p.dead && Finite(p.positionX) && Finite(p.positionY) && Finite(p.positionZ) &&
            p.positionX * p.positionX + (p.positionY - 5000) * (p.positionY - 5000) + (p.positionZ + 5000) * (p.positionZ + 5000) > ReportThresholds.SentinelRadius * ReportThresholds.SentinelRadius; }
        internal static float WorldDistance(PlayerTelemetry a, PlayerTelemetry b) { return (float)Math.Sqrt(Math.Pow(a.positionX - b.positionX, 2) + Math.Pow(a.positionY - b.positionY, 2) + Math.Pow(a.positionZ - b.positionZ, 2)); }
        internal sealed class ResourceDelta { internal string key; internal bool valid; internal float before, after; internal ResourceDelta(string k, bool v, float a, float b) { key = k; valid = v; before = a; after = b; } }
        private sealed class Entry { internal float time; internal int index; internal PlayerSample sample; internal InventorySnapshot inventory; internal StatsEvent ev; }
        private sealed class Removal { internal float time; internal int player; }
        private sealed class Arrival { internal float time; internal int player; internal ItemSnapshot item; }
        private sealed class Trend
        {
            private readonly AnalysisPlayer _player;
            private float _start, _end, _first, _last;
            private bool _active, _burst;
            private int _direction;
            private readonly Queue<Tuple<float, float>> _window = new Queue<Tuple<float, float>>();
            internal Trend(AnalysisPlayer player) { _player = player; }
            internal void Add(float time, float value)
            {
                if (!_active) { _active = true; _start = time; _first = value; _direction = 0; }
                float delta = value - (_direction == 0 ? _first : _last);
                int direction = delta > .002f ? 1 : delta < -.002f ? -1 : 0;
                if (direction != 0 && _direction != 0 && direction != _direction) { float lastTime = _end, lastValue = _last; Finish(); _active = true; _start = lastTime; _first = lastValue; }
                if (direction != 0) _direction = direction;
                _window.Enqueue(Tuple.Create(time, value));
                while (_window.Count > 0 && time - _window.Peek().Item1 > .5f) _window.Dequeue();
                if (_window.Count > 0 && Math.Abs(value - _window.Peek().Item2) >= .1f) _burst = true;
                _last = value; _end = time;
            }
            internal void Finish()
            {
                if (_active && _direction != 0 && (Math.Abs(_last - _first) >= .02f || _end - _start >= 1))
                    _player.staminaEpisodes.Add(new AnalysisStaminaEpisode { startTime = _start, endTime = _end, startValue = _first, endValue = _last, direction = _last >= _first ? "Recovery" : "Drain", burst = _burst });
                _active = false; _burst = false; _window.Clear();
            }
        }
    }
}
